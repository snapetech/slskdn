// <copyright file="RelayClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

// <copyright file="RelayClient.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>
using Microsoft.Extensions.Options;
using slskd.Files;

namespace slskd.Relay
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Security;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Serilog;
    using slskd.Cryptography;
    using slskd.Shares;

    /// <summary>
    ///     Relay client (agent).
    /// </summary>
    public class RelayClient : IRelayClient
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RelayClient"/> class.
        /// </summary>
        /// <param name="shareService"></param>
        /// <param name="fileService"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="httpClientFactory"></param>
        public RelayClient(
            IShareService shareService,
            FileService fileService,
            IOptionsMonitor<Options> optionsMonitor,
            IHttpClientFactory httpClientFactory)
        {
            Shares = shareService;
            Files = fileService;

            HttpClientFactory = httpClientFactory;

            StateMonitor = State;

            OptionsMonitor = optionsMonitor;
            OptionsMonitorRegistration = OptionsMonitor.OnChange(options => Configure(options));

            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the client state.
        /// </summary>
        public IStateMonitor<RelayClientState> StateMonitor { get; }

        private FileService Files { get; }
        private SemaphoreSlim ConfigurationSyncRoot { get; } = new SemaphoreSlim(1, 1);
        private bool Disposed { get; set; }
        private IHttpClientFactory HttpClientFactory { get; }
        private HubConnection? HubConnection { get; set; }
        private string LastOptionsHash { get; set; } = string.Empty;
        private ILogger Log { get; } = Serilog.Log.ForContext<RelayClient>();
        private bool LoggedIn { get; set; }
        private TaskCompletionSource LoggedInTaskCompletionSource { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IDisposable? OptionsMonitorRegistration { get; set; }
        private IShareService Shares { get; }
        private CancellationTokenSource? StartCancellationTokenSource { get; set; }
        private bool StartRequested { get; set; }
        private ManagedState<RelayClientState> State { get; } = new();
        private SemaphoreSlim StateSyncRoot { get; } = new SemaphoreSlim(1, 1);

        private static string GetRelayTokenLogId(Guid token)
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token.ToString()));
            return $"relay-token:{Convert.ToHexString(digest.AsSpan(0, 6)).ToLowerInvariant()}";
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Starts the client and connects to the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!StateSyncRoot.Wait(0, cancellationToken))
            {
                // we're already attempting to connect, let the existing attempt handle it
                return;
            }

            try
            {
                var mode = OptionsMonitor.CurrentValue.Relay.Mode.ToEnum<RelayMode>();

                if (mode != RelayMode.Agent && mode != RelayMode.Debug)
                {
                    throw new InvalidOperationException($"Relay client can only be started when operation mode is {RelayMode.Agent}");
                }

                StartCancellationTokenSource?.Cancel();
                StartCancellationTokenSource?.Dispose();
                StartCancellationTokenSource = new CancellationTokenSource();
                StartRequested = true;

                // retry indefinitely
                await Retry.Do(
                    task: async () =>
                    {
                        Log.Information("Attempting to connect to the relay controller {Address}", OptionsMonitor.CurrentValue.Relay.Controller.Address);

                        ResetLoggedInState();
                        State.SetValue(_ => TranslateState(HubConnectionState.Connecting));

                        var startCancellationTokenSource = StartCancellationTokenSource
                            ?? throw new InvalidOperationException("Relay start cancellation token source was not initialized.");
                        var hubConnection = HubConnection
                            ?? throw new InvalidOperationException("Relay hub connection was not initialized.");
                        await hubConnection.StartAsync(startCancellationTokenSource.Token);

                        State.SetValue(_ => TranslateState(hubConnection.State));
                        Log.Information("Relay controller connection established. Awaiting authentication...");

                        // the controller will send an authentication challenge immediately after connection
                        // wait for the auth handler to log in before proceeding with the initial share upload
                        await LoggedInTaskCompletionSource.Task;

                        Log.Information("Uploading shares...");

                        try
                        {
                            await UploadSharesAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, ex.Message);
                            Log.Error("Disconnecting from the relay controller");

                            // stop, so we can start again when the retry loop comes back around
                            await StopAsync();

                            // throw, to trigger a retry
                            throw;
                        }

                        Log.Information("Shares uploaded. Ready to relay files.");
                    },
                    isRetryable: (attempts, ex) => true,
                    onFailure: (attempts, ex) =>
                    {
                        Log.Debug(ex, "Relay hub connection failure");
                        Log.Warning("Failed attempt #{Attempts} to connect to relay controller: {Message}", attempts, ex.Message);
                    },
                    maxAttempts: int.MaxValue,
                    baseDelayInMilliseconds: 1000,
                    maxDelayInMilliseconds: 30000,
                    cancellationToken: StartCancellationTokenSource.Token);
            }
            finally
            {
                StateSyncRoot.Release();
            }
        }

        /// <summary>
        ///     Stops the client and disconnects from the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            StartRequested = false;
            var startCancellationTokenSource = StartCancellationTokenSource;
            StartCancellationTokenSource = null;
            startCancellationTokenSource?.Cancel();

            if (HubConnection != null)
            {
                await HubConnection.StopAsync(cancellationToken);

                ResetLoggedInState();
                State.SetValue(_ => TranslateState(HubConnectionState.Disconnected));

                Log.Information("Relay controller connection disconnected");
            }

            startCancellationTokenSource?.Dispose();
        }

        /// <summary>
        ///     Synchronizes state with the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        public Task SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            return UploadSharesAsync(cancellationToken);
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    var hubConnection = HubConnection;
                    HubConnection = null;

                    OptionsMonitorRegistration?.Dispose();
                    OptionsMonitorRegistration = null;
                    StartCancellationTokenSource?.Cancel();
                    StartCancellationTokenSource?.Dispose();

                    DisposeHubConnection(hubConnection, "[RelayClient] Failed to dispose HubConnection");
                }

                Disposed = true;
            }
        }

        private string ComputeCredential(string token)
        {
            var options = OptionsMonitor.CurrentValue;

            var key = Pbkdf2.GetKey(options.Relay.Controller.Secret, options.InstanceName, 48);
            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);

            return Convert.ToBase64String(System.Security.Cryptography.HMACSHA256.HashData(key, tokenBytes));
        }

        private string ComputeCredential(Guid token) => ComputeCredential(token.ToString());

        private void Configure(Options options)
        {
            var mode = options.Relay.Mode.ToEnum<RelayMode>();

            if (mode != RelayMode.Agent && mode != RelayMode.Debug)
            {
                return;
            }

            ConfigurationSyncRoot.Wait();

            try
            {
                var optionsHash = Compute.Sha1Hash(options.Relay.Controller.ToJson());

                if (optionsHash == LastOptionsHash)
                {
                    return;
                }

                Log.Debug("Relay options changed. Reconfiguring...");

                // if the client is attempting a connection, cancel it it's going to be dropped when we create a new instance, but
                // we need the retry loop around connection to stop.
                var previousHubConnection = HubConnection;
                HubConnection = null;
                StartCancellationTokenSource?.Cancel();
                StartCancellationTokenSource?.Dispose();
                StartCancellationTokenSource = null;
                DisposeHubConnection(previousHubConnection, "[RelayClient] Failed to dispose previous HubConnection during reconfiguration");

                var pinnedSpkiPins = RelayTlsPinValidator.ParsePins(options.Relay.Controller.PinnedSpki);

                if (options.Relay.Controller.IgnoreCertificateErrors && pinnedSpkiPins.Length == 0)
                {
                    Log.Warning("[RelayClient] MED-08: relay.controller.ignore_certificate_errors is enabled — " +
                        "TLS certificate validation is REDUCED for the relay controller connection. " +
                        "Valid TLS chains and self-signed/untrusted-root certificates are allowed; full CA-chain " +
                        "validation is still rejected. This should be used only in controlled lab environments.");
                }
                else if (pinnedSpkiPins.Length > 0)
                {
                    Log.Information("[RelayClient] HARDENING-2026-04-20 H8-pin: controller TLS is SPKI-pinned " +
                        "({PinCount} pin(s) configured). CA/IgnoreCertificateErrors settings are overridden by pinning.", pinnedSpkiPins.Length);
                }

                HubConnection = new HubConnectionBuilder()
                    .WithUrl($"{options.Relay.Controller.Address}/hub/relay", builder =>
                    {
                        builder.AccessTokenProvider = () => Task.FromResult<string?>(options.Relay.Controller.ApiKey);
                        builder.HttpMessageHandlerFactory = (message) =>
                        {
                            if (message is HttpClientHandler clientHandler)
                            {
                                if (pinnedSpkiPins.Length > 0)
                                {
                                    // HARDENING-2026-04-20 H8-pin: pins are authoritative; reject any cert whose
                                    // SPKI doesn't match, regardless of IgnoreCertificateErrors.
                                    clientHandler.ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                                        IsAllowedRelayPinnedCertificate(cert, chain, errors, pinnedSpkiPins);
                                }
                                else if (options.Relay.Controller.IgnoreCertificateErrors)
                                {
                                    clientHandler.ServerCertificateCustomValidationCallback +=
                                        (_, certificate, chain, errors) => IsAllowedInsecureRelayCertificate(certificate, chain, errors);
                                }
                            }

                            return message;
                        };
                    })
                    .WithAutomaticReconnect(new ControllerRetryPolicy(0, 1, 3, 10, 30, 60))
                    .Build();

                HubConnection.Reconnected += HubConnection_Reconnected;
                HubConnection.Reconnecting += HubConnection_Reconnecting;
                HubConnection.Closed += HubConnection_Closed;

                HubConnection.On<string, long, Guid>(nameof(IRelayHub.RequestFileUpload), HandleFileUploadRequest);
                HubConnection.On<string, Guid>(nameof(IRelayHub.RequestFileInfo), HandleFileInfoRequest);
                HubConnection.On<string>(nameof(IRelayHub.Challenge), HandleAuthenticationChallenge);
                HubConnection.On<string, Guid>(nameof(IRelayHub.NotifyFileDownloadCompleted), HandleNotifyFileDownloadCompleted);

                LastOptionsHash = optionsHash;

                Log.Debug("Relay options reconfigured");

                // if start was requested (if StartAsync() was called externally), restart after re-configuration
                if (StartRequested)
                {
                    Log.Information("Reconnecting the relay controller connection...");
                    _ = StartAsync();
                }
            }
            finally
            {
                ConfigurationSyncRoot.Release();
            }
        }

        private void DisposeHubConnection(HubConnection? hubConnection, string failureMessage)
        {
            try
            {
                using var disposeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                hubConnection?.DisposeAsync().AsTask().WaitAsync(disposeTimeout.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Log.Warning("[RelayClient] Timed out while disposing HubConnection");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, failureMessage);
            }
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpClient takes ownership of the handler when disposeHandler is true.")]
        private HttpClient CreateHttpClient()
        {
            var options = OptionsMonitor.CurrentValue.Relay.Controller;
            var pinnedSpkiPins = RelayTlsPinValidator.ParsePins(options.PinnedSpki);
            HttpClient client;

            if (pinnedSpkiPins.Length > 0)
            {
                // HARDENING-2026-04-20 H8-pin: pins are authoritative. Use a handler that requires
                // the controller's SPKI to match one of the configured pins; CA chain state and
                // IgnoreCertificateErrors no longer matter for the file-upload HTTP client either.
                var handler = new HttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                        IsAllowedRelayPinnedCertificate(cert, chain, errors, pinnedSpkiPins),
                };
                client = new HttpClient(handler, disposeHandler: true);
                handler = null!;
            }
            else if (options.IgnoreCertificateErrors)
            {
                var handler = new HttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                        IsAllowedInsecureRelayCertificate(cert, chain, errors),
                };
                client = new HttpClient(handler, disposeHandler: true);
                handler = null!;
            }
            else
            {
                client = new HttpClient();
            }

            client.Timeout = TimeSpan.FromMilliseconds(int.MaxValue);
            client.BaseAddress = new(options.Address);
            return client;
        }

        private static bool IsAllowedInsecureRelayCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null || sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
            {
                return false;
            }

            var certificate2 = certificate as X509Certificate2;
            if (certificate2 == null)
            {
                return false;
            }

            if (!string.Equals(certificate2.Subject, certificate2.Issuer, StringComparison.Ordinal))
            {
                return false;
            }

            if (chain == null || chain.ChainStatus.Length == 0)
            {
                return false;
            }

            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                    status.Status != X509ChainStatusFlags.PartialChain)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAllowedRelayPinnedCertificate(
            X509Certificate2? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors,
            string[] expectedPins)
        {
            if (certificate is null || !RelayTlsPinValidator.IsPinned(certificate, expectedPins))
            {
                return false;
            }

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
            {
                return false;
            }

            if (!string.Equals(certificate.Subject, certificate.Issuer, StringComparison.Ordinal))
            {
                return false;
            }

            if (chain == null || chain.ChainStatus.Length == 0)
            {
                return false;
            }

            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                    status.Status != X509ChainStatusFlags.PartialChain)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task HandleAuthenticationChallenge(string challengeToken)
        {
            try
            {
                Log.Information("Relay controller sent an authentication challenge");

                var options = OptionsMonitor.CurrentValue;

                var agent = options.InstanceName;
                var response = ComputeCredential(challengeToken);

                Log.Information("Logging in...");

                var hubConnection = HubConnection ?? throw new InvalidOperationException("Relay hub connection is not configured");
                await hubConnection.InvokeAsync(nameof(RelayHub.Login), agent, response);

                LoggedIn = true;
                Log.Information("Login succeeded.");

                LoggedInTaskCompletionSource.TrySetResult();
            }
            catch (UnauthorizedAccessException)
            {
                if (HubConnection != null)
                {
                    await HubConnection.StopAsync();
                }

                Log.Error("Relay controller authentication failed. Check configuration.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to handle authentication challenge: {Message}", ex.Message);
            }
        }

        private async Task HandleFileInfoRequest(string filename, Guid id)
        {
            Log.Information("Relay controller requested file info for {Filename} with ID {Id}", filename, GetRelayTokenLogId(id));

            try
            {
                var (_, localFilename, _) = await Shares.ResolveFileAsync(filename);

                var localFileInfo = Files.ResolveFileInfo(localFilename);

                var hubConnection = HubConnection ?? throw new InvalidOperationException("Relay hub connection is not configured");
                await hubConnection.InvokeAsync(nameof(RelayHub.ReturnFileInfo), id, localFileInfo.Exists, localFileInfo.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to handle file info request: {Message}", ex.Message);
                if (HubConnection != null)
                {
                    await HubConnection.InvokeAsync(nameof(RelayHub.ReturnFileInfo), id, false, 0);
                }
            }
        }

        private Task HandleFileUploadRequest(string filename, long startOffset, Guid token)
        {
            _ = Task.Run(async () =>
            {
                Log.Information("Relay controller requested file {Filename} with ID {Id}", filename, GetRelayTokenLogId(token));

                try
                {
                    var (_, localFilename, _) = await Shares.ResolveFileAsync(filename);

                    var localFileInfo = Files.ResolveFileInfo(localFilename);

                    if (!localFileInfo.Exists)
                    {
                        Shares.RequestScan();
                        throw new NotFoundException($"The file '{localFilename}' could not be located on disk. A share scan should be performed.");
                    }

                    using var stream = new FileStream(localFileInfo.FullName, FileMode.Open, FileAccess.Read);

                    stream.Seek(startOffset, SeekOrigin.Begin);

                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v0/relay/controller/files/{token}");

                    request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Relay.Controller.ApiKey);
                    request.Headers.Add("X-Relay-Agent", OptionsMonitor.CurrentValue.InstanceName);
                    request.Headers.Add("X-Relay-Credential", ComputeCredential(token));

                    using var content = new MultipartFormDataContent
                    {
                        { new StreamContent(stream), "file", filename },
                    };

                    request.Content = content;

                    Log.Information("Beginning upload of file {Filename} with ID {Id}", filename, GetRelayTokenLogId(token));
                    using var client = CreateHttpClient();
                    using var response = await client.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error("Upload of file {Filename} with ID {Id} failed: {StatusCode}", filename, GetRelayTokenLogId(token), response.StatusCode);
                    }
                    else
                    {
                        Log.Information("Upload of file {Filename} with ID {Id} succeeded.", filename, GetRelayTokenLogId(token));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to handle file request: {Message}", ex.Message);

                    // report the failure to the controller. this avoids a failure due to timeout.
                    if (HubConnection != null)
                    {
                        try
                        {
                            await HubConnection.InvokeAsync(nameof(RelayHub.NotifyFileUploadFailed), token, ex);
                        }
                        catch (Exception notifyEx)
                        {
                            Log.Error(notifyEx, "Failed to report relay upload failure for {Filename} with ID {Id}", filename, GetRelayTokenLogId(token));
                        }
                    }
                }
            });

            return Task.CompletedTask;
        }

        private Task HandleNotifyFileDownloadCompleted(string filename, Guid token)
        {
            if (!OptionsMonitor.CurrentValue.Relay.Controller.Downloads)
            {
                return Task.CompletedTask;
            }

            Log.Information("Relay controller sent a download notification for {Filename} ({Token})", filename, GetRelayTokenLogId(token));

            _ = Task.Run(async () =>
            {
                try
                {
                    var destinationFile = Path.Combine(OptionsMonitor.CurrentValue.Directories.Downloads, filename);

                    if (OptionsMonitor.CurrentValue.Relay.Mode.ToEnum<RelayMode>() == RelayMode.Debug)
                    {
                        // if we're debugging, we're referencing the same file for both the controller and agent which will lead to an
                        // access violation. prefix the destination file to avoid this.
                        destinationFile = Path.Combine(OptionsMonitor.CurrentValue.Directories.Downloads, $"{filename}.relayed");
                    }

                    // if the controller is Windows and the agent is Linux or vice versa, we need to translate the filename to the
                    // local OS or we're going to get funny results when we go to write the file
                    destinationFile = destinationFile.LocalizePath();

                    await Retry.Do(task: async () =>
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v0/relay/controller/downloads/{token}");

                        request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Relay.Controller.ApiKey);
                        request.Headers.Add("X-Relay-Agent", OptionsMonitor.CurrentValue.InstanceName);
                        request.Headers.Add("X-Relay-Credential", ComputeCredential(token));
                        request.Headers.Add("X-Relay-Filename-Base64", filename.ToBase64());

                        using var client = CreateHttpClient();
                        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                        response.EnsureSuccessStatusCode();

                        using var remoteStream = await response.Content.ReadAsStreamAsync();

                        var destinationDirectory = Path.GetDirectoryName(destinationFile)
                            ?? throw new IOException($"Failed to determine destination directory for download {destinationFile}");
                        Directory.CreateDirectory(destinationDirectory);
                        using var localStream = new FileStream(destinationFile, FileMode.Create);
                        await remoteStream.CopyToAsync(localStream);
                    },
                    isRetryable: (_, _) => true,
                    onFailure: (_, ex) => Log.Error(ex, "Failed to handle file download notification for {Filename} ({Token})", filename, GetRelayTokenLogId(token)),
                    maxAttempts: 3,
                    baseDelayInMilliseconds: 1000,
                    maxDelayInMilliseconds: 60000);

                    Log.Information("File {Filename} successfully downloaded to {Destination}", filename, destinationFile);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Relay download notification handling failed for {Filename} ({Token})", filename, GetRelayTokenLogId(token));
                }
            });

            return Task.CompletedTask;
        }

        private Task HubConnection_Closed(Exception? arg)
        {
            Log.Warning("Relay controller connection closed: {Message}", arg?.Message);
            ResetLoggedInState();

            return Task.CompletedTask;
        }

        private async Task HubConnection_Reconnected(string? arg)
        {
            Log.Warning("Relay controller connection reconnected");

            try
            {
                // wait for the authentication flow to complete
                await LoggedInTaskCompletionSource.Task;

                Log.Information("Uploading shares...");

                await UploadSharesAsync();

                Log.Information("Shares uploaded. Ready to relay files.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to log in and/or upload shares: {Message}", ex.Message);
                Log.Error("Disconnecting from the relay controller");

                // stop, then fire and forget StartAsync() to re-enter the connection retry loop
                await StopAsync();
                _ = StartAsync();
            }
        }

        private Task HubConnection_Reconnecting(Exception? arg)
        {
            Log.Warning("Relay controller connection reconnecting: {Message}", arg?.Message);
            ResetLoggedInState();

            return Task.CompletedTask;
        }

        private void ResetLoggedInState()
        {
            LoggedIn = false;
            State.SetValue(_ => TranslateState(HubConnection?.State ?? HubConnectionState.Disconnected));

            var old = LoggedInTaskCompletionSource;

            LoggedInTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // in case someone was waiting on this, cancel it
            // _very important_ to avoid deadlocks
            old?.TrySetCanceled();
        }

        private RelayClientState TranslateState(HubConnectionState hub) => hub switch
        {
            HubConnectionState.Disconnected => RelayClientState.Disconnected,
            HubConnectionState.Connected => RelayClientState.Connected,
            HubConnectionState.Connecting => RelayClientState.Connecting,
            HubConnectionState.Reconnecting => RelayClientState.Reconnecting,
            _ => throw new ArgumentException($"Unexpected HubConnectionState {hub}"),
        };

        private async Task UploadSharesAsync(CancellationToken cancellationToken = default)
        {
            if (!LoggedIn)
            {
                return;
            }

            var temp = Path.Combine(Path.GetTempPath(), Program.AppName, $"share_backup_{Path.GetRandomFileName()}.db");

            try
            {
                Log.Debug("Backing up shares to {Filename}", temp);

                var tempDirectory = Path.GetDirectoryName(temp) ?? Path.GetTempPath();
                Directory.CreateDirectory(tempDirectory);

                await Shares.DumpAsync(temp);

                Log.Debug("Share backup successful");
                Log.Debug("Requesting share upload token...");

                Guid token = Guid.Empty;

                // retry this a few times. it can throw if a race condition materializes
                // between this logic and the authentication flow, and it'll almost certainly be
                // worked out properly if we just wait a second and try again
                await Retry.Do(task: async () =>
                    {
                        var hubConnection = HubConnection ?? throw new InvalidOperationException("Relay hub connection is not configured");
                        token = await hubConnection.InvokeAsync<Guid>(nameof(RelayHub.BeginShareUpload));
                    },
                    isRetryable: (_, _) => true,
                    onFailure: (count, ex) => Log.Warning("Failed attempt #{Attempts} to obtain share upload token: {Message}", count, ex.Message),
                    maxAttempts: 3,
                    baseDelayInMilliseconds: 1000,
                    maxDelayInMilliseconds: 5000,
                    cancellationToken: cancellationToken);

                Log.Debug("Received share upload token {Token}", GetRelayTokenLogId(token));

                using var stream = new FileStream(temp, FileMode.Open, FileAccess.Read);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v0/relay/controller/shares/{token}");

                request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Relay.Controller.ApiKey);
                request.Headers.Add("X-Relay-Agent", OptionsMonitor.CurrentValue.InstanceName);
                request.Headers.Add("X-Relay-Credential", ComputeCredential(token));

                using var sharesContent = new StringContent(Shares.LocalHost.Shares.ToJson());
                using var databaseContent = new StreamContent(stream);
                using var content = new MultipartFormDataContent
                {
                    { sharesContent, "shares" },
                    { databaseContent, "database", "shares" },
                };

                request.Content = content;

                var size = ((double)stream.Length).SizeSuffix();
                var sw = new Stopwatch();
                sw.Start();

                Log.Information("Beginning upload of shares ({Size})", size);
                Log.Debug("Shares: {Shares}", Shares.LocalHost.Shares.ToJson());
                using var client = CreateHttpClient();
                using var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new RelayException($"Failed to upload shares to relay controller: {response.StatusCode}");
                }

                sw.Stop();
                Log.Information("Upload of shares succeeded ({Size} in {Duration}ms)", size, sw.ElapsedMilliseconds);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        private sealed class ControllerRetryPolicy : IRetryPolicy
        {
            public ControllerRetryPolicy(params int[] intervals)
            {
                Intervals = intervals;
            }

            private int[] Intervals { get; set; }

            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                return TimeSpan.FromSeconds(Intervals[Math.Min(retryContext.PreviousRetryCount, Intervals.Length - 1)]);
            }
        }
    }
}
