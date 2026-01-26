// <copyright file="Program.cs" company="slskd Team">
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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.AudioCore;
using slskd.Mesh.Realm;
using slskd.Mesh.Realm.Bridge;
using slskd.Mesh.Governance;
using slskd.Mesh.Gossip;
using slskd.SocialFederation;

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.RateLimiting;
    using System.Threading.Tasks;
    using Asp.Versioning.ApiExplorer;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Diagnostics.HealthChecks;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Authorization;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.FileProviders.Physical;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi.Models;
    using Prometheus.DotNetRuntime;
    using Prometheus.SystemMetrics;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Grafana.Loki;
    using Serilog.Sinks.SystemConsole.Themes;
    using slskd.Authentication;
    using slskd.Audio;
    using slskd.LibraryHealth;
    using slskd.Configuration;
    using slskd.Core.API;
    using slskd.Cryptography;
    using slskd.Events;
    using slskd.Files;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.AutoTagging;
    using slskd.Integrations.Chromaprint;
    using slskd.Integrations.FTP;
    using slskd.Integrations.MetadataFacade;
    using slskd.Integrations.MusicBrainz;
    using slskd.Integrations.Pushbullet;
    using slskd.Integrations.Scripts;
    using slskd.Integrations.Webhooks;
    using slskd.Mesh;
    using slskd.Messaging;
    using slskd.Relay;
    using slskd.Search;
    using slskd.Search.API;
using slskd.Sharing;
using slskd.Shares;
using slskd.Streaming;
using slskd.Identity;
using slskd.Telemetry;
    using slskd.Transfers;
    using slskd.Transfers.Downloads;
    using slskd.Transfers.MultiSource;
    using slskd.Transfers.Uploads;
    using slskd.Users;
    using slskd.Common.Security;
    using slskd.Validation;
    using slskd.Common.Security;
    using slskd.DhtRendezvous;
    using slskd.DhtRendezvous.Security;
    using slskd.Transfers.MultiSource.Discovery;
    using slskd.Transfers.Rescue;
    using slskd.Signals;
    using Soulseek;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;
    using IOFile = System.IO.File;

    /// <summary>
    ///     Bootstraps configuration and handles primitive command-line instructions.
    /// </summary>
    public static class Program
    {
        /// <summary>
        ///     The name of the application.
        /// </summary>
        public static readonly string AppName = "slskd";

        /// <summary>
        ///     The DateTime of the 'genesis' of the application (the initial commit).
        /// </summary>
        public static readonly DateTime GenesisDateTime = new(2020, 12, 30, 6, 22, 0, DateTimeKind.Utc);

        /// <summary>
        ///     The name of the local share host.
        /// </summary>
        public static readonly string LocalHostName = "local";

        /// <summary>
        ///     The url to the issues/support site.
        /// </summary>
        public static readonly string IssuesUrl = "https://github.com/slskd/slskd/issues";

        /// <summary>
        ///     The global prefix for environment variables.
        /// </summary>
        public static readonly string EnvironmentVariablePrefix = $"{AppName.ToUpperInvariant()}_";

        /// <summary>
        ///     The default XML documentation filename.
        /// </summary>
        public static readonly string XmlDocumentationFile = Path.Combine(AppContext.BaseDirectory, "etc", $"{AppName}.xml");

        /// <summary>
        ///     The default application data directory.
        /// </summary>
        public static readonly string DefaultAppDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), AppName);

        /// <summary>
        ///     Gets the unique Id of this application invocation.
        /// </summary>
        public static readonly Guid InvocationId = Guid.NewGuid();

        /// <summary>
        ///     Gets the Id of the current application process.
        /// </summary>
        public static readonly int ProcessId = Environment.ProcessId;

        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        private static readonly Version AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.Equals(new Version(1, 0, 0, 0)) ? new Version(0, 0, 0, 0) : Assembly.GetExecutingAssembly().GetName().Version;

        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        private static readonly string InformationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion == "1.0.0" ? "0.0.0" : Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <summary>
        ///     Occurs when a new log event is emitted.
        /// </summary>
        public static event EventHandler<LogRecord> LogEmitted;

        /// <summary>
        ///     Gets the semantic application version.
        /// </summary>
        public static string SemanticVersion { get; } = InformationalVersion.Split('+').First();

        /// <summary>
        ///     Gets the full application version, including both assembly and informational versions.
        /// </summary>
        public static string FullVersion { get; } = $"{SemanticVersion} ({InformationalVersion})";

        /// <summary>
        ///     Gets a value indicating whether the current version is a Canary build.
        /// </summary>
        public static bool IsCanary { get; } = AssemblyVersion.Revision == 65534;

        /// <summary>
        ///     Gets a value indicating whether the current version is a Development build.
        /// </summary>
        public static bool IsDevelopment { get; } = new Version(0, 0, 0, 0) == AssemblyVersion;

        /// <summary>
        ///     Gets a value indicating whether the application is being run in Relay Agent mode.
        /// </summary>
        public static bool IsRelayAgent { get; private set; }

        /// <summary>
        ///     Gets the application flags.
        /// </summary>
        public static Options.FlagsOptions Flags { get; private set; }

        /// <summary>
        ///     Gets the path where application data is saved.
        /// </summary>
        [Argument('a', "app-dir", "path where application data is saved")]
        [EnvironmentVariable("APP_DIR")]
        public static string AppDirectory { get; private set; } = null;

        /// <summary>
        ///     Gets the fully qualified path to the application configuration file.
        /// </summary>
        [Argument('c', "config", "path to configuration file")]
        [EnvironmentVariable("CONFIG")]
        public static string ConfigurationFile { get; private set; } = null;

        /// <summary>
        ///     Gets the path where persistent data is saved.
        /// </summary>
        public static string DataDirectory { get; private set; } = null;

        /// <summary>
        ///     Gets the path where backups of persistent data saved.
        /// </summary>
        public static string DataBackupDirectory { get; private set; } = null;

        /// <summary>
        ///     Gets the default fully qualified path to the configuration file.
        /// </summary>
        public static string DefaultConfigurationFile { get; private set; }

        /// <summary>
        ///     Gets the default downloads directory.
        /// </summary>
        public static string DefaultDownloadsDirectory { get; private set; }

        /// <summary>
        ///     Gets the default incomplete download directory.
        /// </summary>
        public static string DefaultIncompleteDirectory { get; private set; }

        /// <summary>
        ///     Gets the path where application logs are saved.
        /// </summary>
        public static string LogDirectory { get; private set; } = null;

        /// <summary>
        ///     Gets the path where user-defined scripts are stored.
        /// </summary>
        public static string ScriptDirectory { get; private set; } = null;

        /// <summary>
        ///     Gets a buffer containing the last few log events.
        /// </summary>
        public static ConcurrentFixedSizeQueue<LogRecord> LogBuffer { get; } = new ConcurrentFixedSizeQueue<LogRecord>(size: 100);

        /// <summary>
        ///     Gets the master cancellation token source for the program.
        /// </summary>
        /// <remarks>
        ///     The token from this source should be used (or linked) to any long-running asynchronous task, so that when the application
        ///     begins to shut down these tasks also shut down in a timely manner. Actions that control the lifecycle of the program
        ///     (POSIX signals, a restart from the API, etc) should cancel this source.
        /// </remarks>
        public static CancellationTokenSource MasterCancellationTokenSource { get; } = new CancellationTokenSource();

        private static IConfigurationRoot Configuration { get; set; }
        private static OptionsAtStartup OptionsAtStartup { get; } = new OptionsAtStartup();
        
        // Explicit Serilog.ILogger type to avoid ambiguity with Microsoft.Extensions.Logging.ILogger
        private static Serilog.ILogger Log { get; set; } = new Serilog.LoggerConfiguration()
            .WriteTo.Sink(new ConsoleWriteLineLogger())
            .CreateLogger();
            
        private static Mutex Mutex { get; } = new Mutex(initiallyOwned: true, Compute.Sha256Hash(AppName));
        private static IDisposable DotNetRuntimeStats { get; set; }

        [Argument('g', "generate-cert", "generate X509 certificate and password for HTTPs")]
        private static bool GenerateCertificate { get; set; }

        [Argument('k', "generate-secret", "generate random secret of the specified length")]
        private static int GenerateSecret { get; set; }

        [Argument('n', "no-logo", "suppress logo on startup")]
        private static bool NoLogo { get; set; }

        [Argument('e', "envars", "display environment variables")]
        private static bool ShowEnvironmentVariables { get; set; }

        [Argument('h', "help", "display command line usage")]
        private static bool ShowHelp { get; set; }

        [Argument('v', "version", "display version information")]
        private static bool ShowVersion { get; set; }

        /// <summary>
        ///     Panic.
        /// </summary>
        /// <param name="code">An optional exit code.</param>
        public static void Exit(int code = 1) => Environment.Exit(code);

        /// <summary>
        ///     Entrypoint.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            // populate the properties above so that we can override the default config file if needed, and to
            // check if the application is being run in command mode (run task and quit).
            EnvironmentVariables.Populate(prefix: EnvironmentVariablePrefix);

            try
            {
                Arguments.Populate(clearExistingValues: false);
            }
            catch (Exception ex)
            {
                // this is pretty hacky, but i don't have a good way of trapping errors that bubble up here.
                Log.Error($"Invalid command line input: {ex.Message.Replace(".  See inner exception for details.", string.Empty)}");
                return;
            }

            // if a user has used one of the arguments above, perform the requested task, then quit
            if (ShowVersion)
            {
                Log.Information(FullVersion);
                return;
            }

            if (ShowHelp || ShowEnvironmentVariables)
            {
                if (!NoLogo)
                {
                    PrintLogo(FullVersion);
                }

                if (ShowHelp)
                {
                    PrintCommandLineArguments(typeof(Options));
                }

                if (ShowEnvironmentVariables)
                {
                    PrintEnvironmentVariables(typeof(Options), EnvironmentVariablePrefix);
                }

                return;
            }

            if (GenerateCertificate)
            {
                var (filename, password) = GenerateX509Certificate(password: Cryptography.Random.GetBytes(16).ToBase62(), filename: $"{AppName}.pfx");

                Log.Information($"Certificate exported to {filename}");
                Log.Information($"Password: {password}");
                return;
            }

            if (GenerateSecret > 0)
            {
                if (GenerateSecret < 16 || GenerateSecret > 255)
                {
                    Log.Error("Invalid command line input: secret length must be between 16 and 255, inclusive");
                    return;
                }

                Log.Information(Cryptography.Random.GetBytes(GenerateSecret).ToBase62());
                return;
            }

            // the application isn't being run in command mode. check the mutex to ensure
            // only one long-running instance.
            if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false))
            {
                Log.Fatal($"An instance of {AppName} is already running");
                return;
            }

            // derive the application directory value and defaults that are dependent upon it
            AppDirectory ??= DefaultAppDirectory;
            DataDirectory = Path.Combine(AppDirectory, "data");
            DataBackupDirectory = Path.Combine(DataDirectory, "backups");
            LogDirectory = Path.Combine(AppDirectory, "logs");
            ScriptDirectory = Path.Combine(AppDirectory, "scripts");

            DefaultConfigurationFile = Path.Combine(AppDirectory, $"{AppName}.yml");
            DefaultDownloadsDirectory = Path.Combine(AppDirectory, "downloads");
            DefaultIncompleteDirectory = Path.Combine(AppDirectory, "incomplete");

            // the location of the configuration file might have been overridden by command line or envar.
            // if not, set it to the default.
            ConfigurationFile ??= DefaultConfigurationFile;

            // verify(create if needed) default application directories. if the downloads or complete
            // directories are overridden in config, those will be validated after the config is loaded.
            try
            {
                VerifyDirectory(AppDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(DataDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(DataBackupDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(ScriptDirectory, createIfMissing: true, verifyWriteable: false);
                VerifyDirectory(DefaultDownloadsDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(DefaultIncompleteDirectory, createIfMissing: true, verifyWriteable: true);
            }
            catch (Exception ex)
            {
                Log.Information($"Filesystem exception: {ex.Message}");
                return;
            }

            // load and validate the configuration
            try
            {
                Configuration = new ConfigurationBuilder()
                    .AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile, reloadOnChange: !OptionsAtStartup.Flags.NoConfigWatch)
                    .Build();

                Configuration.GetSection(AppName)
                    .Bind(OptionsAtStartup, (o) => { o.BindNonPublicProperties = true; });

                // Log security configuration after binding
                Log.Information("[Config] After binding OptionsAtStartup.Security.Enabled = {Enabled}, Profile = {Profile}", 
                    OptionsAtStartup.Security?.Enabled ?? false, 
                    OptionsAtStartup.Security?.Profile.ToString() ?? "null");
                
                // Also check raw configuration sections
                var securitySection = Configuration.GetSection("security");
                var slskdSecuritySection = Configuration.GetSection("slskd:security");
                Log.Information("[Config] Raw config sections - security.Exists={SecurityExists}, slskd:security.Exists={SlskdSecurityExists}", 
                    securitySection.Exists(), 
                    slskdSecuritySection.Exists());
                if (securitySection.Exists())
                {
                    Log.Information("[Config] Raw security section enabled value: {Enabled}", securitySection["enabled"]);
                }
                if (slskdSecuritySection.Exists())
                {
                    Log.Information("[Config] Raw slskd:security section enabled value: {Enabled}", slskdSecuritySection["enabled"]);
                }

                if (OptionsAtStartup.Debug)
                {
                    Log.Information($"Configuration:\n{Configuration.GetDebugView()}");
                }

                if (!OptionsAtStartup.TryValidate(out var result))
                {
                    Log.Information(result.GetResultView());
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Invalid configuration: {(!OptionsAtStartup.Debug ? ex : ex.Message)}");
                return;
            }

            IsRelayAgent = OptionsAtStartup.Relay.Enabled && OptionsAtStartup.Relay.Mode.ToEnum<RelayMode>() == RelayMode.Agent;
            Flags = OptionsAtStartup.Flags;

            ConfigureGlobalLogger();
            Log = Serilog.Log.ForContext(typeof(Program));

            // Install hard telemetry to catch silent exits
            InstallShutdownTelemetry();

            if (!OptionsAtStartup.Flags.NoLogo)
            {
                PrintLogo(FullVersion);
            }

            Log.Information("Version: {Version}", FullVersion);

            if (IsDevelopment)
            {
                Log.Warning("This is a Development build; YMMV");
            }

            if (IsCanary)
            {
                Log.Warning("This is a canary build");
                Log.Warning("Canary builds are considered UNSTABLE and may be completely BROKEN");
                Log.Warning($"Please report any issues here: {IssuesUrl}");
            }

            Log.Information("System: .NET {DotNet}, {OS}, {BitNess} bit, {ProcessorCount} processors", Environment.Version, Environment.OSVersion, Environment.Is64BitOperatingSystem ? 64 : 32, Environment.ProcessorCount);
            Log.Information("Process ID: {ProcessId} ({BitNess} bit)", ProcessId, Environment.Is64BitProcess ? 64 : 32);

            Log.Information("Invocation ID: {InvocationId}", InvocationId);
            Log.Information("Instance Name: {InstanceName}", OptionsAtStartup.InstanceName);

            Log.Information("Configuring application...");

            // SQLite must have specific capabilities to function properly. this shouldn't be a concern for shrinkwrapped
            // binaries or in Docker, but if someone builds from source weird things can happen.
            InitSQLiteOrFailFast();

            Log.Information("Using application directory {AppDirectory}", AppDirectory);
            Log.Information("Using configuration file {ConfigurationFile}", ConfigurationFile);

            if (OptionsAtStartup.Flags.NoConfigWatch)
            {
                Log.Warning("Configuration watch DISABLED; all configuration changes will require a restart to take effect");
            }

            Log.Information("Storing application data in {DataDirectory}", DataDirectory);

            if (OptionsAtStartup.Logger.Disk)
            {
                Log.Information("Saving application logs to {LogDirectory}", LogDirectory);
            }

            RecreateConfigurationFileIfMissing(ConfigurationFile);

            if (!string.IsNullOrEmpty(OptionsAtStartup.Logger.Loki))
            {
                Log.Information("Forwarding logs to Grafana Loki instance at {LoggerLokiUrl}", OptionsAtStartup.Logger.Loki);
            }

            // bootstrap the ASP.NET application
            try
            {
                var isBindingNonLoopback = OptionsAtStartup.Web.Port > 0 ||
                    (!OptionsAtStartup.Web.Https.Disabled && OptionsAtStartup.Web.Https.Port > 0);
                Common.Security.HardeningValidator.Validate(
                    OptionsAtStartup,
                    System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
                    isBindingNonLoopback);

                var builder = WebApplication.CreateBuilder(args);

                builder.Configuration
                    .AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile, reloadOnChange: !OptionsAtStartup.Flags.NoConfigWatch);

                builder.Host
                    .UseSerilog();

                builder.WebHost
                    .UseUrls()
                    .UseKestrel(options =>
                    {
                        // PR-09: Global body size cap; configurable via Web.MaxRequestBodySize (default 10 MB). MeshGateway and others may enforce lower per-route.
                        options.Limits.MaxRequestBodySize = OptionsAtStartup.Web.MaxRequestBodySize;
                        Log.Information($"[Kestrel] Configuring HTTP listener at http://{IPAddress.Any}:{OptionsAtStartup.Web.Port}/");
                        options.Listen(IPAddress.Any, OptionsAtStartup.Web.Port);
                        Log.Information($"[Kestrel] HTTP listener configured");

                        if (OptionsAtStartup.Web.Socket != null)
                        {
                            Log.Information($"Configuring HTTP listener on unix domain socket (UDS) {OptionsAtStartup.Web.Socket}");
                            options.ListenUnixSocket(OptionsAtStartup.Web.Socket);
                        }

                        if (!OptionsAtStartup.Web.Https.Disabled)
                        {
                            Log.Information($"Configuring HTTPS listener at https://{IPAddress.Any}:{OptionsAtStartup.Web.Https.Port}/");
                            options.Listen(IPAddress.Any, OptionsAtStartup.Web.Https.Port, listenOptions =>
                            {
                                var cert = OptionsAtStartup.Web.Https.Certificate;

                                if (!string.IsNullOrEmpty(cert.Pfx))
                                {
                                    Log.Information($"Using certificate from {cert.Pfx}");
                                    listenOptions.UseHttps(cert.Pfx, cert.Password);
                                }
                                else
                                {
                                    Log.Information($"Using randomly generated self-signed certificate");
                                    listenOptions.UseHttps(X509.Generate(subject: AppName));
                                }
                            });
                        }
                    });

                Log.Information("[MAIN] About to configure ASP.NET services...");
                builder.Services
                    .ConfigureAspDotNetServices()
                    .ConfigureDependencyInjectionContainer();

                Log.Information("[MAIN] Services configured, building DI container...");
                WebApplication app;
                try
                {
                    Log.Information("Building DI container...");
                    Log.Information("[DI] About to call builder.Build() - this will construct all singleton services...");
                    app = builder.Build();
                    Log.Information("DI container built successfully!");
                }
                catch (Exception diEx)
                {
                    Log.Fatal(diEx, "FAILED to build DI container");
                    throw;
                }

                if (!OptionsAtStartup.Flags.Volatile)
                {
                    Log.Debug($"Running Migrate()...");

                    // note: if this ever throws, we've forgotten to register a Migrator following database DI config
                    app.Services.GetService<Migrator>().Migrate(force: OptionsAtStartup.Flags.ForceMigrations);
                }

                if (OptionsAtStartup.Flags.AudioReanalyze && !OptionsAtStartup.Flags.Volatile)
                {
                    Log.Information("[AudioReanalyze] Running analyzer migration (force={Force})...", OptionsAtStartup.Flags.AudioReanalyzeForce);
                    var migrationService = app.Services.GetRequiredService<IAnalyzerMigrationService>();
                    var n = migrationService.MigrateAsync("audioqa-1", OptionsAtStartup.Flags.AudioReanalyzeForce, default).GetAwaiter().GetResult();
                    Log.Information("[AudioReanalyze] Updated {Count} variants", n);
                }

                // hack: services that exist only to subscribe to the event bus are not referenced by anything else
                //       and are thus never instantiated.  force a reference here so they are created.
                Log.Information("[DI] Forcing construction of ScriptService and WebhookService...");
                _ = app.Services.GetService<ScriptService>();
                _ = app.Services.GetService<WebhookService>();
                Log.Information("[DI] ScriptService and WebhookService constructed");

                Log.Information("[DI] About to configure ASP.NET pipeline...");
                try
                {
                    app.ConfigureAspDotNetPipeline();
                    Log.Information("[DI] ASP.NET pipeline configured");
                }
                catch (Exception pipelineEx)
                {
                    Log.Error(pipelineEx, "[DI] EXCEPTION configuring ASP.NET pipeline: {Message}", pipelineEx.Message);
                    throw;
                }

                if (OptionsAtStartup.Flags.NoStart)
                {
                    Log.Information("Quitting because 'no-start' option is enabled");
                    return;
                }

                Log.Information("Configuration complete.  Starting application...");
                
                // Add lifecycle hook to log when host actually starts listening
                var lifetime = app.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();
                lifetime.ApplicationStarted.Register(() =>
                {
                    var addresses = app.Urls;
                    Log.Information("✓ Host started and bound to: {Addresses}", string.Join(", ", addresses));
                });
                
                lifetime.ApplicationStopping.Register(() =>
                {
                    Log.Information("Application is stopping...");
                });
                
                Log.Information("[Program] About to call app.Run()...");
                Log.Information("[Program] app.Run() will start the web server and all hosted services...");
                
                // Add lifecycle hooks to track startup progress
                var hostLifetime = app.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();
                
                // Log when web server starts listening (happens before hosted services StartAsync)
                hostLifetime.ApplicationStarted.Register(() =>
                {
                    Log.Information("[Program] ✓ ApplicationStarted event fired - all hosted services have completed StartAsync");
                    
                    // Start LAN discovery advertising if enabled
                    if (OptionsAtStartup.Feature.IdentityFriends)
                    {
                        try
                        {
                            var discovery = app.Services.GetService<Identity.ILanDiscoveryService>();
                            if (discovery != null)
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await discovery.StartAdvertisingAsync().ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning(ex, "[Program] Failed to start LAN discovery advertising");
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "[Program] Failed to initialize LAN discovery");
                        }
                    }
                });
                
                hostLifetime.ApplicationStopping.Register(() =>
                {
                    try
                    {
                        var discovery = app.Services.GetService<Identity.ILanDiscoveryService>();
                        discovery?.StopAdvertisingAsync().GetAwaiter().GetResult();
                    }
                    catch { }
                });
                
                // Try to detect if we're hanging during web server startup
                Log.Information("[Program] Calling app.Run() - this will block until shutdown...");
                Log.Information("[Program] If you see this but not 'Host started and bound', the web server is hanging");
                
                app.Run();
                Log.Information("[Program] app.Run() returned (this should not happen normally)");
            }
            catch (Common.Security.HardeningValidationException hex)
            {
                Log.Fatal(hex, "Hardening validation failed: {Message}", hex.Message);
                Exit(1);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Serilog.Log.CloseAndFlush();
            }
        }

        private static IServiceCollection ConfigureDependencyInjectionContainer(this IServiceCollection services)
        {
            Log.Information("[DI] Starting ConfigureDependencyInjectionContainer...");

            // add the instance of OptionsAtStartup to DI as they were at startup. use when Options might change, but
            // the values at startup are to be used (generally anything marked RequiresRestart).
            services.AddSingleton(OptionsAtStartup);

            // add IOptionsMonitor and IOptionsSnapshot to DI.
            // use when the current Options are to be used (generally anything not marked RequiresRestart)
            // the monitor should be used for services with Singleton lifetime, snapshots for everything else
            services.AddOptions<Options>()
                .Bind(Configuration.GetSection(AppName), o => { o.BindNonPublicProperties = true; })
                .Validate(options =>
                {
                    if (!options.TryValidate(out var result))
                    {
                        Log.Warning("Options (re)configuration rejected.");
                        Log.Warning(result.GetResultView());
                        return false;
                    }

                    return true;
                });

            // add IManagedState, IStateMutator, IStateMonitor, and IStateSnapshot state to DI.
            // the mutator should be used any time application state needs to be mutated (as the name implies)
            // as with options, the monitor should be used for services with Singleton lifetime, snapshots for everything else
            // IManagedState should be used where state is being mutated and accessed in the same context
            services.AddManagedState<State>();

            // add IHttpClientFactory
            // use through 'using var http = HttpClientFactory.CreateClient()' wherever HTTP calls will be made
            // this is important to prevent memory leaks
            services.AddHttpClient();

            // add a special HttpClientFactory to DI that disables SSL.  access it via:
            // 'using var http = HttpClientFactory.CreateClient(Constants.IgnoreCertificateErrors)'
            // thanks Microsoft, makes total sense and surely won't be easy to fuck up later!
            services.AddHttpClient(Constants.IgnoreCertificateErrors)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                });

            // PR-14: SSRF-safe key fetcher for ActivityPub HTTP Signature (timeout 3s, max 3 redirects)
            services.AddHttpClient<SocialFederation.IHttpSignatureKeyFetcher, SocialFederation.HttpSignatureKeyFetcher>(c => c.Timeout = TimeSpan.FromSeconds(3))
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { MaxAutomaticRedirections = 3 });

            // add a partially configured instance of SoulseekClient. the Application instance will
            // complete configuration at startup.
            services.AddSingleton<ISoulseekClient, SoulseekClient>(_ =>
                new SoulseekClient(options: new SoulseekClientOptions(
                    maximumConcurrentUploads: OptionsAtStartup.Global.Upload.Slots,
                    maximumConcurrentDownloads: OptionsAtStartup.Global.Download.Slots,
                    minimumDiagnosticLevel: OptionsAtStartup.Soulseek.DiagnosticLevel.ToEnum<Soulseek.Diagnostics.DiagnosticLevel>(),
                    maximumConcurrentSearches: 2,
                    raiseEventsAsynchronously: true)));

            // add the core application service to DI as well as a hosted service so that other services can
            // access instance methods
            services.AddSingleton<IApplication>(sp =>
            {
                Log.Information("[DI] Factory function called to construct Application singleton...");
                Log.Information("[DI] Resolving OptionsAtStartup...");
                var optionsAtStartup = sp.GetRequiredService<OptionsAtStartup>();
                Log.Information("[DI] Resolving IOptionsMonitor<Options>...");
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<Options>>();
                Log.Information("[DI] Resolving IManagedState<State>...");
                var state = sp.GetRequiredService<IManagedState<State>>();
                Log.Information("[DI] Resolving ISoulseekClient...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Information("[DI] Resolving FileService...");
                var fileService = sp.GetRequiredService<FileService>();
                Log.Information("[DI] Resolving ConnectionWatchdog...");
                var connectionWatchdog = sp.GetRequiredService<ConnectionWatchdog>();
                Log.Information("[DI] Resolving ITransferService...");
                var transferService = sp.GetRequiredService<ITransferService>();
                Log.Information("[DI] Resolving IBrowseTracker...");
                var browseTracker = sp.GetRequiredService<IBrowseTracker>();
                Log.Information("[DI] Resolving IRoomService...");
                var roomService = sp.GetRequiredService<IRoomService>();
                Log.Information("[DI] Resolving IUserService...");
                var userService = sp.GetRequiredService<IUserService>();
                Log.Information("[DI] Resolving IMessagingService...");
                var messagingService = sp.GetRequiredService<IMessagingService>();
                Log.Information("[DI] Resolving IShareService...");
                var shareService = sp.GetRequiredService<IShareService>();
                Log.Information("[DI] Resolving ISearchService...");
                var searchService = sp.GetRequiredService<ISearchService>();
                Log.Information("[DI] Resolving INotificationService...");
                var notificationService = sp.GetRequiredService<Integrations.Notifications.INotificationService>();
                Log.Information("[DI] Resolving IRelayService...");
                var relayService = sp.GetRequiredService<IRelayService>();
                Log.Information("[DI] Resolving IHubContext<ApplicationHub>...");
                var applicationHub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ApplicationHub>>();
                Log.Information("[DI] Resolving IHubContext<LogsHub>...");
                var logHub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<LogsHub>>();
                Log.Information("[DI] Resolving IHubContext<TransfersHub>...");
                var transfersHub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Transfers.API.TransfersHub>>();
                Log.Information("[DI] Resolving EventBus...");
                var eventBus = sp.GetRequiredService<Events.EventBus>();
                Log.Information("[DI] All dependencies resolved, constructing Application...");
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                var app = new Application(
                    optionsAtStartup, optionsMonitor, state, soulseekClient, fileService,
                    connectionWatchdog, transferService, browseTracker, roomService,
                    userService, messagingService, shareService, searchService,
                    notificationService, relayService, applicationHub, logHub, transfersHub,
                    eventBus, sp, scopeFactory);
                Log.Information("[DI] Application singleton constructed successfully");
                return app;
            });
            // Use a wrapper to avoid factory function blocking
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Constructing ApplicationHostedServiceWrapper hosted service...");
                Log.Information("[DI] About to resolve IApplication from DI...");
                var app = p.GetRequiredService<IApplication>();
                Log.Information("[DI] IApplication resolved successfully");
                Log.Information("[DI] About to create ApplicationHostedServiceWrapper instance...");
                var service = new ApplicationHostedServiceWrapper(app, p.GetService<Microsoft.Extensions.Logging.ILogger<ApplicationHostedServiceWrapper>>());
                Log.Information("[DI] ApplicationHostedServiceWrapper constructed");
                return service;
            });

            services.AddSingleton<IWaiter, Waiter>();
            services.AddSingleton<ConnectionWatchdog, ConnectionWatchdog>();

            // wire up all of the connection strings we'll use. this is somewhat annoying but necessary because of the
            // intersection of run-time options (volatile, non-volatile) and ORM/mappers in use (EF, Dapper)
            var connectionStringDictionary = new ConnectionStringDictionary(Database.List
                .Select(database =>
                {
                    var pooling = OptionsAtStartup.Flags.NoSqlitePooling ? "False" : "True"; // don't invert and ToString this it is confusing

                    var connStr = OptionsAtStartup.Flags.Volatile
                        ? $"Data Source=file:{database}?mode=memory;Pooling={pooling};"
                        : $"Data Source={Path.Combine(DataDirectory, $"{database}.db")};Pooling={pooling}";

                    return new KeyValuePair<Database, ConnectionString>(database, connStr);
                })
                .ToDictionary(x => x.Key, x => x.Value));

            services.AddDbContext<SearchDbContext>(connectionStringDictionary[Database.Search]);
            services.AddDbContext<TransfersDbContext>(connectionStringDictionary[Database.Transfers]);
            services.AddDbContext<MessagingDbContext>(connectionStringDictionary[Database.Messaging]);
            services.AddDbContext<EventsDbContext>(connectionStringDictionary[Database.Events]);

            services.AddSingleton<ConnectionStringDictionary>(connectionStringDictionary);

            if (!OptionsAtStartup.Flags.Volatile)
            {
                // we're working with non-volatile database files, so register a Migrator to be used later in the
                // bootup process. the presence of a Migrator instance in DI determines whether a migration is needed.
                // it's important that we keep this list of databases in sync with those used by the application; anything
                // not in this list will not be able to be migrated.
                services.AddSingleton<Migrator>(_ => new Migrator(databases: connectionStringDictionary));
            }

            services.AddSingleton<EventService>();
            services.AddSingleton<EventBus>();

            services.AddSingleton<PrometheusService>();
            services.AddSingleton<ReportsService>();
            services.AddSingleton<TelemetryService>();

            services.AddSingleton<ScriptService>();
            services.AddSingleton<WebhookService>();

            services.AddSingleton<IBrowseTracker, BrowseTracker>();
            services.AddSingleton<IRoomTracker, RoomTracker>(_ => new RoomTracker(messageLimit: 250));

            services.AddSingleton<IMessagingService, MessagingService>();
            services.AddSingleton<IConversationService>(sp =>
            {
                Log.Information("[DI] Constructing ConversationService...");
                Log.Information("[DI] Resolving ISoulseekClient for ConversationService...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Information("[DI] Resolving EventBus for ConversationService...");
                var eventBus = sp.GetRequiredService<Events.EventBus>();
                Log.Information("[DI] Resolving IDbContextFactory<MessagingDbContext> for ConversationService...");
                var contextFactory = sp.GetRequiredService<IDbContextFactory<Messaging.MessagingDbContext>>();
                Log.Information("[DI] Resolving IPodService for ConversationService...");
                var podService = sp.GetRequiredService<PodCore.IPodService>();
                Log.Information("[DI] All ConversationService dependencies resolved, creating instance...");
                var service = new Messaging.ConversationService(soulseekClient, eventBus, contextFactory, podService);
                Log.Information("[DI] ConversationService constructed");
                return service;
            });

            services.AddSingleton<IShareService>(sp =>
            {
                Log.Information("[DI] Constructing ShareService...");
                Log.Information("[DI] Resolving FileService for ShareService...");
                var fileService = sp.GetRequiredService<FileService>();
                Log.Information("[DI] Resolving IShareRepositoryFactory for ShareService...");
                var shareRepositoryFactory = sp.GetRequiredService<IShareRepositoryFactory>();
                Log.Information("[DI] Resolving IOptionsMonitor<Options> for ShareService...");
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<Options>>();
                Log.Information("[DI] Resolving IModerationProvider for ShareService...");
                var moderationProvider = sp.GetRequiredService<Common.Moderation.IModerationProvider>();
                Log.Information("[DI] Resolving IShareScanner for ShareService (optional)...");
                var scanner = sp.GetService<IShareScanner>();
                Log.Information("[DI] Resolving IContentPeerHintService for ShareService (optional)...");
                var contentPeerHintService = sp.GetService<Mesh.Dht.IContentPeerHintService>();
                Log.Information("[DI] All ShareService dependencies resolved, creating instance...");
                var service = new ShareService(
                    fileService, shareRepositoryFactory, optionsMonitor, moderationProvider, scanner, contentPeerHintService);
                Log.Information("[DI] ShareService constructed");
                return service;
            });
            services.AddTransient<IShareRepositoryFactory, SqliteShareRepositoryFactory>();

            services.AddSingleton<IContentLocator, ContentLocator>();
            services.AddSingleton<IMeshContentFetcher, MeshContentFetcher>();
            services.AddSingleton<IStreamSessionLimiter, StreamSessionLimiter>();
            services.AddSingleton<IShareTokenService, ShareTokenService>();

            services.AddSingleton<ISearchService, SearchService>();

            services.AddSingleton<IUserService, UserService>();

            services.AddSingleton<IRoomService, RoomService>();

            services.AddSingleton<IScheduledRateLimitService, ScheduledRateLimitService>();
            services.AddSingleton<IDownloadService>(sp =>
            {
                Log.Information("[DI] Constructing DownloadService...");
                var service = new DownloadService(
                    sp.GetRequiredService<IOptionsMonitor<Options>>(),
                    sp.GetRequiredService<ISoulseekClient>(),
                    sp.GetRequiredService<IDbContextFactory<TransfersDbContext>>(),
                    sp.GetRequiredService<FileService>(),
                    sp.GetRequiredService<IRelayService>(),
                    sp.GetRequiredService<IFTPService>(),
                    sp.GetRequiredService<EventBus>(),
                    sp.GetService<Transfers.MultiSource.Metrics.IPeerMetricsService>());
                Log.Information("[DI] DownloadService constructed");
                return service;
            });
            services.AddSingleton<IUploadService>(sp =>
            {
                Log.Information("[DI] Constructing UploadService...");
                Log.Information("[DI] Resolving FileService for UploadService...");
                var fileService = sp.GetRequiredService<FileService>();
                Log.Information("[DI] Resolving IUserService for UploadService...");
                var userService = sp.GetRequiredService<IUserService>();
                Log.Information("[DI] Resolving ISoulseekClient for UploadService...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Information("[DI] Resolving IOptionsMonitor<Options> for UploadService...");
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<Options>>();
                Log.Information("[DI] Resolving IShareService for UploadService...");
                var shareService = sp.GetRequiredService<IShareService>();
                Log.Information("[DI] Resolving IRelayService for UploadService...");
                var relayService = sp.GetRequiredService<IRelayService>();
                Log.Information("[DI] Resolving IDbContextFactory<TransfersDbContext> for UploadService...");
                var contextFactory = sp.GetRequiredService<IDbContextFactory<TransfersDbContext>>();
                Log.Information("[DI] Resolving EventBus for UploadService...");
                var eventBus = sp.GetRequiredService<EventBus>();
                Log.Information("[DI] Resolving IScheduledRateLimitService for UploadService (optional)...");
                var scheduledRateLimitService = sp.GetService<IScheduledRateLimitService>();
                Log.Information("[DI] All UploadService dependencies resolved, creating instance...");
                var service = new UploadService(
                    fileService, userService, soulseekClient, optionsMonitor,
                    shareService, relayService, contextFactory, eventBus, scheduledRateLimitService);
                Log.Information("[DI] UploadService constructed");
                return service;
            });
            services.AddSingleton<ITransferService>(sp =>
            {
                Log.Information("[DI] Constructing TransferService...");
                var service = new TransferService(
                    sp.GetRequiredService<IUploadService>(),
                    sp.GetRequiredService<IDownloadService>());
                Log.Information("[DI] TransferService constructed");
                return service;
            });
            services.AddSingleton<FileService>();
            services.AddSingleton<Transfers.AutoReplace.IAutoReplaceService, Transfers.AutoReplace.AutoReplaceService>();

            // Source ranking services (smart scoring + download history)
            var rankingDbPath = Path.Combine(Program.AppDirectory, "ranking.db");
            services.AddDbContextFactory<Transfers.Ranking.SourceRankingDbContext>(options =>
            {
                options.UseSqlite($"Data Source={rankingDbPath}");
            });

            // Ensure ranking database is created
            using (var rankingContext = new Transfers.Ranking.SourceRankingDbContext(
                new DbContextOptionsBuilder<Transfers.Ranking.SourceRankingDbContext>()
                    .UseSqlite($"Data Source={rankingDbPath}")
                    .Options))
            {
                rankingContext.Database.EnsureCreated();
            }

            services.AddSingleton<Transfers.Ranking.ISourceRankingService, Transfers.Ranking.SourceRankingService>();

            // Multi-source feature services
            // (IHashDbService, IMediaVariantStore, ICanonicalStatsService, IDedupeService, IAnalyzerMigrationService in AddAudioCore)
            services.AddSingleton<IArtistReleaseGraphService, ReleaseGraphService>();
            services.AddSingleton<IDiscographyProfileService, DiscographyProfileService>();
            services.AddSingleton<Jobs.IDiscographyJobService, Jobs.DiscographyJobService>();
            services.AddSingleton<Jobs.ILabelCrateJobService, Jobs.LabelCrateJobService>();
            services.AddSingleton<Signals.Swarm.ISwarmJobStore, Signals.Swarm.InMemorySwarmJobStore>();
            services.AddSingleton<Signals.Swarm.ISecurityPolicyEngine, Signals.Swarm.StubSecurityPolicyEngine>();
            services.AddSingleton<Signals.Swarm.IBitTorrentBackend, Signals.Swarm.MonoTorrentBitTorrentBackend>();
            services.AddSingleton<Transfers.MultiSource.Metrics.ITrafficAccountingService, Transfers.MultiSource.Metrics.TrafficAccountingService>();
            services.AddSingleton<Transfers.MultiSource.Metrics.IFairnessGuard>(sp =>
                new Transfers.MultiSource.Metrics.FairnessGuard(
                    sp.GetRequiredService<Transfers.MultiSource.Metrics.ITrafficAccountingService>()));
            services.AddSingleton<Jobs.Manifests.IJobManifestValidator, Jobs.Manifests.JobManifestValidator>();
            services.AddSingleton<Jobs.Manifests.IJobManifestService, Jobs.Manifests.JobManifestService>();
            services.AddSingleton<Transfers.MultiSource.Tracing.ISwarmEventStore, Transfers.MultiSource.Tracing.SwarmEventStore>();
            services.AddSingleton<Transfers.MultiSource.Tracing.ISwarmTraceSummarizer, Transfers.MultiSource.Tracing.SwarmTraceSummarizer>();
            services.AddSingleton<Transfers.MultiSource.Caching.IWarmCachePopularityService, Transfers.MultiSource.Caching.WarmCachePopularityService>();
            services.AddSingleton<Transfers.MultiSource.Caching.IWarmCacheService, Transfers.MultiSource.Caching.WarmCacheService>();

            // Add signal system
            services.AddSignalSystem();
            services.AddSingleton<Transfers.MultiSource.Playback.IPlaybackPriorityService, Transfers.MultiSource.Playback.PlaybackPriorityService>();
            services.AddSingleton<Transfers.MultiSource.Playback.IPlaybackFeedbackService, Transfers.MultiSource.Playback.PlaybackFeedbackService>();
            // (ILibraryHealthService, ILibraryHealthRemediationService in AddAudioCore)

            // Virtual Soulfind services
            services.AddSingleton<VirtualSoulfind.Capture.ITrafficObserver, VirtualSoulfind.Capture.TrafficObserver>();
            services.AddSingleton<VirtualSoulfind.Capture.INormalizationPipeline, VirtualSoulfind.Capture.NormalizationPipeline>();
            services.AddSingleton<VirtualSoulfind.Capture.IUsernamePseudonymizer, VirtualSoulfind.Capture.UsernamePseudonymizer>();
            services.AddSingleton<VirtualSoulfind.Capture.IObservationStore, VirtualSoulfind.Capture.InMemoryObservationStore>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShadowIndexBuilder, VirtualSoulfind.ShadowIndex.ShadowIndexBuilder>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IDhtClient, VirtualSoulfind.ShadowIndex.DhtClientStub>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShardPublisher, VirtualSoulfind.ShadowIndex.ShardPublisher>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShadowIndexQuery, VirtualSoulfind.ShadowIndex.ShadowIndexQuery>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShardMerger, VirtualSoulfind.ShadowIndex.ShardMerger>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShardCache, VirtualSoulfind.ShadowIndex.ShardCache>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IDhtRateLimiter, VirtualSoulfind.ShadowIndex.DhtRateLimiter>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneService, VirtualSoulfind.Scenes.SceneService>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneAnnouncementService, VirtualSoulfind.Scenes.SceneAnnouncementService>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneMembershipTracker, VirtualSoulfind.Scenes.SceneMembershipTracker>();
            services.AddSingleton<VirtualSoulfind.Scenes.IScenePubSubService, VirtualSoulfind.Scenes.ScenePubSubService>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneJobService, VirtualSoulfind.Scenes.SceneJobService>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneChatService, VirtualSoulfind.Scenes.SceneChatService>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneModerationService, VirtualSoulfind.Scenes.SceneModerationService>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.ISoulseekClient>(sp => 
                new VirtualSoulfind.DisasterMode.SoulseekClientWrapper(sp.GetRequiredService<Soulseek.ISoulseekClient>()));
            services.AddSingleton<VirtualSoulfind.DisasterMode.ISoulseekHealthMonitor, VirtualSoulfind.DisasterMode.SoulseekHealthMonitor>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.IDisasterModeCoordinator, VirtualSoulfind.DisasterMode.DisasterModeCoordinator>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.IMeshSearchService, VirtualSoulfind.DisasterMode.MeshSearchService>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.IMeshTransferService, VirtualSoulfind.DisasterMode.MeshTransferService>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.IScenePeerDiscovery, VirtualSoulfind.DisasterMode.ScenePeerDiscovery>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.IDisasterModeTelemetry, VirtualSoulfind.DisasterMode.DisasterModeTelemetryService>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.IGracefulDegradationService, VirtualSoulfind.DisasterMode.GracefulDegradationService>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.IDisasterModeRecovery, VirtualSoulfind.DisasterMode.DisasterModeRecovery>();
            services.AddSingleton<VirtualSoulfind.Integration.IShadowIndexJobIntegration, VirtualSoulfind.Integration.ShadowIndexJobIntegration>();
            services.AddSingleton<VirtualSoulfind.Integration.ISceneLabelCrateIntegration, VirtualSoulfind.Integration.SceneLabelCrateIntegration>();
            services.AddSingleton<VirtualSoulfind.Integration.IDisasterRescueIntegration, VirtualSoulfind.Integration.DisasterRescueIntegration>();
            services.AddSingleton<VirtualSoulfind.Integration.IPrivacyAudit, VirtualSoulfind.Integration.PrivacyAudit>();
            services.AddSingleton<VirtualSoulfind.Integration.IPerformanceOptimizer, VirtualSoulfind.Integration.PerformanceOptimizer>();
            services.AddSingleton<VirtualSoulfind.Integration.ITelemetryDashboard, VirtualSoulfind.Integration.TelemetryDashboardService>();
            services.AddSingleton<VirtualSoulfind.Bridge.ISoulfindBridgeService, VirtualSoulfind.Bridge.SoulfindBridgeService>();
            services.AddSingleton<VirtualSoulfind.Bridge.IBridgeApi, VirtualSoulfind.Bridge.BridgeApi>();
            services.AddSingleton<VirtualSoulfind.Bridge.IPeerIdAnonymizer, VirtualSoulfind.Bridge.PeerIdAnonymizer>();
            services.AddSingleton<VirtualSoulfind.Bridge.IFilenameGenerator, VirtualSoulfind.Bridge.FilenameGenerator>();
            services.AddSingleton<VirtualSoulfind.Bridge.IRoomSceneMapper, VirtualSoulfind.Bridge.RoomSceneMapper>();
            services.AddSingleton<VirtualSoulfind.Bridge.ITransferProgressProxy, VirtualSoulfind.Bridge.TransferProgressProxy>();
            services.AddSingleton<VirtualSoulfind.Bridge.IBridgeDashboard, VirtualSoulfind.Bridge.BridgeDashboard>();

            // VirtualSoulfind v2 Domain Providers (T-VC02, T-VC03) (IMusicContentDomainProvider in AddAudioCore)
            services.AddSingleton<VirtualSoulfind.Core.GenericFile.IGenericFileContentDomainProvider, VirtualSoulfind.Core.GenericFile.GenericFileContentDomainProvider>();

            // Peer Reputation System (T-MCP04)
            services.AddSingleton<Common.Moderation.IPeerReputationStore>(sp =>
            {
                var dataProtection = sp.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
                var protector = dataProtection.CreateProtector("PeerReputation");
                var storagePath = Path.Combine(Program.AppDirectory, "reputation", "peers.db");
                return new Common.Moderation.PeerReputationStore(
                    sp.GetRequiredService<ILogger<Common.Moderation.PeerReputationStore>>(),
                    protector,
                    storagePath);
            });
            services.AddSingleton<Common.Moderation.PeerReputationService>();

            // MediaCore (Phase 9)
            services.AddOptions<MediaCore.MediaCoreOptions>();
            services.AddSingleton<MediaCore.IDescriptorValidator, MediaCore.DescriptorValidator>();
            services.AddSingleton<MediaCore.IDescriptorPublisher>(sp =>
                new MediaCore.DescriptorPublisher(
                    sp.GetRequiredService<ILogger<MediaCore.DescriptorPublisher>>(),
                    sp.GetRequiredService<MediaCore.IDescriptorValidator>(),
                    sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>(),
                    sp.GetRequiredService<IOptions<MediaCore.MediaCoreOptions>>()));
            services.AddSingleton<MediaCore.IContentIdRegistry, MediaCore.ContentIdRegistry>();
            services.AddSingleton<MediaCore.IIpldMapper, MediaCore.IpldMapper>();
            services.AddSingleton<MediaCore.IPerceptualHasher, MediaCore.PerceptualHasher>();
            services.AddSingleton<MediaCore.IMetadataPortability, MediaCore.MetadataPortability>();
            services.AddSingleton<MediaCore.IContentDescriptorPublisher, MediaCore.ContentDescriptorPublisher>();
            services.AddSingleton<MediaCore.IDescriptorRetriever, MediaCore.DescriptorRetriever>();
            services.AddSingleton<MediaCore.IFuzzyMatcher, MediaCore.FuzzyMatcher>();
            services.AddSingleton<MediaCore.IMediaCoreStatsService, MediaCore.MediaCoreStatsService>();

            // PodCore services
            services.AddSingleton<PodCore.IPodDhtPublisher, PodCore.PodDhtPublisher>();
            services.AddSingleton<PodCore.IPodMembershipService, PodCore.PodMembershipService>();
            services.AddSingleton<PodCore.IPodMembershipVerifier, PodCore.PodMembershipVerifier>();
            services.AddSingleton<PodCore.IPodDiscoveryService, PodCore.PodDiscoveryService>();
            services.AddSingleton<PodCore.IPodJoinLeaveService, PodCore.PodJoinLeaveService>();
            services.AddSingleton<PodCore.IPodMessageRouter>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PodCore.PodMessageRouter>>();
                var podService = sp.GetRequiredService<PodCore.IPodService>();
                var overlayClient = sp.GetRequiredService<Mesh.Overlay.IOverlayClient>();
                var controlSigner = sp.GetRequiredService<Mesh.Overlay.IControlSigner>();
                var peerResolution = sp.GetRequiredService<PodCore.IPeerResolutionService>();
                var privacyLayer = sp.GetService<Mesh.Privacy.IPrivacyLayer>();
                return new PodCore.PodMessageRouter(logger, podService, overlayClient, controlSigner, peerResolution, privacyLayer);
            });
            services.AddSingleton<PodCore.IMessageSigner, PodCore.MessageSigner>();

            // MultiSource MediaCore integration
            services.AddSingleton<IMediaCoreSwarmIntelligence, MediaCoreSwarmIntelligence>();
            services.AddSingleton<IMediaCoreSwarmService, MediaCoreSwarmService>();
            services.AddSingleton<slskd.Transfers.MultiSource.Scheduling.IChunkScheduler, slskd.Transfers.MultiSource.Scheduling.MediaCoreChunkScheduler>();
            services.AddSingleton<MediaCore.IIpldMapper, MediaCore.IpldMapper>();
            services.AddSingleton<MediaCore.IFuzzyMatcher, MediaCore.FuzzyMatcher>();
            services.AddSingleton<MediaCore.IContentDescriptorSource, MediaCore.ShadowIndexDescriptorSource>();

            // PodCore (Phase 10 - SQLite persistence)
            var podDbPath = Path.Combine(Program.AppDirectory, "pods.db");
            services.AddDbContextFactory<PodCore.PodDbContext>(options =>
            {
                options.UseSqlite($"Data Source={podDbPath}");
            });

            // Ensure pod database is created with secure permissions and migrations
            using (var podContext = new PodCore.PodDbContext(
                new DbContextOptionsBuilder<PodCore.PodDbContext>()
                    .UseSqlite($"Data Source={podDbPath}")
                    .Options))
            {
                podContext.Database.EnsureCreated();
                
                // Apply schema migrations for existing databases (synchronous since we're in ConfigureServices)
                try
                {
                    var connection = podContext.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        connection.Open();
                    }
                    
                    // Check if AllowGuests column exists, if not add it
                    using var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = "PRAGMA table_info(Pods)";
                    using var reader = checkCmd.ExecuteReader();
                    var hasAllowGuests = false;
                    while (reader.Read())
                    {
                        if (reader.GetString(1) == "AllowGuests")
                        {
                            hasAllowGuests = true;
                            break;
                        }
                    }
                    reader.Close();
                    
                    if (!hasAllowGuests)
                    {
                        Log.Information("[PodDb] Adding missing AllowGuests column to Pods table");
                        using var alterCmd = connection.CreateCommand();
                        alterCmd.CommandText = "ALTER TABLE Pods ADD COLUMN AllowGuests INTEGER NOT NULL DEFAULT 0";
                        alterCmd.ExecuteNonQuery();
                        Log.Information("[PodDb] AllowGuests column added successfully");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[PodDb] Could not apply schema migration (database may be new or already up to date)");
                }
                
                // SECURITY: Set restrictive file permissions on the database (Unix/Linux only)
                if (System.IO.File.Exists(podDbPath))
                {
                    try
                    {
                        // Unix chmod 600 (owner read/write only) - requires Mono.Posix.NETStandard package
                        // For now, just log warning if on Windows (file permissions are more complex there)
                        if (!OperatingSystem.IsWindows())
                        {
                            Log.Information("Pod database created at {Path} - ensure file permissions are secure (chmod 600)", podDbPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Could not verify secure file permissions on pods.db");
                    }
                }
            }

            // Pod membership signer
            services.AddSingleton<PodCore.IPodMembershipSigner, PodCore.PodMembershipSigner>();

            // Pod DHT publishing + discovery
            services.AddSingleton<PodCore.IPodPublisher>(sp =>
            {
                Log.Information("[DI] Constructing PodPublisher...");
                Log.Information("[DI] Resolving IMeshDhtClient for PodPublisher...");
                var dht = sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>();
                Log.Information("[DI] Resolving IServiceScopeFactory for PodPublisher...");
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                Log.Information("[DI] Resolving ILogger<PodPublisher> for PodPublisher...");
                var logger = sp.GetRequiredService<ILogger<PodCore.PodPublisher>>();
                Log.Information("[DI] All PodPublisher dependencies resolved, creating instance...");
                var service = new PodCore.PodPublisher(dht, scopeFactory, logger);
                Log.Information("[DI] PodPublisher constructed");
                return service;
            });
            services.AddSingleton<PodCore.IPodDiscovery, PodCore.PodDiscovery>();

            // Peer resolution service (for PeerReputation lookup)
            services.AddSingleton<PodCore.IPeerResolutionService, PodCore.PeerResolutionService>();

            // Soulseek chat bridge
            services.AddSingleton<PodCore.ISoulseekChatBridge>(sp =>
            {
                Log.Information("[DI] Constructing SoulseekChatBridge...");
                Log.Information("[DI] Resolving IServiceScopeFactory for SoulseekChatBridge...");
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                Log.Information("[DI] Resolving IRoomService for SoulseekChatBridge...");
                var roomService = sp.GetRequiredService<IRoomService>();
                Log.Information("[DI] Resolving ISoulseekClient for SoulseekChatBridge...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Information("[DI] Resolving ILogger<SoulseekChatBridge> for SoulseekChatBridge...");
                var logger = sp.GetRequiredService<ILogger<PodCore.SoulseekChatBridge>>();
                Log.Information("[DI] All SoulseekChatBridge dependencies resolved, creating instance...");
                var service = new PodCore.SoulseekChatBridge(scopeFactory, roomService, soulseekClient, logger);
                Log.Information("[DI] SoulseekChatBridge constructed");
                return service;
            });

            // Main pod service (SQLite-backed with persistence)
            services.AddSingleton<PodCore.IPodService>(sp =>
            {
                Log.Information("[DI] Constructing SqlitePodService...");
                Log.Information("[DI] Resolving IDbContextFactory<PodDbContext> for SqlitePodService...");
                var factory = sp.GetRequiredService<IDbContextFactory<PodCore.PodDbContext>>();
                Log.Information("[DI] Resolving IPodPublisher for SqlitePodService (optional)...");
                var podPublisher = sp.GetService<PodCore.IPodPublisher>();
                Log.Information("[DI] Resolving IPodMembershipSigner for SqlitePodService (optional)...");
                var membershipSigner = sp.GetService<PodCore.IPodMembershipSigner>();
                Log.Information("[DI] Resolving ILogger<SqlitePodService> for SqlitePodService...");
                var logger = sp.GetRequiredService<ILogger<PodCore.SqlitePodService>>();
                Log.Information("[DI] Resolving IServiceScopeFactory for SqlitePodService (for lazy IContentLinkService resolution)...");
                var scopeFactory = sp.GetService<IServiceScopeFactory>();
                Log.Information("[DI] All SqlitePodService dependencies resolved, creating instance (IContentLinkService will be resolved lazily via scope)...");
                var service = new PodCore.SqlitePodService(factory, podPublisher, membershipSigner, logger, scopeFactory);
                Log.Information("[DI] SqlitePodService constructed");
                return service;
            });

            // Pod messaging service (SQLite-backed)
            services.AddScoped<PodCore.IPodMessaging>(sp =>
            {
                var factory = sp.GetRequiredService<IDbContextFactory<PodCore.PodDbContext>>();
                var dbContext = factory.CreateDbContext();
                return new PodCore.SqlitePodMessaging(
                    dbContext,
                    sp.GetRequiredService<ILogger<PodCore.SqlitePodMessaging>>());
            });

            // Pod message storage service with full-text search and retention policies
            services.AddScoped<PodCore.IPodMessageStorage>(sp =>
            {
                var factory = sp.GetRequiredService<IDbContextFactory<PodCore.PodDbContext>>();
                var dbContext = factory.CreateDbContext();
                return new PodCore.SqlitePodMessageStorage(
                    dbContext,
                    sp.GetRequiredService<ILogger<PodCore.SqlitePodMessageStorage>>());
            });

            // Content link service for pod content validation
            services.AddScoped<PodCore.IContentLinkService>(sp =>
            {
                var musicBrainzClient = sp.GetService<Integrations.MusicBrainz.IMusicBrainzClient>();
                return new PodCore.ContentLinkService(
                    musicBrainzClient,
                    sp.GetRequiredService<ILogger<PodCore.ContentLinkService>>());
            });

            // Pod message backfill service for synchronizing missed messages
            services.AddScoped<PodCore.IPodMessageBackfill>(sp =>
            {
                var messageStorage = sp.GetRequiredService<PodCore.IPodMessageStorage>();
                var messageRouter = sp.GetRequiredService<PodCore.IPodMessageRouter>();
                var overlayClient = sp.GetRequiredService<Mesh.Overlay.IOverlayClient>();
                return new PodCore.PodMessageBackfill(
                    messageStorage,
                    messageRouter,
                    overlayClient,
                    sp.GetRequiredService<ILogger<PodCore.PodMessageBackfill>>());
            });

            // Pod opinion service for managing content variant opinions
            services.AddScoped<PodCore.IPodOpinionService>(sp =>
            {
                var podService = sp.GetRequiredService<PodCore.IPodService>();
                var dhtClient = sp.GetService<Mesh.Dht.IMeshDhtClient>();
                var messageSigner = sp.GetRequiredService<PodCore.IMessageSigner>();
                return new PodCore.PodOpinionService(
                    podService,
                    dhtClient,
                    messageSigner,
                    sp.GetRequiredService<ILogger<PodCore.PodOpinionService>>());
            });

            // Pod opinion aggregator for weighted opinion analysis and consensus
            services.AddScoped<PodCore.IPodOpinionAggregator>(sp =>
            {
                var podService = sp.GetRequiredService<PodCore.IPodService>();
                var opinionService = sp.GetRequiredService<PodCore.IPodOpinionService>();
                var messageStorage = sp.GetRequiredService<PodCore.IPodMessageStorage>();
                return new PodCore.PodOpinionAggregator(
                    podService,
                    opinionService,
                    messageStorage,
                    sp.GetRequiredService<ILogger<PodCore.PodOpinionAggregator>>());
            });

            // Background service for periodic pod metadata refresh
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Constructing PodPublisherBackgroundService hosted service...");
                var service = ActivatorUtilities.CreateInstance<PodCore.PodPublisherBackgroundService>(p);
                Log.Information("[DI] PodPublisherBackgroundService constructed");
                return service;
            });

            // Typed options (Phase 11)
            services.AddOptions<Core.SwarmOptions>().Bind(Configuration.GetSection("Swarm"));
            services.AddOptions<Core.SecurityOptions>().Bind(Configuration.GetSection("Security"));
            services.AddOptions<Common.Security.AdversarialOptions>().Bind(Configuration.GetSection("Security:Adversarial"));
            services.AddOptions<PodCore.PodMessageSignerOptions>().Bind(Configuration.GetSection("PodCore:Security"));
            services.AddOptions<PodCore.PodJoinOptions>().Bind(Configuration.GetSection("PodCore:Join"));

            // Transport policy manager for per-peer/per-pod transport policies
            services.AddSingleton<Mesh.Transport.TransportPolicyManager>();

            // Anonymity transport selector with policy-aware selection
            services.AddSingleton<Common.Security.IAnonymityTransportSelector>(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Common.Security.AdversarialOptions>>();
                var policyManager = sp.GetRequiredService<Mesh.Transport.TransportPolicyManager>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Common.Security.AnonymityTransportSelector>>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var overlayDataPlane = sp.GetService<Mesh.Overlay.IOverlayDataPlane>();
                return new Common.Security.AnonymityTransportSelector(options.Value, policyManager, logger, loggerFactory, overlayDataPlane);
            });

            // Privacy layer for traffic analysis protection
            services.AddSingleton<Mesh.Privacy.IPrivacyLayer>(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Common.Security.AdversarialOptions>>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Privacy.PrivacyLayer>>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new Mesh.Privacy.PrivacyLayer(logger, loggerFactory, options.Value.Privacy);
            });

            services.AddOptions<Core.BrainzOptions>().Bind(Configuration.GetSection("Brainz"));
            services.AddOptions<Mesh.MeshOptions>().Bind(Configuration.GetSection("Mesh")); // transport prefs
            services.AddOptions<Mesh.MeshSyncSecurityOptions>().Bind(Configuration.GetSection("Mesh:SyncSecurity"));
            services.AddOptions<Mesh.MeshTransportOptions>().Bind(Configuration.GetSection("Mesh:Transport"));
            services.AddOptions<Mesh.TorTransportOptions>().Bind(Configuration.GetSection("Mesh:Transport:Tor"));
            services.AddOptions<Mesh.I2PTransportOptions>().Bind(Configuration.GetSection("Mesh:Transport:I2P"));
            services.AddOptions<Common.Security.WebSocketTransportOptions>().Bind(Configuration.GetSection("Security:Adversarial:Transport:WebSocket"));
            services.AddOptions<Common.Security.HttpTunnelTransportOptions>().Bind(Configuration.GetSection("Security:Adversarial:Transport:HttpTunnel"));
            services.AddOptions<Common.Security.Obfs4TransportOptions>().Bind(Configuration.GetSection("Security:Adversarial:Transport:Obfs4"));
            services.AddOptions<Common.Security.MeekTransportOptions>().Bind(Configuration.GetSection("Security:Adversarial:Transport:Meek"));

            // Register options as singletons for direct injection (temporary workaround)
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Mesh.TorTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Mesh.I2PTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.WebSocketTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.HttpTunnelTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.Obfs4TransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.MeekTransportOptions>>().Value);
            services.AddOptions<MediaCore.MediaCoreOptions>().Bind(Configuration.GetSection("MediaCore"));
            services.AddOptions<Mesh.Overlay.OverlayOptions>().Bind(Configuration.GetSection("Overlay"));
            services.AddOptions<Mesh.ServiceFabric.MeshGatewayOptions>().Bind(Configuration.GetSection("MeshGateway"));

            // Realm services (T-REALM-01, T-REALM-02, T-REALM-04)
            Log.Information("[DI] Configuring Realm services...");
            services.Configure<Mesh.Realm.RealmConfig>(Configuration.GetSection("Realm"));
            services.Configure<Mesh.Realm.MultiRealmConfig>(Configuration.GetSection("MultiRealm"));
            services.AddRealmServices();

            // Social federation services (required by bridges)
            Log.Information("[DI] Configuring Social Federation services...");
            services.AddSocialFederation();
            services.AddBridgeServices();

            // Governance and Gossip services (T-REALM-03)
            Log.Information("[DI] Configuring Governance and Gossip services...");
            services.AddGovernanceServices();
            services.AddGossipServices();

            // MeshCore (Phase 8 implementation)
            Log.Information("[DI] Configuring MeshCore services...");
            services.Configure<Mesh.MeshOptions>(Configuration.GetSection("Mesh"));
            services.AddSingleton<Mesh.INatDetector, Mesh.StunNatDetector>();
            services.AddSingleton<Mesh.Nat.IUdpHolePuncher, Mesh.Nat.UdpHolePuncher>();
            services.AddSingleton<Mesh.Nat.IRelayClient, Mesh.Nat.RelayClient>();
            services.AddSingleton<Mesh.Nat.INatTraversalService, Mesh.Nat.NatTraversalService>();
            // DHT: use in-memory Kademlia-style implementation for now
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IDhtClient>(sp =>
            {
                Log.Information("[DI] Constructing InMemoryDhtClient...");
                Log.Information("[DI] Resolving ILogger<InMemoryDhtClient>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.InMemoryDhtClient>>();
                Log.Information("[DI] Resolving IOptions<MeshOptions> for InMemoryDhtClient...");
                var options = sp.GetRequiredService<IOptions<Mesh.MeshOptions>>();
                Log.Information("[DI] Resolving MeshStatsCollector for InMemoryDhtClient (optional)...");
                var statsCollector = sp.GetRequiredService<Mesh.MeshStatsCollector>();
                Log.Information("[DI] All InMemoryDhtClient dependencies resolved, creating instance...");
                var service = new Mesh.Dht.InMemoryDhtClient(logger, options, statsCollector);
                Log.Information("[DI] InMemoryDhtClient constructed");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IMeshDhtClient>(sp =>
            {
                Log.Information("[DI] Constructing MeshDhtClient...");
                Log.Information("[DI] Resolving ILogger<MeshDhtClient>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.MeshDhtClient>>();
                Log.Information("[DI] Resolving IDhtClient for MeshDhtClient...");
                var dhtClient = sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>();
                Log.Information("[DI] All MeshDhtClient dependencies resolved, creating instance (DhtService will be resolved lazily to break circular dependency)...");
                var service = new Mesh.Dht.MeshDhtClient(logger, dhtClient, sp, sp.GetService<IOptions<Mesh.MeshOptions>>());
                Log.Information("[DI] MeshDhtClient constructed");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IPeerDescriptorPublisher>(sp =>
            {
                Log.Information("[DI] Constructing PeerDescriptorPublisher...");
                var service = new Mesh.Dht.PeerDescriptorPublisher(
                    sp.GetRequiredService<ILogger<Mesh.Dht.PeerDescriptorPublisher>>(),
                    sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>(),
                    sp.GetRequiredService<IOptions<Mesh.MeshOptions>>(),
                    sp.GetRequiredService<Mesh.INatDetector>(),
                    sp.GetRequiredService<IOptions<Mesh.MeshTransportOptions>>(),
                    sp.GetRequiredService<Mesh.Transport.DescriptorSigningService>(),
                    sp.GetService<Mesh.Overlay.IKeyStore>());
                Log.Information("[DI] PeerDescriptorPublisher constructed");
                return service;
            });
            services.AddSingleton<Mesh.IMeshDirectory, Mesh.Dht.ContentDirectory>();
            services.AddSingleton<Mesh.IMeshAdvanced>(sp => new Mesh.MeshAdvanced(
                sp.GetRequiredService<ILogger<Mesh.MeshAdvanced>>(),
                sp.GetRequiredService<Mesh.IMeshDirectory>(),
                sp.GetRequiredService<Mesh.MeshStatsCollector>(),
                sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>(),
                sp.GetRequiredService<Mesh.Nat.INatTraversalService>()));
            services.AddSingleton<Mesh.MeshStatsCollector>(sp =>
            {
                Log.Information("[DI] Constructing MeshStatsCollector...");
                var service = new Mesh.MeshStatsCollector(
                    sp.GetRequiredService<ILogger<Mesh.MeshStatsCollector>>(),
                    sp);
                Log.Information("[DI] MeshStatsCollector constructed");
                return service;
            });
            services.AddSingleton<Mesh.IMeshStatsCollector>(sp => sp.GetRequiredService<Mesh.MeshStatsCollector>());
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Resolving MeshBootstrapService hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Bootstrap.MeshBootstrapService>(p);
                Log.Information("[DI] MeshBootstrapService hosted service resolved");
                return service;
            });
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Resolving PeerDescriptorRefreshService hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Dht.PeerDescriptorRefreshService>(p);
                Log.Information("[DI] PeerDescriptorRefreshService hosted service resolved");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IContentPeerPublisher>(sp =>
            {
                Log.Information("[DI] Constructing ContentPeerPublisher...");
                Log.Information("[DI] Resolving ILogger<ContentPeerPublisher>...");
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Dht.ContentPeerPublisher>>();
                Log.Information("[DI] Resolving IMeshDhtClient for ContentPeerPublisher...");
                var dht = sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>();
                Log.Information("[DI] Resolving IOptions<MeshOptions> for ContentPeerPublisher...");
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.MeshOptions>>();
                Log.Information("[DI] All ContentPeerPublisher dependencies resolved, creating instance...");
                var service = new Mesh.Dht.ContentPeerPublisher(logger, dht, options);
                Log.Information("[DI] ContentPeerPublisher constructed");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IContentPeerHintService>(sp =>
            {
                Log.Information("[DI] Constructing ContentPeerHintService...");
                var service = new Mesh.Dht.ContentPeerHintService(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Dht.ContentPeerHintService>>(),
                    sp.GetRequiredService<Mesh.Dht.IContentPeerPublisher>());
                Log.Information("[DI] ContentPeerHintService constructed");
                return service;
            });
            services.AddHostedService(sp => (Mesh.Dht.ContentPeerHintService)sp.GetRequiredService<Mesh.Dht.IContentPeerHintService>());
            services.AddSingleton<Mesh.Health.IMeshHealthService, Mesh.Health.MeshHealthService>();

            // Service Fabric (client + directory + validation)
            services.AddSingleton<Mesh.ServiceFabric.IMeshServiceDescriptorValidator, Mesh.ServiceFabric.MeshServiceDescriptorValidator>();
            services.AddSingleton<Mesh.ServiceFabric.IMeshServiceDirectory, Mesh.ServiceFabric.DhtMeshServiceDirectory>();
            services.AddSingleton<Mesh.ServiceFabric.IMeshServiceClient, Mesh.ServiceFabric.MeshServiceClient>();

            // Kademlia routing table using overlay key material for node ID
            services.AddSingleton<Mesh.Dht.KademliaRoutingTable>(sp =>
            {
                var keyStore = sp.GetRequiredService<Mesh.Overlay.IKeyStore>();
                var pubKey = keyStore.Current.PublicKey;

                // KademliaRoutingTable expects 160-bit IDs (20 bytes). SHA1 gives exactly 20 bytes.
                var selfId = System.Security.Cryptography.SHA1.HashData(pubKey);

                return new Mesh.Dht.KademliaRoutingTable(selfId);
            });

            // DHT services for Kademlia operations
            services.AddSingleton<Mesh.Dht.KademliaRpcClient>(sp =>
            {
                Log.Information("[DI] Constructing KademliaRpcClient...");
                Log.Information("[DI] Resolving ILogger<KademliaRpcClient>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.KademliaRpcClient>>();
                Log.Information("[DI] Resolving IMeshServiceClient for KademliaRpcClient...");
                var meshClient = sp.GetRequiredService<Mesh.ServiceFabric.IMeshServiceClient>();
                Log.Information("[DI] Resolving KademliaRoutingTable for KademliaRpcClient...");
                var routingTable = sp.GetRequiredService<Mesh.Dht.KademliaRoutingTable>();
                Log.Information("[DI] Resolving IDhtClient for KademliaRpcClient...");
                var dhtClient = sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>();
                Log.Information("[DI] All KademliaRpcClient dependencies resolved, creating instance...");
                var service = new Mesh.Dht.KademliaRpcClient(logger, meshClient, routingTable, dhtClient);
                Log.Information("[DI] KademliaRpcClient constructed");
                return service;
            });
            services.AddSingleton<Mesh.ServiceFabric.Services.DhtMeshService>();
            services.AddSingleton<Mesh.Dht.DhtService>(sp =>
            {
                Log.Information("[DI] Constructing DhtService...");
                Log.Information("[DI] Resolving ILogger<DhtService>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.DhtService>>();
                Log.Information("[DI] Resolving KademliaRoutingTable for DhtService...");
                var routingTable = sp.GetRequiredService<Mesh.Dht.KademliaRoutingTable>();
                Log.Information("[DI] Resolving IDhtClient for DhtService...");
                var dhtClient = sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>();
                Log.Information("[DI] Resolving KademliaRpcClient for DhtService...");
                var rpcClient = sp.GetRequiredService<Mesh.Dht.KademliaRpcClient>();
                Log.Information("[DI] Resolving IMeshMessageSigner for DhtService...");
                var messageSigner = sp.GetRequiredService<Mesh.IMeshMessageSigner>();
                Log.Information("[DI] All DhtService dependencies resolved, creating instance...");
                var service = new Mesh.Dht.DhtService(logger, routingTable, dhtClient, rpcClient, messageSigner);
                Log.Information("[DI] DhtService constructed");
                return service;
            });

            // Hole punching services for NAT traversal
            services.AddSingleton<Mesh.ServiceFabric.Services.HolePunchMeshService>();
            services.AddSingleton<Mesh.ServiceFabric.Services.MeshContentMeshService>();
            services.AddSingleton<Mesh.Nat.IHolePunchCoordinator, Mesh.Nat.HolePunchCoordinator>();
            services.AddSingleton<Mesh.Nat.INatTraversalService, Mesh.Nat.NatTraversalService>();

            // Private gateway service for VPN functionality (Phase 14)
            services.AddSingleton<DnsSecurityService>();
            services.AddSingleton<LocalPortForwarder>();
            services.AddSingleton<Mesh.ServiceFabric.Services.PrivateGatewayMeshService>();

            // Onion routing services (Phase 12)
            services.AddSingleton<Mesh.IMeshPeerManager, Mesh.MeshPeerManager>();
            services.AddSingleton<Mesh.IMeshTransportService>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.MeshTransportService>>();
                var meshOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.MeshOptions>>();
                var anonymitySelector = sp.GetService<Common.Security.IAnonymityTransportSelector>();
                var adversarialOptions = sp.GetService<Microsoft.Extensions.Options.IOptions<Common.Security.AdversarialOptions>>();
                return new Mesh.MeshTransportService(logger, meshOptions, anonymitySelector, adversarialOptions);
            });

            services.AddSingleton<Mesh.MeshCircuitBuilder>(sp =>
            {
                var meshOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.MeshOptions>>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.MeshCircuitBuilder>>();
                var peerManager = sp.GetRequiredService<Mesh.IMeshPeerManager>();
                var transportSelector = sp.GetRequiredService<Common.Security.IAnonymityTransportSelector>();
                return new Mesh.MeshCircuitBuilder(meshOptions.Value, logger, peerManager, transportSelector);
            });
            services.AddSingleton<Mesh.IMeshCircuitBuilder>(sp => sp.GetRequiredService<Mesh.MeshCircuitBuilder>());
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Constructing CircuitMaintenanceService hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.CircuitMaintenanceService>(p);
                Log.Information("[DI] CircuitMaintenanceService constructed");
                return service;
            });

            // Transport dialers (Tor/I2P integration Phase 2)
            services.AddSingleton<Mesh.Transport.ITransportDialer, Mesh.Transport.DirectQuicDialer>();
            services.AddSingleton<Mesh.Transport.ITransportDialer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<Mesh.TorTransportOptions>>();
                var logger = sp.GetRequiredService<ILogger<Mesh.Transport.TorSocksDialer>>();
                return new Mesh.Transport.TorSocksDialer(options.Value, logger);
            });
            services.AddSingleton<Mesh.Transport.ITransportDialer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<Mesh.I2PTransportOptions>>();
                var logger = sp.GetRequiredService<ILogger<Mesh.Transport.I2pSocksDialer>>();
                return new Mesh.Transport.I2pSocksDialer(options.Value, logger);
            });

            // Transport policy manager for per-peer/per-pod policies
            services.AddSingleton<Mesh.Transport.TransportPolicyManager>();

            // Transport downgrade protection
            services.AddSingleton<Mesh.Transport.TransportDowngradeProtector>();

            // Certificate pin management for peer identity verification
            services.AddSingleton<Mesh.Transport.CertificatePinManager>();

            // Rate limiting for DoS protection
            services.AddSingleton<Mesh.Transport.RateLimiter>();
            services.AddSingleton<Mesh.Transport.ConnectionThrottler>();
            services.AddSingleton<Mesh.Dht.DhtRateLimiter>();

            // DNS leak prevention verification
            services.AddSingleton<Mesh.Transport.DnsLeakPreventionVerifier>();

            // Transport selector for endpoint negotiation
            services.AddSingleton<Mesh.Transport.TransportSelector>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<Mesh.MeshTransportOptions>>();
                var dialers = sp.GetServices<Mesh.Transport.ITransportDialer>();
                var policyManager = sp.GetRequiredService<Mesh.Transport.TransportPolicyManager>();
                var downgradeProtector = sp.GetRequiredService<Mesh.Transport.TransportDowngradeProtector>();
                var connectionThrottler = sp.GetRequiredService<Mesh.Transport.ConnectionThrottler>();
                var logger = sp.GetRequiredService<ILogger<Mesh.Transport.TransportSelector>>();
                return new Mesh.Transport.TransportSelector(
                    options.Value,
                    dialers,
                    policyManager,
                    downgradeProtector,
                    connectionThrottler,
                    logger);
            });

            // Descriptor signing service for cryptographic integrity
            services.AddSingleton<Mesh.Transport.DescriptorSigningService>();

            // Ed25519 signing implementation
            services.AddSingleton<Mesh.Transport.Ed25519Signer>();

            // Control envelope validator for replay protection and peer-bound verification
            services.AddSingleton<Mesh.Overlay.ControlEnvelopeValidator>();

            // KeyStore for Ed25519 signing (used by ControlSigner and MeshMessageSigner)
            services.AddSingleton<Mesh.Overlay.IKeyStore, Mesh.Overlay.FileKeyStore>();
            services.AddSingleton<Mesh.Overlay.IControlSigner, Mesh.Overlay.ControlSigner>();
            services.AddSingleton<Mesh.Overlay.IControlDispatcher>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Overlay.ControlDispatcher>>();
                var validator = sp.GetRequiredService<Mesh.Overlay.ControlEnvelopeValidator>();
                var privacyLayer = sp.GetService<Mesh.Privacy.IPrivacyLayer>();
                return new Mesh.Overlay.ControlDispatcher(logger, validator, privacyLayer);
            });
            // Mesh message signing for mesh sync security
            services.AddSingleton<Mesh.IMeshMessageSigner, Mesh.MeshMessageSigner>();
            services.AddSingleton(sp =>
            {
                var keyStore = sp.GetRequiredService<Mesh.Overlay.IKeyStore>();
                return keyStore.Current;
            });
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Constructing UdpOverlayServer hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Overlay.UdpOverlayServer>(p);
                Log.Information("[DI] UdpOverlayServer constructed");
                return service;
            });
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Constructing QuicOverlayServer hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Overlay.QuicOverlayServer>(p);
                Log.Information("[DI] QuicOverlayServer constructed");
                return service;
            });
            services.AddSingleton<Mesh.Overlay.IOverlayClient>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Overlay.QuicOverlayClient>>();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.Overlay.OverlayOptions>>();
                var signer = sp.GetRequiredService<Mesh.Overlay.IControlSigner>();
                var privacyLayer = sp.GetService<Mesh.Privacy.IPrivacyLayer>();
                return new Mesh.Overlay.QuicOverlayClient(logger, options, signer, privacyLayer);
            });
            services.AddOptions<Mesh.Overlay.DataOverlayOptions>().Bind(Configuration.GetSection("OverlayData"));
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Constructing QuicDataServer hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Overlay.QuicDataServer>(p);
                Log.Information("[DI] QuicDataServer constructed");
                return service;
            });
            services.AddSingleton<Mesh.Overlay.IOverlayDataPlane, Mesh.Overlay.QuicDataClient>();

            // MediaCore publisher
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Constructing ContentPublisherService hosted service...");
                var service = ActivatorUtilities.CreateInstance<MediaCore.ContentPublisherService>(p);
                Log.Information("[DI] ContentPublisherService constructed");
                return service;
            });

            // Capabilities - tracks available features per peer
            services.AddSingleton<Capabilities.ICapabilityService, Capabilities.CapabilityService>();

            // DhtRendezvous services (BitTorrent DHT peer discovery)
            services.AddSingleton(OptionsAtStartup.DhtRendezvous);
            services.AddSingleton<CertificateManager>(sp => new CertificateManager(sp.GetRequiredService<ILogger<CertificateManager>>(), AppDirectory));
            services.AddSingleton<CertificatePinStore>(sp => new CertificatePinStore(sp.GetRequiredService<ILogger<CertificatePinStore>>(), AppDirectory));
            services.AddSingleton<OverlayRateLimiter>();
            services.AddSingleton<OverlayBlocklist>();
            services.AddSingleton<MeshNeighborRegistry>();
            services.AddSingleton<DhtRendezvous.Search.IMeshSearchRpcHandler, DhtRendezvous.Search.MeshSearchRpcHandler>();
            services.AddSingleton<DhtRendezvous.Search.IMeshOverlaySearchService, DhtRendezvous.Search.MeshOverlaySearchService>();
            services.AddSingleton<IMeshOverlayServer, MeshOverlayServer>();
            services.AddSingleton<IMeshOverlayConnector, MeshOverlayConnector>();

            services.AddSingleton<IDhtRendezvousService, DhtRendezvousService>();
            services.AddHostedService(p =>
            {
                Log.Information("[DI] Resolving DhtRendezvousService hosted service...");
                var service = (DhtRendezvousService)p.GetRequiredService<IDhtRendezvousService>();
                Log.Information("[DI] DhtRendezvousService hosted service resolved");
                return service;
            });

            // Backfill services (Long-tail content discovery)
            services.AddSingleton<Backfill.IBackfillSchedulerService, Backfill.BackfillSchedulerService>();
            services.AddHostedService(p => (Backfill.BackfillSchedulerService)p.GetRequiredService<Backfill.IBackfillSchedulerService>());

            // Mesh services (Hash database synchronization)
            services.AddSingleton<Mesh.IFlacKeyToPathResolver, Mesh.ShareBasedFlacKeyToPathResolver>();
            services.AddSingleton<Mesh.IProofOfPossessionService, Mesh.ProofOfPossessionService>();
            services.AddSingleton<Mesh.IMeshSyncService, Mesh.MeshSyncService>();

            // Multi-source download services (Swarm)
            services.AddSingleton<ISourceDiscoveryService>(sp => new SourceDiscoveryService(
                Program.AppDirectory,
                sp.GetRequiredService<ISoulseekClient>(),
                sp.GetRequiredService<Transfers.MultiSource.IContentVerificationService>()));
            services.AddSingleton<Transfers.MultiSource.IMultiSourceDownloadService, Transfers.MultiSource.MultiSourceDownloadService>();
            services.AddSingleton<IRescueGuardrailService, RescueGuardrailService>();
            services.AddSingleton<IRescueService>(sp => new RescueService(
                sp.GetService<HashDb.IHashDbService>(),
                sp.GetService<IFingerprintExtractionService>(),
                sp.GetService<IAcoustIdClient>(),
                sp.GetService<Mesh.IMeshSyncService>(),
                sp.GetService<Mesh.IMeshDirectory>(),
                sp.GetService<Transfers.MultiSource.IMultiSourceDownloadService>(),
                sp.GetRequiredService<IDownloadService>(),
                sp.GetService<IRescueGuardrailService>()));
            services.AddHostedService<UnderperformanceDetectorHostedService>();
            services.AddSingleton<Transfers.MultiSource.IContentVerificationService, Transfers.MultiSource.ContentVerificationService>();
            services.AddSingleton<Transfers.MultiSource.Metrics.IPeerMetricsService, Transfers.MultiSource.Metrics.PeerMetricsService>();
            services.AddSingleton<Transfers.MultiSource.Scheduling.IChunkScheduler>(sp =>
            {
                // TODO: Get enableCostBasedScheduling from configuration (Options.Transfers.CostBasedScheduling)
                // For now, default to enabled
                bool enableCostBasedScheduling = true;
                return new Transfers.MultiSource.Scheduling.ChunkScheduler(
                    sp.GetRequiredService<Transfers.MultiSource.Metrics.IPeerMetricsService>(),
                    enableCostBasedScheduling: enableCostBasedScheduling);
            });

            // Wishlist services
            var wishlistDbPath = Path.Combine(Program.AppDirectory, "wishlist.db");
            services.AddDbContextFactory<Wishlist.WishlistDbContext>(options =>
            {
                options.UseSqlite($"Data Source={wishlistDbPath}");
            });

            // Ensure wishlist database is created
            using (var wishlistContext = new Wishlist.WishlistDbContext(
                new DbContextOptionsBuilder<Wishlist.WishlistDbContext>()
                    .UseSqlite($"Data Source={wishlistDbPath}")
                    .Options))
            {
                wishlistContext.Database.EnsureCreated();
            }

            services.AddSingleton<Wishlist.IWishlistService, Wishlist.WishlistService>();
            services.AddHostedService(provider => (Wishlist.WishlistService)provider.GetRequiredService<Wishlist.IWishlistService>());

            // Auto-replace services
            services.AddSingleton<Transfers.AutoReplace.IAutoReplaceService, Transfers.AutoReplace.AutoReplaceService>();
            services.AddSingleton<Transfers.AutoReplace.AutoReplaceBackgroundService>();
            services.AddHostedService(provider => provider.GetRequiredService<Transfers.AutoReplace.AutoReplaceBackgroundService>());

            services.AddSingleton<IRelayService, RelayService>();

            services.AddSingleton<IFTPClientFactory, FTPClientFactory>();
            services.AddSingleton<IFTPService, FTPService>();

            // AudioCore: IChromaprintService, IFingerprintExtractionService in AddAudioCore
            services.AddSingleton<IAcoustIdClient, AcoustIdClient>();
            services.AddSingleton<IAutoTaggingService, AutoTaggingService>();
            services.AddSingleton<IMusicBrainzClient, MusicBrainzClient>();
            services.AddAudioCore(Program.AppDirectory);
            services.AddSingleton<IMetadataFacade>(sp => new MetadataFacade(
                sp.GetRequiredService<IMusicBrainzClient>(),
                sp.GetRequiredService<IAcoustIdClient>(),
                sp.GetRequiredService<IFingerprintExtractionService>(),
                sp.GetRequiredService<IOptionsMonitor<Options>>(),
                sp.GetRequiredService<ILogger<MetadataFacade>>(),
                sp.GetService<IMemoryCache>()));
            services.AddSingleton<IPushbulletService, PushbulletService>();
            services.AddSingleton<Integrations.Notifications.INotificationService, Integrations.Notifications.NotificationService>();

            // User Notes services
            var userNotesDbPath = Path.Combine(Program.AppDirectory, "user_notes.db");
            services.AddDbContextFactory<Users.Notes.UserNotesDbContext>(options =>
            {
                options.UseSqlite($"Data Source={userNotesDbPath}");
            });

            // Ensure user notes database is created
            using (var userNotesContext = new Users.Notes.UserNotesDbContext(
                new DbContextOptionsBuilder<Users.Notes.UserNotesDbContext>()
                    .UseSqlite($"Data Source={userNotesDbPath}")
                    .Options))
            {
                userNotesContext.Database.EnsureCreated();
            }

            services.AddSingleton<Users.Notes.IUserNoteService, Users.Notes.UserNoteService>();

            // Collections / sharing (ShareGroup, Collection, ShareGrant) — behind Feature.CollectionsSharing
            var collectionsDbPath = Path.Combine(Program.AppDirectory, "collections.db");
            services.AddDbContextFactory<Sharing.CollectionsDbContext>(options =>
            {
                options.UseSqlite($"Data Source={collectionsDbPath}");
            });
            using (var collectionsContext = new Sharing.CollectionsDbContext(
                new DbContextOptionsBuilder<Sharing.CollectionsDbContext>()
                    .UseSqlite($"Data Source={collectionsDbPath}")
                    .Options))
            {
                collectionsContext.Database.EnsureCreated();
            }
            services.AddSingleton<Sharing.IShareGroupRepository, Sharing.ShareGroupRepository>();
            services.AddSingleton<Sharing.ICollectionRepository, Sharing.CollectionRepository>();
            services.AddSingleton<Sharing.IShareGrantRepository, Sharing.ShareGrantRepository>();
            services.AddSingleton<Sharing.ISharingService, Sharing.SharingService>();

            // Identity / friends (PeerProfile, Contact) — behind Feature.IdentityFriends
            var identityDbPath = Path.Combine(Program.AppDirectory, "identity.db");
            services.AddDbContextFactory<Identity.IdentityDbContext>(options =>
            {
                options.UseSqlite($"Data Source={identityDbPath}");
            });
            using (var identityContext = new Identity.IdentityDbContext(
                new DbContextOptionsBuilder<Identity.IdentityDbContext>()
                    .UseSqlite($"Data Source={identityDbPath}")
                    .Options))
            {
                identityContext.Database.EnsureCreated();
            }
            services.AddSingleton<Identity.IContactRepository, Identity.ContactRepository>();
            services.AddSingleton<Identity.IContactService, Identity.ContactService>();
            services.AddSingleton<Identity.IProfileService, Identity.ProfileService>();
            services.AddSingleton<Identity.ILanDiscoveryService, Identity.LanDiscoveryService>();

            // Security services (zero-trust hardening)
            Log.Information("[DI] About to call AddSlskdnSecurity...");
            services.AddSlskdnSecurity(Configuration);
            Log.Information("[DI] AddSlskdnSecurity completed");

            return services;
        }

        private static void InitSQLiteOrFailFast()
        {
            // initialize
            // avoids: System.Exception: You need to call SQLitePCL.raw.SetProvider().  If you are using a bundle package, this is done by calling SQLitePCL.Batteries.Init().
            SQLitePCL.Batteries.Init();

            // check the threading mode set at compile time. if it is 0 it is unsafe to use in a multithreaded application, which slskd is.
            // https://www.sqlite.org/compile.html#threadsafe
            var threadSafe = SQLitePCL.raw.sqlite3_threadsafe();

            if (threadSafe == 0)
            {
                throw new InvalidOperationException($"SQLite binary was not compiled with THREADSAFE={threadSafe}, which is not compatible with this application. Please create a GitHub issue to report this and include details about your environment.");
            }

            Log.Debug("SQLite was compiled with THREADSAFE={Mode}", threadSafe);

            if (SQLitePCL.raw.sqlite3_config(SQLitePCL.raw.SQLITE_CONFIG_SERIALIZED) != SQLitePCL.raw.SQLITE_OK)
            {
                throw new InvalidOperationException($"SQLite threading mode could not be set to SERIALIZED ({SQLitePCL.raw.SQLITE_CONFIG_SERIALIZED}). Please create a GitHub issue to report this and include details about your environment.");
            }

            Log.Debug("SQLite threading mode set to {Mode} ({Number})", "SERIALIZED", SQLitePCL.raw.SQLITE_CONFIG_SERIALIZED);
        }

        private static IServiceCollection ConfigureAspDotNetServices(this IServiceCollection services)
        {
            Log.Information("[ASP] Starting ConfigureAspDotNetServices...");
            
            services.AddCors(options =>
            {
                var c = OptionsAtStartup.Web.Cors;
                if (c.Enabled && c.AllowedOrigins != null && c.AllowedOrigins.Length > 0)
                {
                    options.AddPolicy("ConfiguredCors", b =>
                    {
                        b.WithOrigins(c.AllowedOrigins)
                            .WithExposedHeaders("X-URL-Base", "X-Total-Count")
                            .SetPreflightMaxAge(TimeSpan.FromHours(1));
                        if (c.AllowCredentials)
                            b.AllowCredentials();
                        if (c.AllowedHeaders != null && c.AllowedHeaders.Length > 0)
                            b.WithHeaders(c.AllowedHeaders);
                        else
                            b.AllowAnyHeader();
                        if (c.AllowedMethods != null && c.AllowedMethods.Length > 0)
                            b.WithMethods(c.AllowedMethods);
                        else
                            b.AllowAnyMethod();
                    });
                }
            });

            // note: don't dispose this (or let it be disposed) or some of the stats, like those related
            // to the thread pool won't work
            DotNetRuntimeStats = DotNetRuntimeStatsBuilder.Default().StartCollecting();
            services.AddSystemMetrics();

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(DataDirectory, "misc", ".DataProtection-Keys")));

            var jwtSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OptionsAtStartup.Web.Authentication.Jwt.Key));

            services.AddSingleton(jwtSigningKey);
            services.AddSingleton<ISecurityService, SecurityService>();
            services.AddSingleton<Common.Security.ISoulseekSafetyLimiter, Common.Security.SoulseekSafetyLimiter>();

            // T-MCP01: Register Moderation / Control Plane services
            services.AddSingleton<Common.Moderation.IModerationProvider>(sp =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<Options>>();
                var logger = sp.GetRequiredService<ILogger<Common.Moderation.CompositeModerationProvider>>();

                if (!opts.CurrentValue.Moderation.Enabled)
                {
                    return new Common.Moderation.NoopModerationProvider();
                }

                // T-MCP03: Inject share repository for content ID checking
                var shareRepository = sp.GetService<Shares.IShareRepository>();

                // For now, use CompositeModerationProvider with no sub-providers
                // T-MCP02+ will add actual implementations
                // We need to wrap the Options.Moderation in an IOptionsMonitor
                var moderationOptions = Microsoft.Extensions.Options.Options.Create(opts.CurrentValue.Moderation);
                var moderationOptionsMonitor = new Common.Moderation.WrappedOptionsMonitor<Common.Moderation.ModerationOptions>(moderationOptions);

                return new Common.Moderation.CompositeModerationProvider(
                    moderationOptionsMonitor,
                    logger,
                    hashBlocklist: null,
                    peerReputation: null,
                    externalClient: null,
                    shareRepository: shareRepository); // T-MCP03
            });

            // T-FED01: Register Social Federation services
            if (OptionsAtStartup.SocialFederation.Enabled)
            {
                services.AddSocialFederation();
            }

            if (!OptionsAtStartup.Web.Authentication.Disabled)
            {
                services.AddAuthorization(options =>
                {
                    options.AddPolicy(AuthPolicy.JwtOnly, policy =>
                    {
                        policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                        policy.RequireAuthenticatedUser();
                    });

                    options.AddPolicy(AuthPolicy.ApiKeyOnly, policy =>
                    {
                        policy.AuthenticationSchemes.Add(ApiKeyAuthentication.AuthenticationScheme);
                        policy.RequireAuthenticatedUser();
                    });

                    options.AddPolicy(AuthPolicy.Any, policy =>
                    {
                        policy.AuthenticationSchemes.Add(ApiKeyAuthentication.AuthenticationScheme);
                        policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                        policy.RequireAuthenticatedUser();
                    });
                });

                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ClockSkew = TimeSpan.FromMinutes(5),
                            RequireSignedTokens = true,
                            RequireExpirationTime = true,
                            ValidateLifetime = true,
                            ValidIssuer = AppName,
                            ValidateIssuer = true,
                            ValidateAudience = false,
                            IssuerSigningKey = jwtSigningKey,
                            ValidateIssuerSigningKey = true,
                        };

                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                // signalr authentication is stupid
                                if (context.Request.Path.StartsWithSegments("/hub"))
                                {
                                    // assign the request token from the access_token query parameter if one is present
                                    // this typically means that the calling signalr client is running in a browser. this takes
                                    // precedent over the Authorization header value (if one is present)
                                    // https://docs.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-5.0
                                    if (context.Request.Query.TryGetValue("access_token", out var accessToken))
                                    {
                                        context.Token = accessToken;
                                    }
                                    else if (context.Request.Headers.ContainsKey("Authorization")
                                        && context.Request.Headers.TryGetValue("Authorization", out var authorization)
                                        && authorization.ToString().StartsWith("Bearer ", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // extract the bearer token. this value might be an API key, a JWT, or some garbage value
                                        var token = authorization.ToString().Split(' ').LastOrDefault();

                                        try
                                        {
                                            // check to see if the provided value is a valid API key
                                            var service = services.BuildServiceProvider().GetRequiredService<ISecurityService>();
                                            var (name, role) = service.AuthenticateWithApiKey(token, callerIpAddress: context.HttpContext.Connection.RemoteIpAddress);

                                            // the API key is valid. create a new, short lived jwt for the key name and role
                                            context.Token = service.GenerateJwt(name, role, ttl: 1000).Serialize();
                                        }
                                        catch
                                        {
                                            // the token either isn't a valid API key. use the provided value and let the
                                            // rest of the auth middleware figure out whether it is valid
                                            context.Token = token;
                                        }
                                    }
                                }

                                return Task.CompletedTask;
                            },
                        };
                    })
                    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthentication.AuthenticationScheme, (_) => { });
            }
            else
            {
                Log.Warning("Authentication of web requests is DISABLED");

                services.AddAuthorization(options =>
                {
                    options.AddPolicy(AuthPolicy.Any, policy =>
                    {
                        policy.AuthenticationSchemes.Add(PassthroughAuthentication.AuthenticationScheme);
                        policy.RequireAuthenticatedUser();
                    });

                    options.AddPolicy(AuthPolicy.ApiKeyOnly, policy =>
                    {
                        policy.AuthenticationSchemes.Add(PassthroughAuthentication.AuthenticationScheme);
                        policy.RequireAuthenticatedUser();
                    });

                    options.AddPolicy(AuthPolicy.JwtOnly, policy =>
                    {
                        policy.AuthenticationSchemes.Add(PassthroughAuthentication.AuthenticationScheme);
                        policy.RequireAuthenticatedUser();
                    });
                });

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = PassthroughAuthentication.AuthenticationScheme;
                    options.DefaultChallengeScheme = PassthroughAuthentication.AuthenticationScheme;
                    options.DefaultScheme = PassthroughAuthentication.AuthenticationScheme;
                })
                    .AddScheme<PassthroughAuthenticationOptions, PassthroughAuthenticationHandler>(PassthroughAuthentication.AuthenticationScheme, options =>
                    {
                        options.Username = "Anonymous";
                        options.Role = Role.Administrator;
                        options.AllowRemoteNoAuth = OptionsAtStartup.Web.AllowRemoteNoAuth;
                        options.AllowedCidrs = OptionsAtStartup.Web.Authentication.Passthrough?.AllowedCidrs;
                    });
            }

            services.AddMemoryCache(); // Required by ShardCache and others

            // CSRF Protection (only applies to cookie-based authentication, not JWT/API keys)
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "XSRF-TOKEN";
                options.HeaderName = "X-CSRF-TOKEN";
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTPS in prod, HTTP in dev
                options.Cookie.HttpOnly = false; // JavaScript needs to read this
                
                // IMPORTANT: Don't auto-validate - we use custom ValidateCsrfForCookiesOnlyAttribute
                // This ensures GET requests are never validated automatically
                options.SuppressXFrameOptionsHeader = false; // Keep X-Frame-Options for security
                
                // Session-based tokens (30 days with sliding expiration)
                // Tokens don't expire independently - they're tied to the session
            });

            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddControllers(options =>
                {
                    options.Filters.Add(new AuthorizeFilter(AuthPolicy.Any));
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.SuppressInferBindingSourcesForParameters = true; // explicit [FromRoute], etc
                    options.SuppressMapClientErrors = true; // disables automatic ProblemDetails for 4xx
                    // PR-07: when EnforceSecurity, enable automatic 400 for invalid model (ValidationProblemDetails)
                    options.SuppressModelStateInvalidFilter = !OptionsAtStartup.Web.EnforceSecurity;
                    options.DisableImplicitFromServicesParameters = true; // explicit [FromServices]
                    // PR-05, PR-07: custom ValidationProblemDetails; in Production do not leak internal property paths or structure.
                    options.InvalidModelStateResponseFactory = actionContext =>
                    {
                        var env = actionContext.HttpContext.RequestServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                        var isDev = env?.IsDevelopment() == true;
                        var problem = new ValidationProblemDetails(actionContext.ModelState)
                        {
                            Status = 400,
                            Title = "One or more validation errors occurred.",
                        };
                        if (!isDev)
                        {
                            problem.Detail = "The request is invalid.";
                            problem.Errors.Clear();
                        }
                        return new BadRequestObjectResult(problem);
                    };
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new IPAddressConverter());
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });

            services
                .AddSignalR(options =>
                {
                    // https://github.com/SignalR/SignalR/issues/1149#issuecomment-973887222
                    options.MaximumParallelInvocationsPerClient = 2;
                })
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.Converters.Add(new IPAddressConverter());
                    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            services.AddHealthChecks()
                .AddSecurityHealthCheck()
                .AddMeshHealthCheck();

            services.AddApiVersioning(options =>
                {
                    options.ReportApiVersions = true;
                })
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            // PR-09: HTTP rate limiting – Api (generous), FederationInbox (tighter), MeshGateway (tighter). Per-IP partitions.
            if (OptionsAtStartup.Web.RateLimiting.Enabled)
            {
                var rl = OptionsAtStartup.Web.RateLimiting;
                var apiPermit = rl.ApiPermitLimit;
                var apiWindow = TimeSpan.FromSeconds(rl.ApiWindowSeconds <= 0 ? 60 : rl.ApiWindowSeconds);
                var fedPermit = rl.FederationPermitLimit;
                var fedWindow = TimeSpan.FromSeconds(rl.FederationWindowSeconds <= 0 ? 60 : rl.FederationWindowSeconds);
                var meshPermit = rl.MeshGatewayPermitLimit;
                var meshWindow = TimeSpan.FromSeconds(rl.MeshGatewayWindowSeconds <= 0 ? 60 : rl.MeshGatewayWindowSeconds);

                services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = 429;
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    {
                        var path = context.Request.Path.Value ?? "";
                        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                        if (path.StartsWith("/mesh/", StringComparison.OrdinalIgnoreCase))
                            return RateLimitPartition.GetFixedWindowLimiter("mesh:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = meshPermit, Window = meshWindow });
                        if (path.Contains("/inbox", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                            return RateLimitPartition.GetFixedWindowLimiter("fed:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = fedPermit, Window = fedWindow });
                        return RateLimitPartition.GetFixedWindowLimiter("api:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = apiPermit, Window = apiWindow });
                    });
                });
            }

            if (OptionsAtStartup.Feature.Swagger)
            {
                services.AddSwaggerGen(options =>
                {
                    options.DescribeAllParametersInCamelCase();
                    options.SwaggerDoc("v0", new OpenApiInfo
                    {
                        Version = "v0",
                        Title = AppName,
                        Description = "A modern client-server application for the Soulseek community service network",
                        Contact = new OpenApiContact
                        {
                            Name = "GitHub",
                            Url = new Uri("https://github.com/slskd/slskd"),
                        },
                        License = new OpenApiLicense
                        {
                            Name = "AGPL-3.0 license",
                            Url = new Uri("https://github.com/slskd/slskd/blob/master/LICENSE"),
                        },
                    });

                    // allow endpoints marked with multiple content types in [Produces] to generate properly
                    options.OperationFilter<ContentNegotiationOperationFilter>();

                    if (IOFile.Exists(XmlDocumentationFile))
                    {
                        options.IncludeXmlComments(XmlDocumentationFile);
                    }
                    else
                    {
                        Log.Warning($"Unable to find XML documentation in {XmlDocumentationFile}, Swagger will not include metadata");
                    }
                });
            }

            return services;
        }

        private static WebApplication ConfigureAspDotNetPipeline(this WebApplication app)
        {
            // STEP 1: Verify middleware is in the built pipeline by inspecting the ApplicationBuilder
            // STEP 2: Check for exceptions during pipeline construction
            // STEP 3: Use a custom middleware class instead of inline delegate
            Log.Information("[Pipeline] Starting ConfigureAspDotNetPipeline...");
            
            // PR-05: RFC 7807 ProblemDetails; in Production do not leak exception message; always include traceId
            app.UseExceptionHandler(a => a.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerPathFeature>();
                if (feature?.Error != null)
                {
                    var ex = feature.Error;
                    var path = context.Request.Path.Value ?? string.Empty;
                    var traceId = context.TraceIdentifier;
                    Log.Error(ex, "[ExceptionHandler] Unhandled exception for {Method} {Path} traceId={TraceId}: {Message}",
                        context.Request.Method, path, traceId, ex.Message);

                    if (!context.Response.HasStarted)
                    {
                        int status;
                        string title;
                        string detail;
                        if (ex is FeatureNotImplementedException fe)
                        {
                            // §11: Incomplete features → 501 Not Implemented
                            status = 501;
                            title = "Not Implemented";
                            detail = fe.Message;
                        }
                        else
                        {
                            var env = context.RequestServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                            var isDev = env?.IsDevelopment() == true;
                            status = 500;
                            title = "Internal Server Error";
                            detail = isDev ? ex.ToString() : "An unexpected error occurred.";
                        }
                        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
                        problem.Extensions["traceId"] = traceId;
                        context.Response.StatusCode = status;
                        context.Response.ContentType = "application/problem+json";
                        await context.Response.Body.WriteAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(problem));
                    }
                    else
                    {
                        Log.Warning("[ExceptionHandler] Response already started, cannot write error body for {Method} {Path} traceId={TraceId}",
                            context.Request.Method, path, traceId);
                    }
                }
            }));

            if (OptionsAtStartup.Web.Cors.Enabled && OptionsAtStartup.Web.Cors.AllowedOrigins != null && OptionsAtStartup.Web.Cors.AllowedOrigins.Length > 0)
                app.UseCors("ConfiguredCors");

            // CSRF token middleware - generates tokens for cookie-based auth
            // This must come AFTER UsePathBase but BEFORE UseAuthentication
            app.Use(async (context, next) =>
            {
                // Log all requests to MediaCore endpoints for debugging
                var path = context.Request.Path.Value ?? string.Empty;
                if (path.Contains("mediacore", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("[CSRF Middleware] Processing MediaCore request: {Method} {Path} (Raw: {RawPath})", 
                        context.Request.Method, path, context.Request.Path);
                }
                
                try
                {
                    var antiforgery = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
                    
                    // GetAndStoreTokens can throw for some requests - catch and log but don't fail
                    // This is safe because our custom ValidateCsrfForCookiesOnlyAttribute handles validation
                    var tokens = antiforgery.GetAndStoreTokens(context);
                    
                    // Set the XSRF-TOKEN cookie that frontend JavaScript can read
                    if (tokens.RequestToken != null)
                    {
                        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken, 
                            new CookieOptions 
                            { 
                                HttpOnly = false,  // JavaScript needs to read this
                                Secure = context.Request.IsHttps,
                                SameSite = SameSiteMode.Strict,
                                Path = "/"
                            });
                    }
                }
                catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException ex)
                {
                    // This is expected for some requests - log at debug level only
                    Log.Debug(ex, "[CSRF Middleware] Antiforgery validation exception for {Method} {Path} (this is normal for some requests)", 
                        context.Request.Method, context.Request.Path);
                }
                catch (Exception ex)
                {
                    // Log other exceptions but don't fail - GetAndStoreTokens can fail for some requests
                    Log.Warning(ex, "[CSRF Middleware] Exception getting/storing tokens for {Method} {Path}", 
                        context.Request.Method, context.Request.Path);
                }
                
                await next();
            });

            if (OptionsAtStartup.Web.Https.Force)
            {
                app.UseHttpsRedirection();
                app.UseHsts();

                Log.Information($"Forcing HTTP requests to HTTPS");
            }

            // Security middleware (rate limiting, violation tracking, etc.)
            // MUST be FIRST in pipeline (before UsePathBase) to catch path traversal and other attacks
            // This ensures we check the raw request path before any path rewriting occurs
            Log.Information("[Pipeline] About to call UseSlskdnSecurity (FIRST in pipeline)...");
            app.UseSlskdnSecurity();
            Log.Information("[Pipeline] UseSlskdnSecurity completed");

            // allow users to specify a custom path base, for use behind a reverse proxy
            var urlBase = OptionsAtStartup.Web.UrlBase;
            urlBase = urlBase.StartsWith("/") ? urlBase : "/" + urlBase;

            // use urlBase. this effectively just removes urlBase from the path.
            // inject urlBase into any html files we serve, and rewrite links to ./static or /static to
            // prepend the url base.
            app.UsePathBase(urlBase);
            app.UseHTMLRewrite("((\\.)?\\/static)", $"{(urlBase == "/" ? string.Empty : urlBase)}/static");
            app.UseHTMLInjection($"<script>window.urlBase=\"{urlBase}\";window.port={OptionsAtStartup.Web.Port}</script>", excludedRoutes: new[] { "/api", "/swagger" });
            Log.Information("Using base url {UrlBase}", urlBase);

            // serve static content from the configured path
            FileServerOptions fileServerOptions = default;
            var contentPath = Path.Combine(AppContext.BaseDirectory, OptionsAtStartup.Web.ContentPath);

            fileServerOptions = new FileServerOptions
            {
                FileProvider = new PhysicalFileProvider(contentPath),
                RequestPath = string.Empty,
                EnableDirectoryBrowsing = false,
                EnableDefaultFiles = true,
            };
            
            // CRITICAL: Block suspicious paths at the file server level
            // This is the last line of defense before files are served
            fileServerOptions.StaticFileOptions.OnPrepareResponse = (context) =>
            {
                var path = context.Context.Request.Path.Value ?? string.Empty;
                var rawTarget = context.Context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestFeature>()?.RawTarget ?? string.Empty;
                
                if (path.Contains("/etc/passwd") || path.Contains("/etc/") || 
                    rawTarget.Contains("/etc/passwd") || rawTarget.Contains("/etc/") ||
                    path.StartsWith("/etc", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("[FILE_SERVER_BLOCK] Blocking suspicious path: {Path}, RawTarget: {RawTarget}", path, rawTarget);
                    context.Context.Response.StatusCode = 400;
                    context.Context.Response.ContentLength = 0;
                }
            };

            // Mesh gateway auth middleware (must be before UseRouting to catch /mesh paths)
            // This middleware blocks /mesh/* paths when gateway is disabled
            app.UseMiddleware<Mesh.ServiceFabric.MeshGatewayAuthMiddleware>();

            // PR-14: Capture POST /actors/.../inbox body for HTTP Signature verification (Digest) before model binding.
            // §8: Bounded read to prevent DoS; reject over MaxRemotePayloadSize with 413.
            app.UseWhen(
                ctx => string.Equals(ctx.Request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                    && ctx.Request.Path.StartsWithSegments("/actors", StringComparison.OrdinalIgnoreCase)
                    && (ctx.Request.Path.Value ?? string.Empty).Contains("/inbox", StringComparison.OrdinalIgnoreCase),
                branch => branch.Use(async (ctx, next) =>
                {
                    ctx.Request.EnableBuffering();
                    var limit = ctx.RequestServices.GetService<IOptions<Mesh.MeshOptions>>()?.Value?.Security?.GetEffectiveMaxPayloadSize()
                        ?? slskd.Mesh.Transport.SecurityUtils.MaxRemotePayloadSize;
                    if (ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > limit)
                    {
                        ctx.Response.StatusCode = 413;
                        return;
                    }
                    var buf = new byte[8192];
                    int total = 0;
                    using var ms = new MemoryStream();
                    int n;
                    while ((n = await ctx.Request.Body.ReadAsync(buf)) > 0)
                    {
                        total += n;
                        if (total > limit)
                        {
                            ctx.Response.StatusCode = 413;
                            return;
                        }
                        ms.Write(buf, 0, n);
                    }
                    var b = ms.ToArray();
                    ctx.Request.Body.Position = 0;
                    ctx.Items["ActivityPubInboxBody"] = b;
                    await next(ctx);
                }));

            app.UseAuthentication();
            app.UseRouting();
            if (OptionsAtStartup.Web.RateLimiting.Enabled)
                app.UseRateLimiter();
            app.UseAuthorization();

            if (OptionsAtStartup.Web.Logging)
            {
                app.UseSerilogRequestLogging();
            }

            // UseFileServer is placed AFTER UseEndpoints to ensure routing happens first, then static files.
            // This prevents static file middleware from short-circuiting requests before routing/security middleware runs.
            
            // starting with .NET 7 the framework *really* wants you to use top level endpoint mapping
            // for whatever reason this breaks everything, and i just can't bring myself to care unless
            // UseEndpoints is going to be deprecated or if there's some material benefit
#pragma warning disable ASP0014 // Suggest using top level route registrations
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<ApplicationHub>("/hub/application");
                endpoints.MapHub<LogsHub>("/hub/logs");
                endpoints.MapHub<Transfers.API.TransfersHub>("/hub/transfers");
                var searchHub = endpoints.MapHub<SearchHub>("/hub/search");
                if (OptionsAtStartup.Web.EnforceSecurity)
                    searchHub.RequireAuthorization(AuthPolicy.Any);
                var relayHub = endpoints.MapHub<RelayHub>("/hub/relay");
                if (OptionsAtStartup.Web.EnforceSecurity)
                    relayHub.RequireAuthorization(AuthPolicy.Any);

                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
                endpoints.MapHealthChecks("/health/mesh", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("mesh")
                });

                if (OptionsAtStartup.Metrics.Enabled)
                {
                    var options = OptionsAtStartup.Metrics;
                    var url = options.Url.StartsWith('/') ? options.Url : "/" + options.Url;

                    Log.Information("Publishing Prometheus metrics to {URL}", url);

                    if (options.Authentication.Disabled)
                    {
                        Log.Warning("Authentication for the metrics endpoint is DISABLED");
                    }

                    endpoints.MapGet(url, async context =>
                    {
                        // at the time of writing, the prometheus library doesn't include a way to add authentication
                        // to the UseMetricServer() middleware. this is most likely a consequence of me mixing
                        // and matching minimal API stuff with controllers. if i ever straighten that out,
                        // this should be revisited.
                        if (!options.Authentication.Disabled)
                        {
                            static void Reject(HttpContext ctx)
                            {
                                ctx.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"metrics\"");
                                ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            }

                            var auth = context.Request.Headers["Authorization"].FirstOrDefault();
                            if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                            {
                                Reject(context);
                                return;
                            }
                            var providedBase64 = auth["Basic ".Length..].Trim();
                            if (string.IsNullOrEmpty(providedBase64))
                            {
                                Reject(context);
                                return;
                            }
                            byte[] providedBytes;
                            try
                            {
                                providedBytes = Convert.FromBase64String(providedBase64);
                            }
                            catch (FormatException)
                            {
                                Reject(context);
                                return;
                            }
                            var validBytes = Encoding.UTF8.GetBytes($"{options.Authentication.Username}:{options.Authentication.Password}");
                            if (!CryptographicOperations.FixedTimeEquals(providedBytes, validBytes))
                            {
                                Reject(context);
                                return;
                            }
                        }

                        var telemetryService = context.RequestServices.GetRequiredService<TelemetryService>();
                        var metricsAsText = await telemetryService.Prometheus.GetMetricsAsString();

                        context.Response.Headers.Append("Content-Type", "text/plain; version=0.0.4; charset=utf-8");
                        await context.Response.WriteAsync(metricsAsText);
                    });
                }
            });
#pragma warning restore ASP0014 // Suggest using top level route registrations

            // UseFileServer is placed AFTER UseEndpoints to ensure routing happens first, then static files.
            // This prevents static file middleware from short-circuiting requests before routing/security middleware runs.
            if (!OptionsAtStartup.Headless)
            {
                app.UseFileServer(fileServerOptions);
                Log.Information("Serving static content from {ContentPath}", contentPath);
            }
            else
            {
                Log.Warning("Running in headless mode; web UI is DISABLED");
            }

            // if this is an /api route and no API controller was matched, give up and return a 404.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                await next();
            });

            if (OptionsAtStartup.Feature.Swagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(options => app.Services.GetRequiredService<IApiVersionDescriptionProvider>().ApiVersionDescriptions.ToList()
                    .ForEach(description => options.SwaggerEndpoint($"{(urlBase == "/" ? string.Empty : urlBase)}/swagger/{description.GroupName}/swagger.json", description.GroupName)));

                Log.Information("Publishing Swagger documentation to {URL}", "/swagger");
            }

            /*
                if we made it this far, the caller is either looking for a route that was synthesized with a SPA router, or is genuinely confused.
                if the request is for a directory, modify the request to redirect it to the index, otherwise leave it alone and let it 404 in the next
                middleware.

                if we're running in headless mode, do nothing and let ASP.NET return a 404
            */
            if (!OptionsAtStartup.Headless)
            {
                app.Use(async (context, next) =>
                {
                    if (Path.GetExtension(context.Request.Path.ToString()) == string.Empty)
                    {
                        context.Request.Path = "/";
                    }

                    await next();
                });

            }

            return app;
        }

        private static void ConfigureGlobalLogger()
        {
            Serilog.Log.Logger = (OptionsAtStartup.Debug ? new LoggerConfiguration().MinimumLevel.Debug() : new LoggerConfiguration().MinimumLevel.Information())
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .MinimumLevel.Override("System.Net.Http.HttpClient", OptionsAtStartup.Debug ? LogEventLevel.Warning : LogEventLevel.Fatal)
                .MinimumLevel.Override("slskd.Authentication.PassthroughAuthenticationHandler", LogEventLevel.Warning)
                .MinimumLevel.Override("slskd.Authentication.ApiKeyAuthenticationHandler", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning) // bump this down to Information to show SQL
                .Enrich.WithProperty("InstanceName", OptionsAtStartup.InstanceName)
                .Enrich.WithProperty("InvocationId", InvocationId)
                .Enrich.WithProperty("ProcessId", ProcessId)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    theme: (OptionsAtStartup.Logger.NoColor || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))) ? ConsoleTheme.None : SystemConsoleTheme.Literate,
                    outputTemplate: (OptionsAtStartup.Debug ? "[{SourceContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Async(config =>
                    config.Conditional(
                        e => OptionsAtStartup.Logger.Disk,
                        config => config.File(
                            Path.Combine(LogDirectory, $"{AppName}-.log"),
                            outputTemplate: (OptionsAtStartup.Debug ? "[{SourceContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                            rollingInterval: RollingInterval.Day,
                            retainedFileTimeLimit: TimeSpan.FromDays(OptionsAtStartup.Retention.Logs))))
                .WriteTo.Conditional(
                    e => !string.IsNullOrEmpty(OptionsAtStartup.Logger.Loki),
                    config => config.GrafanaLoki(
                        OptionsAtStartup.Logger.Loki ?? string.Empty,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .WriteTo.Sink(new DelegatingSink(logEvent =>
                {
                    string message = default;

                    try
                    {
                        message = logEvent.RenderMessage();

                        if (logEvent.Exception != null)
                        {
                            message = $"{message}: {logEvent.Exception}";
                        }

                        var record = new LogRecord()
                        {
                            Timestamp = logEvent.Timestamp.LocalDateTime,
                            Context = logEvent.Properties["SourceContext"].ToString().TrimStart('"').TrimEnd('"'),
                            SubContext = logEvent.Properties.ContainsKey("SubContext") ? logEvent.Properties["SubContext"].ToString().TrimStart('"').TrimEnd('"') : null,
                            Level = logEvent.Level.ToString(),
                            Message = message.TrimStart('"').TrimEnd('"'),
                        };

                        LogBuffer.Enqueue(record);
                        LogEmitted?.Invoke(null, record);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Misconfigured delegating logger: {Exception}.  Message: {Message}", ex.Message, message);
                    }
                }))
                .CreateLogger();

            if (OptionsAtStartup.Flags.LogUnobservedExceptions)
            {
                // log Exceptions raised on fired-and-forgotten tasks, which adds very little value but might help debug someday
                TaskScheduler.UnobservedTaskException += (sender, e) =>
                {
                    Serilog.Log.Logger.Error(e.Exception, "Unobserved exception: {Message}", e.Exception.Message);
                    e.SetObserved();
                };
            }

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;

                if (e.IsTerminating)
                {
                    Serilog.Log.Logger.Fatal(exception, "Unhandled fatal exception: {Message}", e.IsTerminating);
                }
                else
                {
                    Serilog.Log.Logger.Error(exception, "Unhandled exception: {Message}", exception.Message);
                }
            };
        }

        private static IConfigurationBuilder AddConfigurationProviders(this IConfigurationBuilder builder, string environmentVariablePrefix, string configurationFile, bool reloadOnChange)
        {
            configurationFile = Path.GetFullPath(configurationFile);
            Log.Information("[Config] Loading configuration from {ConfigFile}", configurationFile);

            var multiValuedArguments = typeof(Options)
                .GetPropertiesRecursively()
                .Where(p => p.PropertyType.IsArray)
                .SelectMany(p =>
                    p.CustomAttributes
                        .Where(a => a.AttributeType == typeof(ArgumentAttribute))
                        .Select(a => new[] { a.ConstructorArguments[0].Value, a.ConstructorArguments[1].Value })
                        .SelectMany(v => v))
                .Select(v => v.ToString())
                .Where(v => v != "\u0000")
                .ToArray();

            var result = builder
                .AddDefaultValues(
                    targetType: typeof(Options))
                .AddEnvironmentVariables(
                    targetType: typeof(Options),
                    prefix: environmentVariablePrefix)
                .AddYamlFile(
                    path: Path.GetFileName(configurationFile),
                    targetType: typeof(Options),
                    optional: true,
                    reloadOnChange: reloadOnChange,
                    provider: new PhysicalFileProvider(Path.GetDirectoryName(configurationFile), ExclusionFilters.None)) // required for locations outside of the app directory
                .AddCommandLine(
                    targetType: typeof(Options),
                    multiValuedArguments,
                    commandLine: Environment.CommandLine);
            
            Log.Information("[Config] Configuration providers added, YAML file: {ConfigFile}", configurationFile);
            return result;
        }

        private static IServiceCollection AddDbContext<T>(this IServiceCollection services, string connectionString)
            where T : DbContext
        {
            Log.Debug("Initializing database context {Name}", typeof(T).Name);

            try
            {
                services.AddDbContextFactory<T>(options =>
                {
                    options.UseSqlite(connectionString);
                    options.AddInterceptors(new SqliteConnectionOpenedInterceptor());

                    if (OptionsAtStartup.Debug && OptionsAtStartup.Flags.LogSQL)
                    {
                        options.LogTo(Log.Debug, LogLevel.Information);
                    }
                });

                /*
                    instantiate the DbContext and make sure it is created
                */
                using var ctx = services
                    .BuildServiceProvider()
                    .GetRequiredService<IDbContextFactory<T>>()
                    .CreateDbContext();

                Log.Debug("Ensuring {Contex} is created", typeof(T).Name);
                ctx.Database.EnsureCreated();

                /*
                    set (and validate) our desired PRAGMAs

                    synchronous mode is also set upon every connection via SqliteConnectionOpenedInterceptor.
                */
                ctx.Database.OpenConnection();
                var conn = ctx.Database.GetDbConnection();

                Log.Debug("Setting PRAGMAs for {Contex}", typeof(T).Name);
                using var initCommand = conn.CreateCommand();
                initCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=1; PRAGMA optimize;";
                initCommand.ExecuteNonQuery();

                using var journalCmd = conn.CreateCommand();
                journalCmd.CommandText = "PRAGMA journal_mode;";
                var journalMode = journalCmd.ExecuteScalar()?.ToString();

                if (!journalMode.Equals("WAL", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Failed to set database {Type} journal_mode PRAGMA to WAL; performance may be reduced", typeof(T).Name);
                }

                using var syncCmd = conn.CreateCommand();
                syncCmd.CommandText = "PRAGMA synchronous;";
                var sync = syncCmd.ExecuteScalar()?.ToString();

                if (!sync.Equals("1", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Failed to set database {Type} synchronous PRAGMA to 1; performance may be reduced", typeof(T).Name);
                }

                Log.Debug("PRAGMAs for {Context}: journal_mode={JournalMode}, synchronous={Synchronous}", typeof(T).Name, journalMode, sync);

                return services;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to initialize database context {typeof(T).Name}: ${ex.Message}");
                throw;
            }
        }

        private static void RecreateConfigurationFileIfMissing(string configurationFile)
        {
            if (!IOFile.Exists(configurationFile))
            {
                try
                {
                    Log.Warning("Configuration file {ConfigurationFile} does not exist; creating from example", configurationFile);
                    var source = Path.Combine(AppContext.BaseDirectory, "config", $"{AppName}.example.yml");
                    var destination = configurationFile;
                    IOFile.Copy(source, destination);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to create configuration file {ConfigurationFile}: {Message}", configurationFile, ex.Message);
                }
            }
        }

        private static (string Filename, string Password) GenerateX509Certificate(string password, string filename)
        {
            filename = Path.Combine(AppContext.BaseDirectory, filename);

            var cert = X509.Generate(subject: AppName, password, X509KeyStorageFlags.Exportable);
            IOFile.WriteAllBytes(filename, cert.Export(X509ContentType.Pkcs12, password));

            return (filename, password);
        }

        private static void PrintCommandLineArguments(Type targetType)
        {
            static string GetLongName(string longName, Type type)
                => type == typeof(bool) ? longName : $"{longName} <{type.ToColloquialString().ToLowerInvariant()}>";

            var lines = new List<(string Item, string Description)>();

            void Map(Type type)
            {
                try
                {
                    var defaults = Activator.CreateInstance(type);
                    var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                    foreach (PropertyInfo property in props)
                    {
                        var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ArgumentAttribute));
                        var descriptionAttribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(DescriptionAttribute));
                        var isRequired = property.CustomAttributes.Any(a => a.AttributeType == typeof(RequiredAttribute));

                        if (attribute != default)
                        {
                            var shortName = (char)attribute.ConstructorArguments[0].Value;
                            var longName = (string)attribute.ConstructorArguments[1].Value;
                            var description = descriptionAttribute?.ConstructorArguments[0].Value;

                            var suffix = isRequired ? " (required)" : $" (default: {property.GetValue(defaults) ?? "<null>"})";
                            var item = $"{(shortName == default ? "  " : $"{shortName}|")}--{GetLongName(longName, property.PropertyType)}";
                            var desc = $"{description}{(property.PropertyType == typeof(bool) ? string.Empty : suffix)}";
                            lines.Add(new(item, desc));
                        }
                        else
                        {
                            Map(property.PropertyType);
                        }
                    }
                }
                catch
                {
                    return;
                }
            }

            Map(targetType);

            var longestItem = lines.Max(l => l.Item.Length);

            Log.Information("\nusage: slskd [arguments]\n");
            Log.Information("arguments:\n");

            foreach (var line in lines)
            {
                Log.Information($"  {line.Item.PadRight(longestItem)}   {line.Description}");
            }
        }

        private static void PrintEnvironmentVariables(Type targetType, string prefix)
        {
            static string GetName(string name, Type type) => $"{name} <{type.ToColloquialString().ToLowerInvariant()}>";

            var lines = new List<(string Item, string Description)>();

            void Map(Type type)
            {
                try
                {
                    var defaults = Activator.CreateInstance(type);
                    var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                    foreach (PropertyInfo property in props)
                    {
                        var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(EnvironmentVariableAttribute));
                        var descriptionAttribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(DescriptionAttribute));
                        var isRequired = property.CustomAttributes.Any(a => a.AttributeType == typeof(RequiredAttribute));

                        if (attribute != default)
                        {
                            var name = (string)attribute.ConstructorArguments[0].Value;
                            var description = descriptionAttribute?.ConstructorArguments[0].Value;

                            var suffix = isRequired ? " (required)" : $" (default: {property.GetValue(defaults) ?? "<null>"})";
                            var item = $"{prefix}{GetName(name, property.PropertyType)}";
                            var desc = $"{description}{(type == typeof(bool) ? string.Empty : suffix)}";
                            lines.Add(new(item, desc));
                        }
                        else
                        {
                            Map(property.PropertyType);
                        }
                    }
                }
                catch
                {
                    return;
                }
            }

            Map(targetType);

            var longestItem = lines.Max(l => l.Item.Length);

            Log.Information("\nenvironment variables (arguments and config file have precedence):\n");

            foreach (var line in lines)
            {
                Log.Information($"  {line.Item.PadRight(longestItem)}   {line.Description}");
            }
        }

        private static void PrintLogo(string version)
        {
            try
            {
                var padding = 56 - version.Length;
                var paddingLeft = padding / 2;
                var paddingRight = paddingLeft + (padding % 2);

                var centeredVersion = new string(' ', paddingLeft) + version + new string(' ', paddingRight);

                var logos = new[]
                {
                    $@"
                   ▄▄▄▄         ▄▄▄▄       ▄▄▄▄
           ▄▄▄▄▄▄▄ █  █ ▄▄▄▄▄▄▄ █  █▄▄▄ ▄▄▄█  █
           █__ --█ █  █ █__ --█ █    ◄█ █  -  █
           █▄▄▄▄▄█ █▄▄█ █▄▄▄▄▄█ █▄▄█▄▄█ █▄▄▄▄▄█",
                    @$"
                    ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
              ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
              █__ --█  █__ --█    ◄█  -  █
              █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█",
                };

                var logo = logos[new System.Random().Next(0, logos.Length)];

                var banner = @$"
{logo}
╒════════════════════════════════════════════════════════╕
│           GNU AFFERO GENERAL PUBLIC LICENSE            │
│                   https://slskd.org                    │
│                                                        │
│{centeredVersion}│";

                if (IsDevelopment)
                {
                    banner += "\n│■■■■■■■■■■■■■■■■■■■■► DEVELOPMENT ◄■■■■■■■■■■■■■■■■■■■■■│";
                }

                if (IsCanary)
                {
                    banner += "\n│■■■■■■■■■■■■■■■■■■■■■■■► CANARY ◄■■■■■■■■■■■■■■■■■■■■■■■│";
                }

                banner += "\n└────────────────────────────────────────────────────────┘";

                Console.WriteLine(banner);
            }
            catch
            {
                // noop. console may not be available in all cases.
            }
        }

        private static void VerifyDirectory(string directory, bool createIfMissing = true, bool verifyWriteable = true)
        {
            if (!System.IO.Directory.Exists(directory))
            {
                if (createIfMissing)
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Directory {directory} does not exist, and could not be created: {ex.Message}", ex);
                    }
                }
                else
                {
                    throw new IOException($"Directory {directory} does not exist");
                }
            }

            if (verifyWriteable)
            {
                try
                {
                    var file = Guid.NewGuid().ToString();
                    var probe = Path.Combine(directory, file);
                    IOFile.WriteAllText(probe, string.Empty);
                    IOFile.Delete(probe);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Directory {directory} is not writeable: {ex.Message}", ex);
                }
            }
        }

        private static void InstallShutdownTelemetry()
        {
            // Install hard telemetry to catch silent exits and unhandled exceptions
            // This ensures we always know WHY the process terminated

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                var msg = $"[FATAL] ProcessExit event fired - process terminating";
                Console.Error.WriteLine(msg);
                try { Log?.Fatal(msg); } catch { }
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                var msg = $"[FATAL] Unhandled exception: {ex?.Message ?? e.ExceptionObject?.ToString() ?? "unknown"}";
                Console.Error.WriteLine(msg);
                Console.Error.WriteLine(ex?.StackTrace ?? "no stack trace");
                try { Log?.Fatal(ex, msg); } catch { }
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                var msg = $"[FATAL] Unobserved task exception: {e.Exception.Message}";
                Console.Error.WriteLine(msg);
                Console.Error.WriteLine(e.Exception.StackTrace);
                try { Log?.Fatal(e.Exception, msg); } catch { }
                e.SetObserved(); // Prevent process termination
            };
        }

    }
}