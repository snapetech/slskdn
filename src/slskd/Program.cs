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

// <copyright file="Program.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.AudioCore;
using slskd.Core.Diagnostics;
using slskd.Mesh.Gossip;
using slskd.Mesh.Governance;
using slskd.Mesh.Realm;
using slskd.Mesh.Realm.Bridge;
using slskd.SocialFederation;
using slskd.VirtualSoulfind.Core;

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
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
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.FileProviders.Physical;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi;
    using OpenTelemetry.Trace;
    using Prometheus.DotNetRuntime;
    using Prometheus.SystemMetrics;
    using Serilog;
    using Serilog.Events;
    using Serilog.Formatting.Display;
    using Serilog.Sinks.Grafana.Loki;
    using Serilog.Sinks.SystemConsole.Themes;
    using slskd.Audio;
    using slskd.Authentication;
    using slskd.Common.Security;
    using slskd.Configuration;
    using slskd.Core.API;
    using slskd.Cryptography;
    using slskd.DhtRendezvous;
    using slskd.DhtRendezvous.Security;
    using slskd.Events;
    using slskd.Files;
    using slskd.Identity;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.AutoTagging;
    using slskd.Integrations.Chromaprint;
    using slskd.Integrations.FTP;
    using slskd.Integrations.MetadataFacade;
    using slskd.Integrations.Lidarr;
    using slskd.Integrations.MusicBrainz;
    using slskd.Integrations.Pushbullet;
    using slskd.Integrations.Scripts;
    using slskd.Integrations.VPN;
    using slskd.Integrations.Webhooks;
    using slskd.LibraryHealth;
    using slskd.ListeningParty;
    using slskd.Mesh;
    using slskd.Messaging;
    using slskd.Player;
    using slskd.Relay;
    using slskd.Search;
    using slskd.Search.API;
    using slskd.Shares;
    using slskd.Sharing;
    using slskd.Signals;
    using slskd.SongID;
    using slskd.Streaming;
    using slskd.Telemetry;
    using slskd.Transfers;
    using slskd.Transfers.Downloads;
    using slskd.Transfers.MultiSource;
    using slskd.Transfers.MultiSource.Discovery;
    using slskd.Transfers.Rescue;
    using slskd.Transfers.Uploads;
    using slskd.Users;
    using slskd.Validation;
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
        public static readonly string IssuesUrl = "https://github.com/snapetech/slskdn/issues";

        /// <summary>
        ///     The global prefix for environment variables.
        /// </summary>
        public static readonly string EnvironmentVariablePrefix = $"{AppName.ToUpperInvariant()}_";

        /// <summary>
        ///     The default XML documentation filename.
        /// </summary>
        public static readonly string XmlDocumentationFile = Path.Combine(AppContext.BaseDirectory, "etc", $"{AppName}.xml");

        /// <summary>
        ///     Soulseek.NET requires a caller-owned minor-version slot.
        ///     slskdN reserved range: 7700000-7709999 (registry PR pending).
        ///     Reserved range 760-7699999 belongs to upstream slskd.
        /// </summary>
        public static readonly int SoulseekMinorVersion = 7700000;

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

        /// <summary>
        ///     Gets the application's base directory.
        /// </summary>
        public static readonly string BaseDirectory = AppContext.BaseDirectory;

        /// <summary>
        ///     Gets the current executable path when available.
        /// </summary>
        public static readonly string ExecutablePath = TryGetExecutablePath();

        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        private static readonly Version AssemblyVersion = (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0)).Equals(new Version(1, 0, 0, 0))
            ? new Version(0, 0, 0, 0)
            : (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0));

        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        private static readonly string InformationalVersion = (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0") == "1.0.0"
            ? "0.0.0"
            : (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0");

        /// <summary>
        ///     Occurs when a new log event is emitted.
        /// </summary>
        public static event EventHandler<LogRecord> LogEmitted = (_, _) => { };

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

        private static void RaiseLogEmitted(LogRecord record)
        {
            foreach (EventHandler<LogRecord> handler in LogEmitted.GetInvocationList())
            {
                try
                {
                    handler.Invoke(null, record);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "LogEmitted subscriber failed");
                }
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the application is being run in Relay Agent mode.
        /// </summary>
        public static bool IsRelayAgent { get; private set; }

        private static string TryGetExecutablePath()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        ///     Gets the application flags.
        /// </summary>
        public static Options.FlagsOptions Flags { get; private set; } = new();

        /// <summary>
        ///     Gets the path where application data is saved.
        /// </summary>
        [Argument('a', "app-dir", "path where application data is saved")]
        [EnvironmentVariable("APP_DIR")]
        public static string AppDirectory { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the fully qualified path to the application configuration file.
        /// </summary>
        [Argument('c', "config", "path to configuration file")]
        [EnvironmentVariable("CONFIG")]
        public static string ConfigurationFile { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the current configuration overlay, if one has been applied.
        /// </summary>
        public static OptionsOverlay? ConfigurationOverlay => VolatileOverlayConfigurationSource?.CurrentValue;

        /// <summary>
        ///     Gets the path where persistent data is saved.
        /// </summary>
        public static string DataDirectory { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the path where backups of persistent data saved.
        /// </summary>
        public static string DataBackupDirectory { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the default fully qualified path to the configuration file.
        /// </summary>
        public static string DefaultConfigurationFile { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the default downloads directory.
        /// </summary>
        public static string DefaultDownloadsDirectory { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the default incomplete download directory.
        /// </summary>
        public static string DefaultIncompleteDirectory { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the path where application logs are saved.
        /// </summary>
        public static string LogDirectory { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the path where user-defined scripts are stored.
        /// </summary>
        public static string ScriptDirectory { get; private set; } = string.Empty;

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

        private static IConfigurationRoot? Configuration { get; set; }
        private static OptionsAtStartup OptionsAtStartup { get; } = new OptionsAtStartup();

        // Explicit Serilog.ILogger type to avoid ambiguity with Microsoft.Extensions.Logging.ILogger
        private static Serilog.ILogger Log { get; set; } = new Serilog.LoggerConfiguration()
            .WriteTo.Sink(new ConsoleWriteLineLogger())
            .CreateLogger();

        // Mutex is created lazily after AppDirectory is set to allow multiple test instances with different app dirs
        private static Mutex? Mutex { get; set; }

        private static string GetMutexName()
        {
            // Use app directory in mutex name if set, otherwise use default
            var dir = AppDirectory ?? DefaultAppDirectory;
            return $"{AppName}_{Compute.Sha256Hash(dir)}";
        }

        internal static string GetWriteBaseDirectory()
        {
            return string.IsNullOrWhiteSpace(AppDirectory) ? DefaultAppDirectory : AppDirectory;
        }

        internal static string ResolveOptionalAppRelativePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path) ? path : Path.Combine(GetWriteBaseDirectory(), path);
        }

        internal static string ResolveAppRelativePath(string path, string fallbackRelativePath)
        {
            var candidate = string.IsNullOrWhiteSpace(path) ? fallbackRelativePath : path;
            return ResolveOptionalAppRelativePath(candidate);
        }

        internal static IReadOnlyList<(string Pattern, string Replacement)> CreateWebHtmlRewriteRules(string urlBase)
        {
            var normalizedUrlBase = string.IsNullOrWhiteSpace(urlBase) || urlBase == "/"
                ? string.Empty
                : (urlBase.StartsWith("/") ? urlBase : "/" + urlBase).TrimEnd('/');

            string Prefix(string path) => string.IsNullOrEmpty(normalizedUrlBase) ? path : $"{normalizedUrlBase}{path}";

            return new List<(string Pattern, string Replacement)>
            {
                ("((?:src|href)=\")/assets/", $"$1{Prefix("/assets/")}"),
                ("((?:src|href)=\")/manifest\\.json", $"$1{Prefix("/manifest.json")}"),
                ("((?:src|href)=\")/logo192\\.png", $"$1{Prefix("/logo192.png")}"),
                ("((?:src|href)=\")/logo512\\.png", $"$1{Prefix("/logo512.png")}"),
            };
        }

        internal static SoulseekClientOptions CreateInitialSoulseekClientOptions(OptionsAtStartup optionsAtStartup)
        {
            if (!IPAddress.TryParse(optionsAtStartup.Soulseek.ListenIpAddress, out var startupListenAddress))
            {
                startupListenAddress = IPAddress.Any;
            }

            return new SoulseekClientOptions(
                enableListener: true,
                listenIPAddress: startupListenAddress,
                listenPort: optionsAtStartup.Soulseek.ListenPort,
                enableDistributedNetwork: !optionsAtStartup.Soulseek.DistributedNetwork.Disabled,
                acceptDistributedChildren: !optionsAtStartup.Soulseek.DistributedNetwork.DisableChildren,
                distributedChildLimit: optionsAtStartup.Soulseek.DistributedNetwork.ChildLimit,
                maximumUploadSpeed: optionsAtStartup.Global.Upload.SpeedLimit,
                maximumConcurrentUploads: optionsAtStartup.Global.Upload.Slots,
                maximumDownloadSpeed: optionsAtStartup.Global.Download.SpeedLimit,
                maximumConcurrentDownloads: optionsAtStartup.Global.Download.Slots,
                minimumDiagnosticLevel: optionsAtStartup.Soulseek.DiagnosticLevel.ToEnum<Soulseek.Diagnostics.DiagnosticLevel>(),
                maximumConcurrentSearches: 2,
                raiseEventsAsynchronously: true);
        }

        internal static bool IsBenignUnobservedTaskException(Exception exception)
        {
            var aggregate = exception as AggregateException;
            var exceptions = aggregate != null
                ? aggregate.Flatten().InnerExceptions.ToArray()
                : new[] { exception };

            return exceptions.Length > 0 && exceptions.All(IsBenignUnobservedTaskInnerException);
        }

        private static bool IsBenignUnobservedTaskInnerException(Exception exception)
        {
            return false;
        }

        private static IDisposable? DotNetRuntimeStats { get; set; }
        private static VolatileOverlayConfigurationSource<OptionsOverlay> VolatileOverlayConfigurationSource { get; set; } = new VolatileOverlayConfigurationSource<OptionsOverlay>();

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
        ///     Apply an instance of <see cref="OptionsOverlay"/> on top of the existing application configuration.
        /// </summary>
        /// <param name="overlay">The overlay containing the property values to be overlaid.</param>
        public static void ApplyConfigurationOverlay(OptionsOverlay overlay) => VolatileOverlayConfigurationSource.Apply(overlay);

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
                Console.WriteLine($"Password: {password}");
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

            // derive the application directory value and defaults that are dependent upon it
            if (string.IsNullOrWhiteSpace(AppDirectory))
            {
                AppDirectory = DefaultAppDirectory;
            }

            // the application isn't being run in command mode. check the mutex to ensure
            // only one long-running instance per app directory.
            // Create mutex with name that includes app directory to allow multiple test instances
            Mutex = new Mutex(initiallyOwned: true, GetMutexName());
            if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false))
            {
                Log.Fatal($"An instance of {AppName} is already running in app directory: {AppDirectory}");
                return;
            }

            DataDirectory = Path.Combine(AppDirectory, "data");
            DataBackupDirectory = Path.Combine(DataDirectory, "backups");
            LogDirectory = Path.Combine(AppDirectory, "logs");
            ScriptDirectory = Path.Combine(AppDirectory, "scripts");

            DefaultConfigurationFile = Path.Combine(AppDirectory, $"{AppName}.yml");
            DefaultDownloadsDirectory = Path.Combine(AppDirectory, "downloads");
            DefaultIncompleteDirectory = Path.Combine(AppDirectory, "incomplete");

            // the location of the configuration file might have been overridden by command line or envar.
            // if not, set it to the default.
            if (string.IsNullOrWhiteSpace(ConfigurationFile))
            {
                ConfigurationFile = DefaultConfigurationFile;
            }

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
                Exit(1);
            }

            // load and validate the configuration
            try
            {
                Configuration = new ConfigurationBuilder()
                    .AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile, reloadOnChange: !OptionsAtStartup.Flags.NoConfigWatch)
                    .Build();

                Configuration.GetSection(AppName)
                    .Bind(OptionsAtStartup, (o) => { o.BindNonPublicProperties = true; });

                Log.Debug("[Config] After binding OptionsAtStartup.Security.Enabled = {Enabled}, Profile = {Profile}",
                    OptionsAtStartup.Security?.Enabled ?? false,
                    OptionsAtStartup.Security?.Profile.ToString() ?? "null");

                var securitySection = Configuration.GetSection("security");
                var slskdSecuritySection = Configuration.GetSection("slskd:security");
                Log.Debug("[Config] Raw config sections - security.Exists={SecurityExists}, slskd:security.Exists={SlskdSecurityExists}",
                    securitySection.Exists(),
                    slskdSecuritySection.Exists());
                if (securitySection.Exists())
                {
                    Log.Debug("[Config] Raw security section enabled value: {Enabled}", securitySection["enabled"]);
                }

                if (slskdSecuritySection.Exists())
                {
                    Log.Debug("[Config] Raw slskd:security section enabled value: {Enabled}", slskdSecuritySection["enabled"]);
                }

                if (!OptionsAtStartup.TryValidate(out var result))
                {
                    Log.Information(result.GetResultView());
                    Exit(1);
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Invalid configuration: {(!OptionsAtStartup.Debug ? ex : ex.Message)}");
                Exit(1);
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
            Log.Information("Executable path: {ExecutablePath}", ExecutablePath);
            Log.Information("Base directory: {BaseDirectory}", BaseDirectory);

            Log.Information("Invocation ID: {InvocationId}", InvocationId);
            Log.Information("Instance Name: {InstanceName}", OptionsAtStartup.InstanceName);

            Log.Information("Configuring application...");

            // SQLite must have specific capabilities to function properly. this shouldn't be a concern for shrinkwrapped
            // binaries or in Docker, but if someone builds from source weird things can happen.
            InitSQLiteOrFailFast();

            Log.Information("Using application directory {AppDirectory}", AppDirectory);
            Log.Information("Using configuration file {ConfigurationFile}", ConfigurationFile);

            foreach (var warning in GetConfigurationCompatibilityWarnings(ConfigurationFile, OptionsAtStartup))
            {
                Log.Warning("{Warning}", warning);
            }

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

                // Deterministic port probe for E2E startup debugging.
                var portStr = builder.Configuration[$"{AppName}:Web:Port"] ?? "<null>";
                if (Environment.GetEnvironmentVariable("SLSKDN_E2E_SERVER_PROBE") == "1")
                {
                    System.Console.Error.WriteLine($"[ConfigProbe] slskd:web:port={portStr}");
                }

                // Note: OptionsAtStartup was bound earlier from a different Configuration instance.
                // Since Options properties are init-only, we can't rebind them. Instead, we read
                // values directly from builder.Configuration when needed (e.g., in UseKestrel below).
                builder.Host
                    .UseSerilog();

                var webPortSection = builder.Configuration.GetSection($"{AppName}:Web:Port");
                var webPort = webPortSection.Exists() && int.TryParse(webPortSection.Value, out var port)
                    ? port
                    : OptionsAtStartup.Web.Port; // Fallback to OptionsAtStartup if not in config

                var webAddressSection = builder.Configuration.GetSection($"{AppName}:Web:Address");
                var webAddress = webAddressSection.Exists() && !string.IsNullOrEmpty(webAddressSection.Value)
                    ? webAddressSection.Value
                    : OptionsAtStartup.Web.Address; // Fallback to OptionsAtStartup if not in config

                var configuredAddress = webAddress == "*" ? IPAddress.Any.ToString() : webAddress;
                if (!IPAddress.TryParse(configuredAddress, out var listenAddress))
                {
                    Log.Warning("Invalid web bind address '{Address}', defaulting to 0.0.0.0", configuredAddress);
                    listenAddress = IPAddress.Any;
                }

                var listenAddressUrl = listenAddress.AddressFamily == AddressFamily.InterNetworkV6
                    ? $"[{listenAddress}]"
                    : listenAddress.ToString();

                builder.WebHost
                    .UseUrls($"http://{listenAddressUrl}:{webPort}")
                    .UseKestrel(options =>
                    {
                        // PR-09: Global body size cap; configurable via Web.MaxRequestBodySize (default 10 MB). MeshGateway and others may enforce lower per-route.
                        options.Limits.MaxRequestBodySize = OptionsAtStartup.Web.MaxRequestBodySize;

                        Log.Debug("[ConfigProbe] slskd:web:port={A} slskd:slskd:web:port={B} using={C}",
                            builder.Configuration.GetValue<string>($"{AppName}:Web:Port") ?? "null",
                            builder.Configuration.GetValue<string>($"{AppName}:{AppName}:Web:Port") ?? "null",
                            webPort);

                        Log.Information($"[Kestrel] Configuring HTTP listener at http://{listenAddressUrl}:{webPort}/ (from config: port={webPortSection.Exists()}, address={webAddressSection.Exists()})");
                        options.Listen(listenAddress, webPort);
                        Log.Debug($"[Kestrel] HTTP listener configured");

                        if (!string.IsNullOrWhiteSpace(OptionsAtStartup.Web.Socket))
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

                Log.Debug("[MAIN] About to configure ASP.NET services...");
                builder.Services
                    .ConfigureAspDotNetServices()
                    .ConfigureDependencyInjectionContainer();

                if (Environment.GetEnvironmentVariable("SLSKDN_E2E_TRACE_HOSTED") == "1")
                {
                    Console.Error.WriteLine("[HostedServiceTracer] Enabled (SLSKDN_E2E_TRACE_HOSTED=1)");

                    // Replace hosted services with a tracer to pinpoint startup blockers
                    var hostedDescriptors = builder.Services
                        .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                        .ToList();

                    foreach (var descriptor in hostedDescriptors)
                    {
                        builder.Services.Remove(descriptor);
                    }

                    builder.Services.AddSingleton<IEnumerable<Microsoft.Extensions.Hosting.IHostedService>>(sp =>
                    {
                        var list = new List<Microsoft.Extensions.Hosting.IHostedService>();
                        foreach (var descriptor in hostedDescriptors)
                        {
                            var svcName = descriptor.ImplementationType?.FullName
                                          ?? descriptor.ImplementationInstance?.GetType().FullName
                                          ?? "factory";
                            Console.Error.WriteLine($"[HostedServiceTracer] create {svcName} begin");

                            var svc = descriptor.ImplementationInstance as Microsoft.Extensions.Hosting.IHostedService
                                      ?? (descriptor.ImplementationFactory?.Invoke(sp) as Microsoft.Extensions.Hosting.IHostedService)
                                      ?? (Microsoft.Extensions.Hosting.IHostedService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
                            list.Add(svc);

                            Console.Error.WriteLine($"[HostedServiceTracer] create {svcName} end");
                        }

                        return list;
                    });

                    builder.Services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, HostedServiceTracer>();
                }

                // Add startup timeout for fail-fast in E2E tests (prevents infinite hangs)
                builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options =>
                {
                    options.StartupTimeout = TimeSpan.FromSeconds(30);
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SLSKDN_E2E_CONCURRENT_START")))
                    {
                        options.ServicesStartConcurrently = true;
                        options.ServicesStopConcurrently = true;
                    }
                });

                // Enable detailed logging for host lifetime and Kestrel in test/dev environments
                builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", Microsoft.Extensions.Logging.LogLevel.Information);
                builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", Microsoft.Extensions.Logging.LogLevel.Debug);

                Log.Debug("[MAIN] Services configured, building DI container...");
                WebApplication app;
                try
                {
                    Log.Debug("Building DI container...");
                    Log.Debug("[DI] About to call builder.Build() - this will construct all singleton services...");
                    app = builder.Build();
                    Log.Debug("DI container built successfully!");
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
                    app.Services.GetRequiredService<Migrator>().Migrate(force: OptionsAtStartup.Flags.ForceMigrations);
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
                Log.Debug("[DI] Forcing construction of ScriptService, WebhookService, VPNService, and TrafficObserverIntegrationService...");
                _ = app.Services.GetService<ScriptService>();
                _ = app.Services.GetService<WebhookService>();
                _ = app.Services.GetService<VPNService>();
                _ = app.Services.GetService<VirtualSoulfind.Capture.TrafficObserverIntegrationService>();
                Log.Debug("[DI] ScriptService, WebhookService, VPNService, and TrafficObserverIntegrationService constructed");

                Log.Debug("[DI] About to configure ASP.NET pipeline...");
                try
                {
                    app.ConfigureAspDotNetPipeline();
                    Log.Debug("[DI] ASP.NET pipeline configured");
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

                    if (Environment.GetEnvironmentVariable("SLSKDN_E2E_SERVER_PROBE") == "1")
                    {
                        try
                        {
                            var server = app.Services.GetService<IServer>();
                            Console.Error.WriteLine($"[ServerProbe] IServer={server?.GetType().FullName ?? "<null>"}");

                            var serverFeatures = server?.Features.Get<IServerAddressesFeature>();
                            if (serverFeatures == null)
                            {
                                Console.Error.WriteLine("[ServerProbe] IServerAddressesFeature=<null>");
                            }
                            else
                            {
                                Console.Error.WriteLine($"[ServerProbe] PreferHostingUrls={serverFeatures.PreferHostingUrls}");
                                Console.Error.WriteLine($"[ServerProbe] Addresses={string.Join(" | ", serverFeatures.Addresses)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[ServerProbe] EX={ex}");
                        }
                    }
                });

                lifetime.ApplicationStopping.Register(() =>
                {
                    Log.Information("Application is stopping...");
                });

                if (Environment.GetEnvironmentVariable("SLSKDN_E2E_SERVER_PROBE") == "1")
                {
                    var hostedServices = app.Services.GetServices<IHostedService>()
                        .Select(s => s.GetType().FullName)
                        .OrderBy(s => s)
                        .ToArray();
                    Console.Error.WriteLine($"[HostedList] count={hostedServices.Length}");
                    foreach (var hosted in hostedServices)
                    {
                        Console.Error.WriteLine($"[HostedList] {hosted}");
                    }
                }

                Log.Debug("[Program] About to call app.Run()...");
                Log.Debug("[Program] app.Run() will start the web server and all hosted services...");

                // Add lifecycle hooks to track startup progress
                var hostLifetime = app.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();

                // Log when web server starts listening (happens before hosted services StartAsync)
                hostLifetime.ApplicationStarted.Register(() =>
                {
                    Log.Debug("[Program] ApplicationStarted event fired - all hosted services have completed StartAsync");

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
                        if (discovery is not null)
                        {
                            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            discovery.StopAdvertisingAsync().WaitAsync(timeout.Token).GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[Program] Failed to stop LAN discovery advertising on host stopping");
                    }
                });

                // Try to detect if we're hanging during web server startup
                Log.Debug("[Program] Calling app.Run() - this will block until shutdown...");
                Log.Debug("[Program] If you see this but not 'Host started and bound', the web server is hanging");

                // Deterministic Kestrel binding probe for E2E startup debugging.
                if (Environment.GetEnvironmentVariable("SLSKDN_E2E_SERVER_PROBE") == "1")
                {
                    System.Console.Error.WriteLine($"[KestrelProbe] URLs={string.Join(";", app.Urls)}");
                }

                app.Run();
                Log.Debug("[Program] app.Run() returned after host shutdown");
            }
            catch (Common.Security.HardeningValidationException hex)
            {
                Console.Error.WriteLine($"[HardeningValidation] {hex.RuleName}: {hex.Message}");
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
            Log.Debug("[DI] Starting ConfigureDependencyInjectionContainer...");

            // add the instance of OptionsAtStartup to DI as they were at startup. use when Options might change, but
            // the values at startup are to be used (generally anything marked RequiresRestart).
            services.AddSingleton(OptionsAtStartup);

            // add IOptionsMonitor and IOptionsSnapshot to DI.
            // use when the current Options are to be used (generally anything not marked RequiresRestart)
            // the monitor should be used for services with Singleton lifetime, snapshots for everything else
            services.AddOptions<Options>()
                .Bind(Configuration!.GetSection(AppName), o => { o.BindNonPublicProperties = true; })
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

            // add configured-only external player integrations.
            services.AddSingleton<IExternalProcessStarter, ExternalProcessStarter>();
            services.AddSingleton<IExternalVisualizerLauncher, ExternalVisualizerLauncher>();

            // add IHttpClientFactory
            // use through 'using var http = HttpClientFactory.CreateClient()' wherever HTTP calls will be made
            // this is important to prevent memory leaks
            services.AddHttpClient();

            // PR-14: SSRF-safe key fetcher for ActivityPub HTTP Signature (timeout 3s, no redirects to prevent SSRF)
            services.AddHttpClient<SocialFederation.IHttpSignatureKeyFetcher, SocialFederation.HttpSignatureKeyFetcher>(c => c.Timeout = TimeSpan.FromSeconds(3))
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

            // add a partially configured instance of SoulseekClient. the Application instance will
            // complete configuration at startup.
            services.AddSingleton<ISoulseekClient, SoulseekClient>(_ =>
                new SoulseekClient(SoulseekMinorVersion, options: CreateInitialSoulseekClientOptions(OptionsAtStartup)));

            // add the core application service to DI as well as a hosted service so that other services can
            // access instance methods
            services.AddSingleton<IApplication>(sp =>
            {
                Log.Debug("[DI] Factory function called to construct Application singleton...");
                Log.Debug("[DI] Resolving OptionsAtStartup...");
                var optionsAtStartup = sp.GetRequiredService<OptionsAtStartup>();
                Log.Debug("[DI] Resolving IOptionsMonitor<Options>...");
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<Options>>();
                Log.Debug("[DI] Resolving IManagedState<State>...");
                var state = sp.GetRequiredService<IManagedState<State>>();
                Log.Debug("[DI] Resolving ISoulseekClient...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Debug("[DI] Resolving FileService...");
                var fileService = sp.GetRequiredService<FileService>();
                Log.Debug("[DI] Resolving ConnectionWatchdog...");
                var connectionWatchdog = sp.GetRequiredService<ConnectionWatchdog>();
                Log.Debug("[DI] Resolving ITransferService...");
                var transferService = sp.GetRequiredService<ITransferService>();
                Log.Debug("[DI] Resolving IBrowseTracker...");
                var browseTracker = sp.GetRequiredService<IBrowseTracker>();
                Log.Debug("[DI] Resolving IRoomService...");
                var roomService = sp.GetRequiredService<IRoomService>();
                Log.Debug("[DI] Resolving IUserService...");
                var userService = sp.GetRequiredService<IUserService>();
                Log.Debug("[DI] Resolving IMessagingService...");
                var messagingService = sp.GetRequiredService<IMessagingService>();
                Log.Debug("[DI] Resolving IShareService...");
                var shareService = sp.GetRequiredService<IShareService>();
                Log.Debug("[DI] Resolving ISearchService...");
                var searchService = sp.GetRequiredService<ISearchService>();
                Log.Debug("[DI] Resolving INotificationService...");
                var notificationService = sp.GetRequiredService<Integrations.Notifications.INotificationService>();
                Log.Debug("[DI] Resolving IRelayService...");
                var relayService = sp.GetRequiredService<IRelayService>();
                Log.Debug("[DI] Resolving IHubContext<ApplicationHub>...");
                var applicationHub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ApplicationHub>>();
                Log.Debug("[DI] Resolving IHubContext<LogsHub>...");
                var logHub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<LogsHub>>();
                Log.Debug("[DI] Resolving IHubContext<TransfersHub>...");
                var transfersHub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Transfers.API.TransfersHub>>();
                Log.Debug("[DI] Resolving EventBus...");
                var eventBus = sp.GetRequiredService<Events.EventBus>();
                var eventService = sp.GetRequiredService<Events.EventService>();
                Log.Debug("[DI] Resolving ShareGrantAnnouncementService (best-effort)...");
                _ = sp.GetService<Sharing.ShareGrantAnnouncementService>();
                Log.Debug("[DI] All dependencies resolved, constructing Application...");
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                var nowPlayingService = sp.GetRequiredService<NowPlaying.NowPlayingService>();
                var app = new Application(
                    optionsAtStartup, optionsMonitor, state, soulseekClient, fileService,
                    connectionWatchdog, transferService, browseTracker, roomService,
                    userService, messagingService, shareService, searchService,
                    notificationService, relayService, applicationHub, logHub, transfersHub,
                    eventBus, eventService, sp, scopeFactory, nowPlayingService);
                Log.Debug("[DI] Application singleton constructed successfully");
                return app;
            });

            // Use a wrapper to avoid factory function blocking
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SLSKDN_E2E_SKIP_APP_HOSTED")))
            {
                services.AddHostedService(p =>
                {
                    Log.Debug("[DI] Constructing ApplicationHostedServiceWrapper hosted service...");
                    Log.Debug("[DI] About to resolve IApplication from DI...");
                    var app = p.GetRequiredService<IApplication>();
                    Log.Debug("[DI] IApplication resolved successfully");
                    Log.Debug("[DI] About to create ApplicationHostedServiceWrapper instance...");
                    var service = new ApplicationHostedServiceWrapper(app, p.GetService<Microsoft.Extensions.Logging.ILogger<ApplicationHostedServiceWrapper>>());
                    Log.Debug("[DI] ApplicationHostedServiceWrapper constructed");
                    return service;
                });
            }
            else
            {
                Log.Debug("[DI] SLSKDN_E2E_SKIP_APP_HOSTED=1; skipping ApplicationHostedServiceWrapper registration");
            }

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

            services.AddSingleton<VPNService>();
            services.AddSingleton<ILidarrClient, LidarrClient>();
            services.AddSingleton<LidarrSyncService>();
            services.AddSingleton<ILidarrSyncService>(sp => sp.GetRequiredService<LidarrSyncService>());
            services.AddHostedService(sp => sp.GetRequiredService<LidarrSyncService>());
            services.AddSingleton<LidarrImportService>();
            services.AddSingleton<ILidarrImportService>(sp => sp.GetRequiredService<LidarrImportService>());
            services.AddHostedService(sp => sp.GetRequiredService<LidarrImportService>());
            services.AddSingleton<ScriptService>();
            services.AddSingleton<WebhookService>();
            services.AddSingleton<NowPlaying.NowPlayingService>();
            services.AddSingleton<IListeningPartyService, ListeningPartyService>();

            services.AddSingleton<IBrowseTracker, BrowseTracker>();
            services.AddSingleton<IRoomTracker, RoomTracker>(_ => new RoomTracker(messageLimit: 250));

            services.AddSingleton<IMessagingService, MessagingService>();
            services.AddSingleton<IConversationService>(sp =>
            {
                Log.Debug("[DI] Constructing ConversationService...");
                Log.Debug("[DI] Resolving ISoulseekClient for ConversationService...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Debug("[DI] Resolving EventBus for ConversationService...");
                var eventBus = sp.GetRequiredService<Events.EventBus>();
                Log.Debug("[DI] Resolving IDbContextFactory<MessagingDbContext> for ConversationService...");
                var contextFactory = sp.GetRequiredService<IDbContextFactory<Messaging.MessagingDbContext>>();
                Log.Debug("[DI] Resolving IPodService for ConversationService...");
                var podService = sp.GetRequiredService<PodCore.IPodService>();
                Log.Debug("[DI] All ConversationService dependencies resolved, creating instance...");
                var service = new Messaging.ConversationService(soulseekClient, eventBus, contextFactory, podService);
                Log.Debug("[DI] ConversationService constructed");
                return service;
            });

            services.AddSingleton<IShareService>(sp =>
            {
                Log.Debug("[DI] Constructing ShareService...");
                Log.Debug("[DI] Resolving FileService for ShareService...");
                var fileService = sp.GetRequiredService<FileService>();
                Log.Debug("[DI] Resolving IShareRepositoryFactory for ShareService...");
                var shareRepositoryFactory = sp.GetRequiredService<IShareRepositoryFactory>();
                Log.Debug("[DI] Resolving IOptionsMonitor<Options> for ShareService...");
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<Options>>();
                Log.Debug("[DI] Resolving IModerationProvider for ShareService...");
                var moderationProvider = sp.GetRequiredService<Common.Moderation.IModerationProvider>();
                Log.Debug("[DI] Resolving IShareScanner for ShareService (optional)...");
                var scanner = sp.GetService<IShareScanner>();
                Log.Debug("[DI] Resolving IContentPeerHintService for ShareService (optional)...");
                var contentPeerHintService = sp.GetService<Mesh.Dht.IContentPeerHintService>();
                Log.Debug("[DI] All ShareService dependencies resolved, creating instance...");
                var service = new ShareService(
                    fileService, shareRepositoryFactory, optionsMonitor, moderationProvider, scanner, contentPeerHintService);
                Log.Debug("[DI] ShareService constructed");
                return service;
            });
            services.AddSingleton<IShareRepository>(sp =>
                sp.GetRequiredService<IShareService>().GetLocalRepository());
            services.AddTransient<IShareRepositoryFactory, SqliteShareRepositoryFactory>();

            services.AddSingleton<IContentLocator, ContentLocator>();
            services.AddSingleton<IStreamSessionLimiter, StreamSessionLimiter>();
            services.AddSingleton<IStreamTicketService, StreamTicketService>();
            services.AddSingleton<IShareTokenService, ShareTokenService>();

            // Register search providers for Scene ↔ Pod Bridging
            services.AddSingleton<slskd.Search.Providers.ISearchProvider>(sp =>
                new slskd.Search.Providers.SceneSearchProvider(
                    sp.GetRequiredService<ISoulseekClient>(),
                    sp.GetRequiredService<slskd.Common.Security.ISoulseekSafetyLimiter>(),
                    sp.GetRequiredService<ILogger<slskd.Search.Providers.SceneSearchProvider>>()));
            services.AddSingleton<slskd.Search.Providers.ISearchProvider>(sp =>
                new slskd.Search.Providers.PodSearchProvider(
                    sp.GetRequiredService<slskd.DhtRendezvous.Search.IMeshOverlaySearchService>(),
                    sp.GetRequiredService<ILogger<slskd.Search.Providers.PodSearchProvider>>()));

            services.AddSingleton<ISearchService>(sp =>
            {
                var searchHub = sp.GetRequiredService<IHubContext<SearchHub>>();
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<Options>>();
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                var contextFactory = sp.GetRequiredService<IDbContextFactory<SearchDbContext>>();
                var safetyLimiter = sp.GetRequiredService<slskd.Common.Security.ISoulseekSafetyLimiter>();
                var eventBus = sp.GetService<slskd.Events.EventBus>();
                var disasterModeCoordinator = sp.GetService<slskd.VirtualSoulfind.DisasterMode.IDisasterModeCoordinator>();
                var meshSearchService = sp.GetService<slskd.VirtualSoulfind.DisasterMode.IMeshSearchService>();
                var meshOverlaySearchService = sp.GetService<slskd.DhtRendezvous.Search.IMeshOverlaySearchService>();
                var trafficObserver = sp.GetService<slskd.VirtualSoulfind.Capture.ITrafficObserver>();
                var searchProviders = sp.GetServices<slskd.Search.Providers.ISearchProvider>();

                return new SearchService(
                    searchHub,
                    optionsMonitor,
                    soulseekClient,
                    contextFactory,
                    safetyLimiter,
                    eventBus,
                    disasterModeCoordinator,
                    meshSearchService,
                    meshOverlaySearchService,
                    trafficObserver,
                    searchProviders);
            });

            services.AddSingleton<IUsernameMatcher, RegexUsernameMatcher>();
            services.AddSingleton<IUserService, UserService>();

            services.AddSingleton<IRoomService, RoomService>();

            services.AddSingleton<IScheduledRateLimitService, ScheduledRateLimitService>();
            services.AddSingleton<IDownloadService>(sp =>
            {
                Log.Debug("[DI] Constructing DownloadService...");
                var service = new DownloadService(
                    sp.GetRequiredService<IOptionsMonitor<Options>>(),
                    sp.GetRequiredService<ISoulseekClient>(),
                    sp.GetRequiredService<IDbContextFactory<TransfersDbContext>>(),
                    sp.GetRequiredService<FileService>(),
                    sp.GetRequiredService<IRelayService>(),
                    sp.GetRequiredService<IFTPService>(),
                    sp.GetRequiredService<EventBus>(),
                    sp.GetService<Transfers.MultiSource.Metrics.IPeerMetricsService>());
                Log.Debug("[DI] DownloadService constructed");
                return service;
            });
            services.AddSingleton<IUploadService>(sp =>
            {
                Log.Debug("[DI] Constructing UploadService...");
                Log.Debug("[DI] Resolving FileService for UploadService...");
                var fileService = sp.GetRequiredService<FileService>();
                Log.Debug("[DI] Resolving IUserService for UploadService...");
                var userService = sp.GetRequiredService<IUserService>();
                Log.Debug("[DI] Resolving ISoulseekClient for UploadService...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Debug("[DI] Resolving IOptionsMonitor<Options> for UploadService...");
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<Options>>();
                Log.Debug("[DI] Resolving IShareService for UploadService...");
                var shareService = sp.GetRequiredService<IShareService>();
                Log.Debug("[DI] Resolving IRelayService for UploadService...");
                var relayService = sp.GetRequiredService<IRelayService>();
                Log.Debug("[DI] Resolving IDbContextFactory<TransfersDbContext> for UploadService...");
                var contextFactory = sp.GetRequiredService<IDbContextFactory<TransfersDbContext>>();
                Log.Debug("[DI] Resolving EventBus for UploadService...");
                var eventBus = sp.GetRequiredService<EventBus>();
                Log.Debug("[DI] Resolving IScheduledRateLimitService for UploadService (optional)...");
                var scheduledRateLimitService = sp.GetService<IScheduledRateLimitService>();
                Log.Debug("[DI] All UploadService dependencies resolved, creating instance...");
                var service = new UploadService(
                    fileService, userService, soulseekClient, optionsMonitor,
                    shareService, relayService, contextFactory, eventBus, scheduledRateLimitService);
                Log.Debug("[DI] UploadService constructed");
                return service;
            });
            services.AddSingleton<ITransferService>(sp =>
            {
                Log.Debug("[DI] Constructing TransferService...");
                var service = new TransferService(
                    sp.GetRequiredService<IUploadService>(),
                    sp.GetRequiredService<IDownloadService>());
                Log.Debug("[DI] TransferService constructed");
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
            services.AddSingleton<IDiscographyCoverageService, DiscographyCoverageService>();
            services.AddSingleton<Integrations.MusicBrainz.Bloom.ILibraryBloomDiffService, Integrations.MusicBrainz.Bloom.LibraryBloomDiffService>();
            services.AddSingleton<Integrations.MusicBrainz.Radar.IArtistReleaseRadarService, Integrations.MusicBrainz.Radar.ArtistReleaseRadarService>();
            services.AddSingleton<Integrations.MusicBrainz.Overlay.IMusicBrainzOverlayService, Integrations.MusicBrainz.Overlay.MusicBrainzOverlayService>();
            services.AddSingleton<QuarantineJury.IQuarantineJuryService, QuarantineJury.QuarantineJuryService>();
            services.AddSingleton<Jobs.IDiscographyJobService, Jobs.DiscographyJobService>();
            services.AddSingleton<Jobs.ILabelCrateJobService, Jobs.LabelCrateJobService>();
            services.AddSingleton<slskd.API.Native.IJobServiceWithList, slskd.Jobs.HashDbJobServiceListAdapter>();
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

            // OpenTelemetry distributed tracing
            services.AddOpenTelemetryTracing(OptionsAtStartup);
            services.AddSingleton<Transfers.MultiSource.Caching.IWarmCachePopularityService, Transfers.MultiSource.Caching.WarmCachePopularityService>();
            services.AddSingleton<Transfers.MultiSource.Optimization.IChunkSizeOptimizer, Transfers.MultiSource.Optimization.ChunkSizeOptimizer>();
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
            services.AddSingleton<VirtualSoulfind.Capture.IObservationStore>(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<slskd.Options>>();
                if (options.CurrentValue.VirtualSoulfind?.Privacy?.PersistRawObservations == true)
                {
                    return new VirtualSoulfind.Capture.SqliteObservationStore(
                        sp.GetRequiredService<ILogger<VirtualSoulfind.Capture.SqliteObservationStore>>(),
                        options);
                }

                return new VirtualSoulfind.Capture.InMemoryObservationStore(
                    sp.GetRequiredService<ILogger<VirtualSoulfind.Capture.InMemoryObservationStore>>());
            });
            services.AddSingleton<VirtualSoulfind.Capture.TrafficObserverIntegrationService>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShadowIndexBuilder, VirtualSoulfind.ShadowIndex.ShadowIndexBuilder>();

            // Note: IDhtClient is registered later in MeshCore section (line ~1456) as InMemoryDhtClient
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IDhtRateLimiter>(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<slskd.Options>>();
                var maxOpsPerMin = options.CurrentValue.VirtualSoulfind?.ShadowIndex?.MaxDhtOperationsPerMinute ?? 60;
                return new VirtualSoulfind.ShadowIndex.DhtRateLimiter(
                    sp.GetRequiredService<ILogger<VirtualSoulfind.ShadowIndex.DhtRateLimiter>>(),
                    maxOpsPerMin);
            });
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShardPublisher, VirtualSoulfind.ShadowIndex.ShardPublisher>();
            services.AddHostedService(sp => (VirtualSoulfind.ShadowIndex.ShardPublisher)sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IShardPublisher>());
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShadowIndexQuery, VirtualSoulfind.ShadowIndex.ShadowIndexQuery>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShardMerger, VirtualSoulfind.ShadowIndex.ShardMerger>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IShardCache, VirtualSoulfind.ShadowIndex.ShardCache>();
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IDhtRateLimiter, VirtualSoulfind.ShadowIndex.DhtRateLimiter>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneService, VirtualSoulfind.Scenes.SceneService>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneAnnouncementService>(sp =>
                new VirtualSoulfind.Scenes.SceneAnnouncementService(
                    sp.GetRequiredService<ILogger<VirtualSoulfind.Scenes.SceneAnnouncementService>>(),
                    sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>(),
                    sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtRateLimiter>(),
                    sp.GetRequiredService<Identity.IProfileService>(),
                    sp.GetService<VirtualSoulfind.Scenes.ISceneService>()));
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneMembershipTracker, VirtualSoulfind.Scenes.SceneMembershipTracker>();
            services.AddSingleton<VirtualSoulfind.Scenes.IScenePubSubService>(sp =>
                new VirtualSoulfind.Scenes.ScenePubSubService(
                    sp.GetRequiredService<ILogger<VirtualSoulfind.Scenes.ScenePubSubService>>(),
                    sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>()));
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneJobService, VirtualSoulfind.Scenes.SceneJobService>();
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneChatService>(sp =>
                new VirtualSoulfind.Scenes.SceneChatService(
                    sp.GetRequiredService<ILogger<VirtualSoulfind.Scenes.SceneChatService>>(),
                    sp.GetRequiredService<VirtualSoulfind.Scenes.IScenePubSubService>(),
                    sp.GetRequiredService<IOptionsMonitor<slskd.Options>>(),
                    sp.GetRequiredService<Identity.IProfileService>()));
            services.AddSingleton<VirtualSoulfind.Scenes.ISceneModerationService, VirtualSoulfind.Scenes.SceneModerationService>();
            services.AddSingleton<VirtualSoulfind.DisasterMode.ISoulseekClient>(sp =>
                new VirtualSoulfind.DisasterMode.SoulseekClientWrapper(sp.GetRequiredService<Soulseek.ISoulseekClient>()));
            services.AddSingleton<VirtualSoulfind.DisasterMode.ISoulseekHealthMonitor>(sp =>
                new VirtualSoulfind.DisasterMode.SoulseekHealthMonitor(
                    sp.GetRequiredService<ILogger<VirtualSoulfind.DisasterMode.SoulseekHealthMonitor>>(),
                    sp.GetRequiredService<Soulseek.ISoulseekClient>(),
                    sp.GetRequiredService<IOptionsMonitor<slskd.Options>>()));
            services.AddHostedService(sp => (VirtualSoulfind.DisasterMode.SoulseekHealthMonitor)sp.GetRequiredService<VirtualSoulfind.DisasterMode.ISoulseekHealthMonitor>());
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

            // Register ITransferProgressProxy BEFORE BridgeApi (BridgeApi depends on it)
            services.AddSingleton<VirtualSoulfind.Bridge.IPeerIdAnonymizer, VirtualSoulfind.Bridge.PeerIdAnonymizer>();
            services.AddSingleton<VirtualSoulfind.Bridge.IFilenameGenerator, VirtualSoulfind.Bridge.FilenameGenerator>();
            services.AddSingleton<VirtualSoulfind.Bridge.IRoomSceneMapper, VirtualSoulfind.Bridge.RoomSceneMapper>();
            services.AddSingleton<VirtualSoulfind.Bridge.ITransferProgressProxy, VirtualSoulfind.Bridge.TransferProgressProxy>();
            services.AddSingleton<VirtualSoulfind.Bridge.IBridgeApi, VirtualSoulfind.Bridge.BridgeApi>();
            services.AddSingleton<VirtualSoulfind.Bridge.Protocol.SoulseekProtocolParser>();

            // BridgeProxyServer causes startup deadlock - skip for local dev
            // TODO: Investigate why BridgeProxyServer construction blocks startup
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SLSKDN_E2E_SKIP_BRIDGE_PROXY")) &&
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SLSKDN_ENABLE_BRIDGE_PROXY")))
            {
                services.AddHostedService<VirtualSoulfind.Bridge.Proxy.BridgeProxyServer>();
            }
            else
            {
                Log.Debug("[DI] BridgeProxyServer disabled (set SLSKDN_ENABLE_BRIDGE_PROXY=1 to enable)");
            }

            services.AddSingleton<VirtualSoulfind.Bridge.IBridgeDashboard, VirtualSoulfind.Bridge.BridgeDashboard>();

            // VirtualSoulfind v2 Domain Providers (T-VC02, T-VC03) (IMusicContentDomainProvider in AddAudioCore)
            services.AddSingleton<VirtualSoulfind.Core.GenericFile.IGenericFileContentDomainProvider, VirtualSoulfind.Core.GenericFile.GenericFileContentDomainProvider>();
            services.AddSingleton<VirtualSoulfind.Core.Movie.IMovieContentDomainProvider, VirtualSoulfind.Core.Movie.MovieContentDomainProvider>();
            services.AddSingleton<VirtualSoulfind.Core.Tv.ITvContentDomainProvider, VirtualSoulfind.Core.Tv.TvContentDomainProvider>();
            services.AddSingleton<VirtualSoulfind.Core.Book.IBookContentDomainProvider, VirtualSoulfind.Core.Book.BookContentDomainProvider>();

            // VirtualSoulfind v2 core graph
            services.AddOptions<VirtualSoulfind.v2.VirtualSoulfindV2Options>();
            services.AddOptions<VirtualSoulfind.v2.Resolution.ResolverOptions>();
            services.AddOptions<VirtualSoulfind.v2.Processing.IntentQueueProcessorOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.HttpBackendOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.WebDavBackendOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.S3BackendOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.TorrentBackendOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.MeshDhtBackendOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.LanBackendOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.NativeMeshBackendOptions>();
            services.AddOptions<VirtualSoulfind.v2.Backends.SoulseekBackendOptions>();

            services.AddSingleton<IOptionsMonitor<VirtualSoulfind.v2.Resolution.ResolverOptions>>(sp =>
            {
                var root = sp.GetRequiredService<IOptionsMonitor<Options>>().CurrentValue.VirtualSoulfindV2;
                var wrapped = Microsoft.Extensions.Options.Options.Create(new VirtualSoulfind.v2.Resolution.ResolverOptions
                {
                    MaxConcurrentExecutions = Math.Max(1, root.MaxConcurrentExecutions),
                    DefaultStepTimeoutSeconds = new VirtualSoulfind.v2.Resolution.ResolverOptions().DefaultStepTimeoutSeconds,
                });
                return new Common.Moderation.WrappedOptionsMonitor<VirtualSoulfind.v2.Resolution.ResolverOptions>(wrapped);
            });

            services.AddSingleton<IOptionsMonitor<VirtualSoulfind.v2.Processing.IntentQueueProcessorOptions>>(sp =>
            {
                var root = sp.GetRequiredService<IOptionsMonitor<Options>>().CurrentValue.VirtualSoulfindV2;
                var wrapped = Microsoft.Extensions.Options.Options.Create(new VirtualSoulfind.v2.Processing.IntentQueueProcessorOptions
                {
                    Enabled = root.Enabled,
                    BatchSize = Math.Max(1, root.ProcessorBatchSize),
                    ProcessingIntervalSeconds = Math.Max(1, root.ProcessorIntervalMs / 1000),
                    StartupDelaySeconds = 10,
                });
                return new Common.Moderation.WrappedOptionsMonitor<VirtualSoulfind.v2.Processing.IntentQueueProcessorOptions>(wrapped);
            });

            services.AddSingleton<IOptionsMonitor<VirtualSoulfind.v2.Backends.SoulseekBackendOptions>>(sp =>
            {
                var root = sp.GetRequiredService<IOptionsMonitor<Options>>().CurrentValue.VirtualSoulfindV2;
                var wrapped = Microsoft.Extensions.Options.Options.Create(new VirtualSoulfind.v2.Backends.SoulseekBackendOptions
                {
                    Enabled = root.Enabled,
                    SearchTimeoutSeconds = Math.Max(1, root.Backends.Soulseek.SearchTimeoutMs / 1000),
                    MinimumUploadSpeed = Math.Max(0, root.Backends.Soulseek.MinUploadSpeedBytesPerSec),
                });
                return new Common.Moderation.WrappedOptionsMonitor<VirtualSoulfind.v2.Backends.SoulseekBackendOptions>(wrapped);
            });

            var virtualSoulfindV2CataloguePath = Path.Combine(Program.AppDirectory, "virtualsoulfind-v2-catalogue.db");
            var virtualSoulfindV2SourcesPath = Path.Combine(Program.AppDirectory, "virtualsoulfind-v2-sources.db");

            services.AddSingleton<VirtualSoulfind.v2.Catalogue.ICatalogueStore>(_ =>
                new VirtualSoulfind.v2.Catalogue.SqliteCatalogueStore(virtualSoulfindV2CataloguePath));
            services.AddSingleton<VirtualSoulfind.v2.Sources.ISourceRegistry>(_ =>
                new VirtualSoulfind.v2.Sources.SqliteSourceRegistry($"Data Source={virtualSoulfindV2SourcesPath};"));
            services.AddSingleton<VirtualSoulfind.v2.Intents.IIntentQueue, VirtualSoulfind.v2.Intents.InMemoryIntentQueue>();
            services.AddSingleton<VirtualSoulfind.v2.Matching.IMatchEngine, VirtualSoulfind.v2.Matching.SimpleMatchEngine>();
            services.AddSingleton<VirtualSoulfind.v2.Fingerprinting.IAudioFingerprintService, VirtualSoulfind.v2.Fingerprinting.NoopAudioFingerprintService>();
            services.AddSingleton<VirtualSoulfind.v2.Planning.IPlanner>(sp =>
            {
                var root = sp.GetRequiredService<IOptionsMonitor<Options>>().CurrentValue.VirtualSoulfindV2;
                return new VirtualSoulfind.v2.Planning.MultiSourcePlanner(
                    sp.GetRequiredService<VirtualSoulfind.v2.Catalogue.ICatalogueStore>(),
                    sp.GetRequiredService<VirtualSoulfind.v2.Sources.ISourceRegistry>(),
                    sp.GetRequiredService<IEnumerable<VirtualSoulfind.v2.Backends.IContentBackend>>(),
                    sp.GetRequiredService<Common.Moderation.IModerationProvider>(),
                    sp.GetRequiredService<Common.Moderation.PeerReputationService>(),
                    root.DefaultMode);
            });
            services.AddSingleton<VirtualSoulfind.v2.Resolution.IResolver, VirtualSoulfind.v2.Resolution.SimpleResolver>();
            services.AddSingleton<VirtualSoulfind.v2.Processing.IIntentQueueProcessor, VirtualSoulfind.v2.Processing.IntentQueueProcessor>();
            services.AddSingleton<VirtualSoulfind.v2.Reconciliation.ILibraryReconciliationService, VirtualSoulfind.v2.Reconciliation.LibraryReconciliationService>();
            services.AddSingleton<VirtualSoulfind.v2.Processing.IntentQueueProcessorBackgroundService>();
            services.AddHostedService(sp => sp.GetRequiredService<VirtualSoulfind.v2.Processing.IntentQueueProcessorBackgroundService>());

            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.LocalLibraryBackend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.NativeMeshBackend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.MeshDhtBackend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.HttpBackend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.WebDavBackend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.S3Backend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.TorrentBackend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.LanBackend>();
            services.AddSingleton<VirtualSoulfind.v2.Backends.IContentBackend, VirtualSoulfind.v2.Backends.SoulseekBackend>();

            // Content Domain Provider Registry (P3: Custom Domain Matching Logic)
            services.AddContentDomainProviders();

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
                Log.Debug("[DI] Constructing PodPublisher...");
                Log.Debug("[DI] Resolving IMeshDhtClient for PodPublisher...");
                var dht = sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>();
                Log.Debug("[DI] Resolving IServiceScopeFactory for PodPublisher...");
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                Log.Debug("[DI] Resolving ILogger<PodPublisher> for PodPublisher...");
                var logger = sp.GetRequiredService<ILogger<PodCore.PodPublisher>>();
                Log.Debug("[DI] All PodPublisher dependencies resolved, creating instance...");
                var service = new PodCore.PodPublisher(dht, scopeFactory, logger);
                Log.Debug("[DI] PodPublisher constructed");
                return service;
            });
            services.AddSingleton<PodCore.IPodDiscovery, PodCore.PodDiscovery>();

            // Peer resolution service (for PeerReputation lookup)
            services.AddSingleton<PodCore.IPeerResolutionService, PodCore.PeerResolutionService>();

            // Soulseek chat bridge
            services.AddSingleton<PodCore.ISoulseekChatBridge>(sp =>
            {
                Log.Debug("[DI] Constructing SoulseekChatBridge...");
                Log.Debug("[DI] Resolving IServiceScopeFactory for SoulseekChatBridge...");
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                Log.Debug("[DI] Resolving IRoomService for SoulseekChatBridge...");
                var roomService = sp.GetRequiredService<IRoomService>();
                Log.Debug("[DI] Resolving ISoulseekClient for SoulseekChatBridge...");
                var soulseekClient = sp.GetRequiredService<ISoulseekClient>();
                Log.Debug("[DI] Resolving ILogger<SoulseekChatBridge> for SoulseekChatBridge...");
                var logger = sp.GetRequiredService<ILogger<PodCore.SoulseekChatBridge>>();
                Log.Debug("[DI] All SoulseekChatBridge dependencies resolved, creating instance...");
                var service = new PodCore.SoulseekChatBridge(scopeFactory, roomService, soulseekClient, logger);
                Log.Debug("[DI] SoulseekChatBridge constructed");
                return service;
            });

            // Main pod service (SQLite-backed with persistence)
            services.AddSingleton<PodCore.IPodService>(sp =>
            {
                Log.Debug("[DI] Constructing SqlitePodService...");
                Log.Debug("[DI] Resolving IDbContextFactory<PodDbContext> for SqlitePodService...");
                var factory = sp.GetRequiredService<IDbContextFactory<PodCore.PodDbContext>>();
                Log.Debug("[DI] Resolving IPodPublisher for SqlitePodService (optional)...");
                var podPublisher = sp.GetRequiredService<PodCore.IPodPublisher>();
                Log.Debug("[DI] Resolving IPodMembershipSigner for SqlitePodService (optional)...");
                var membershipSigner = sp.GetRequiredService<PodCore.IPodMembershipSigner>();
                Log.Debug("[DI] Resolving ILogger<SqlitePodService> for SqlitePodService...");
                var logger = sp.GetRequiredService<ILogger<PodCore.SqlitePodService>>();
                Log.Debug("[DI] Resolving IServiceScopeFactory for SqlitePodService (for lazy IContentLinkService resolution)...");
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                Log.Debug("[DI] All SqlitePodService dependencies resolved, creating instance (IContentLinkService will be resolved lazily via scope)...");
                var service = new PodCore.SqlitePodService(factory, podPublisher, membershipSigner, logger, scopeFactory);
                Log.Debug("[DI] SqlitePodService constructed");
                return service;
            });

            services.AddSingleton<PodCore.GoldStarClubService>();
            services.AddSingleton<PodCore.IGoldStarClubService>(sp => sp.GetRequiredService<PodCore.GoldStarClubService>());
            services.AddHostedService(sp => sp.GetRequiredService<PodCore.GoldStarClubService>());

            // Pod messaging service (SQLite-backed)
            services.AddScoped<PodCore.IPodMessaging>(sp =>
            {
                var factory = sp.GetRequiredService<IDbContextFactory<PodCore.PodDbContext>>();
                var dbContext = factory.CreateDbContext();
                return new PodCore.SqlitePodMessaging(
                    dbContext,
                    sp.GetRequiredService<ILogger<PodCore.SqlitePodMessaging>>(),
                    sp.GetRequiredService<PodCore.IPodMessageRouter>());
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
                var musicBrainzClient = sp.GetRequiredService<Integrations.MusicBrainz.IMusicBrainzClient>();
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
                var podService = sp.GetRequiredService<PodCore.IPodService>();
                var profileService = sp.GetRequiredService<Identity.IProfileService>();
                return new PodCore.PodMessageBackfill(
                    messageStorage,
                    messageRouter,
                    overlayClient,
                    podService,
                    profileService,
                    sp.GetRequiredService<ILogger<PodCore.PodMessageBackfill>>());
            });

            // Pod opinion service for managing content variant opinions
            services.AddScoped<PodCore.IPodOpinionService>(sp =>
            {
                var podService = sp.GetRequiredService<PodCore.IPodService>();
                var dhtClient = sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>();
                return new PodCore.PodOpinionService(
                    podService,
                    dhtClient,
                    sp.GetRequiredService<Mesh.Transport.Ed25519Signer>(),
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
                Log.Debug("[DI] Constructing PodPublisherBackgroundService hosted service...");
                var service = ActivatorUtilities.CreateInstance<PodCore.PodPublisherBackgroundService>(p);
                Log.Debug("[DI] PodPublisherBackgroundService constructed");
                return service;
            });

            // Typed options (Phase 11) - bind under slskd: namespace to match YAML provider
            var slskdSection = Configuration.GetSection(AppName);
            services.AddOptions<Core.SwarmOptions>().Bind(slskdSection.GetSection("Swarm"));
            services.AddOptions<Core.SecurityOptions>().Bind(slskdSection.GetSection("Security"));
            services.AddOptions<Common.Security.AdversarialOptions>().Bind(slskdSection.GetSection("Security:Adversarial"));
            services.AddOptions<PodCore.PodMessageSignerOptions>().Bind(slskdSection.GetSection("PodCore:Security"));
            services.AddOptions<PodCore.PodJoinOptions>().Bind(slskdSection.GetSection("PodCore:Join"));

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

            services.AddOptions<Core.BrainzOptions>().Bind(Configuration.GetSlskdSection("Brainz"));
            services.AddOptions<Mesh.MeshOptions>().Bind(Configuration.GetSlskdSection("Mesh")); // transport prefs
            services.AddOptions<Mesh.MeshSyncSecurityOptions>().Bind(Configuration.GetSlskdSection("Mesh:SyncSecurity"));
            services.AddOptions<Mesh.MeshTransportOptions>().Bind(Configuration.GetSlskdSection("Mesh:Transport"));
            services.AddOptions<Mesh.TorTransportOptions>().Bind(Configuration.GetSlskdSection("Mesh:Transport:Tor"));
            services.AddOptions<Mesh.I2PTransportOptions>().Bind(Configuration.GetSlskdSection("Mesh:Transport:I2P"));
            services.AddOptions<Common.Security.WebSocketTransportOptions>().Bind(Configuration.GetSlskdSection("Security:Adversarial:Transport:WebSocket"));
            services.AddOptions<Common.Security.HttpTunnelTransportOptions>().Bind(Configuration.GetSlskdSection("Security:Adversarial:Transport:HttpTunnel"));
            services.AddOptions<Common.Security.Obfs4TransportOptions>().Bind(Configuration.GetSlskdSection("Security:Adversarial:Transport:Obfs4"));
            services.AddOptions<Common.Security.MeekTransportOptions>().Bind(Configuration.GetSlskdSection("Security:Adversarial:Transport:Meek"));

            // Register options as singletons for direct injection (temporary workaround)
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Mesh.TorTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Mesh.I2PTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.WebSocketTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.HttpTunnelTransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.Obfs4TransportOptions>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Common.Security.MeekTransportOptions>>().Value);
            services.AddOptions<MediaCore.MediaCoreOptions>().Bind(Configuration.GetSlskdSection("MediaCore"));
            services.AddOptions<Mesh.Overlay.OverlayOptions>().Bind(Configuration.GetSlskdSection("Overlay"));
            services.AddOptions<Mesh.Overlay.DataOverlayOptions>().Bind(Configuration.GetSlskdSection("OverlayData"));
            services.PostConfigure<Mesh.MeshOptions>(options =>
            {
                options.DataDirectory = ResolveAppRelativePath(options.DataDirectory, "data");
            });
            services.PostConfigure<Mesh.Overlay.OverlayOptions>(options =>
            {
                options.KeyPath = ResolveAppRelativePath(options.KeyPath, "mesh-overlay.key");
            });
            services.AddOptions<Mesh.ServiceFabric.MeshGatewayOptions>().Bind(Configuration.GetSlskdSection("MeshGateway"));

            // Realm services (T-REALM-01, T-REALM-02, T-REALM-04)
            Log.Debug("[DI] Configuring Realm services...");
            services.Configure<Mesh.Realm.RealmConfig>(Configuration.GetSlskdSection("Realm"));
            services.Configure<Mesh.Realm.MultiRealmConfig>(Configuration.GetSlskdSection("MultiRealm"));
            services.AddRealmServices();

            // Social federation services (required by bridges)
            Log.Debug("[DI] Configuring Social Federation services...");
            services.AddSocialFederation();
            services.AddBridgeServices();

            // Governance and Gossip services (T-REALM-03)
            Log.Debug("[DI] Configuring Governance and Gossip services...");
            services.AddGovernanceServices();
            services.AddGossipServices();

            // MeshCore (Phase 8 implementation)
            Log.Debug("[DI] Configuring MeshCore services...");
            services.Configure<Mesh.MeshOptions>(Configuration.GetSlskdSection("Mesh"));
            services.AddSingleton<Mesh.INatDetector, Mesh.StunNatDetector>();
            services.AddSingleton<Mesh.Nat.IUdpHolePuncher, Mesh.Nat.UdpHolePuncher>();
            services.AddSingleton<Mesh.Nat.IRelayClient, Mesh.Nat.RelayClient>();
            services.AddSingleton<Mesh.Nat.INatTraversalService, Mesh.Nat.NatTraversalService>();

            // DHT: use in-memory Kademlia-style implementation for now
            services.AddSingleton<VirtualSoulfind.ShadowIndex.IDhtClient>(sp =>
            {
                Log.Debug("[DI] Constructing InMemoryDhtClient...");
                Log.Debug("[DI] Resolving ILogger<InMemoryDhtClient>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.InMemoryDhtClient>>();
                Log.Debug("[DI] Resolving IOptions<MeshOptions> for InMemoryDhtClient...");
                var options = sp.GetRequiredService<IOptions<Mesh.MeshOptions>>();
                Log.Debug("[DI] Resolving MeshStatsCollector for InMemoryDhtClient (optional)...");
                var statsCollector = sp.GetRequiredService<Mesh.MeshStatsCollector>();
                Log.Debug("[DI] All InMemoryDhtClient dependencies resolved, creating instance...");
                var service = new Mesh.Dht.InMemoryDhtClient(logger, options, statsCollector);
                Log.Debug("[DI] InMemoryDhtClient constructed");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IMeshDhtClient>(sp =>
            {
                Log.Debug("[DI] Constructing MeshDhtClient...");
                Log.Debug("[DI] Resolving ILogger<MeshDhtClient>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.MeshDhtClient>>();
                Log.Debug("[DI] Resolving IDhtClient for MeshDhtClient...");
                var dhtClient = sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>();
                Log.Debug("[DI] All MeshDhtClient dependencies resolved, creating instance (DhtService will be resolved lazily to break circular dependency)...");
                var service = new Mesh.Dht.MeshDhtClient(logger, dhtClient, sp, sp.GetService<IOptions<Mesh.MeshOptions>>());
                Log.Debug("[DI] MeshDhtClient constructed");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IPeerDescriptorPublisher>(sp =>
            {
                Log.Debug("[DI] Constructing PeerDescriptorPublisher...");
                var service = new Mesh.Dht.PeerDescriptorPublisher(
                    sp.GetRequiredService<ILogger<Mesh.Dht.PeerDescriptorPublisher>>(),
                    sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>(),
                    sp.GetRequiredService<IOptions<Mesh.MeshOptions>>(),
                    sp.GetRequiredService<Mesh.INatDetector>(),
                    sp.GetRequiredService<IOptions<Mesh.MeshTransportOptions>>(),
                    sp.GetRequiredService<IOptions<Mesh.Overlay.OverlayOptions>>(),
                    sp.GetRequiredService<Mesh.Transport.DescriptorSigningService>(),
                    sp.GetService<Mesh.Overlay.IKeyStore>());
                Log.Debug("[DI] PeerDescriptorPublisher constructed");
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
                Log.Debug("[DI] Constructing MeshStatsCollector...");
                var service = new Mesh.MeshStatsCollector(
                    sp.GetRequiredService<ILogger<Mesh.MeshStatsCollector>>(),
                    sp);
                Log.Debug("[DI] MeshStatsCollector constructed");
                return service;
            });
            services.AddSingleton<Mesh.IMeshStatsCollector>(sp => sp.GetRequiredService<Mesh.MeshStatsCollector>());
            services.AddHostedService(p =>
            {
                Log.Debug("[DI] Resolving MeshBootstrapService hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Bootstrap.MeshBootstrapService>(p);
                Log.Debug("[DI] MeshBootstrapService hosted service resolved");
                return service;
            });
            services.AddHostedService(p =>
            {
                Log.Debug("[DI] Resolving PeerDescriptorRefreshService hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Dht.PeerDescriptorRefreshService>(p);
                Log.Debug("[DI] PeerDescriptorRefreshService hosted service resolved");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IContentPeerPublisher>(sp =>
            {
                Log.Debug("[DI] Constructing ContentPeerPublisher...");
                Log.Debug("[DI] Resolving ILogger<ContentPeerPublisher>...");
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Dht.ContentPeerPublisher>>();
                Log.Debug("[DI] Resolving IMeshDhtClient for ContentPeerPublisher...");
                var dht = sp.GetRequiredService<Mesh.Dht.IMeshDhtClient>();
                Log.Debug("[DI] Resolving IOptions<MeshOptions> for ContentPeerPublisher...");
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.MeshOptions>>();
                Log.Debug("[DI] All ContentPeerPublisher dependencies resolved, creating instance...");
                var service = new Mesh.Dht.ContentPeerPublisher(logger, dht, options);
                Log.Debug("[DI] ContentPeerPublisher constructed");
                return service;
            });
            services.AddSingleton<Mesh.Dht.IContentPeerHintService>(sp =>
            {
                Log.Debug("[DI] Constructing ContentPeerHintService...");
                var service = new Mesh.Dht.ContentPeerHintService(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Dht.ContentPeerHintService>>(),
                    sp.GetRequiredService<Mesh.Dht.IContentPeerPublisher>());
                Log.Debug("[DI] ContentPeerHintService constructed");
                return service;
            });
            services.AddHostedService(sp => (Mesh.Dht.ContentPeerHintService)sp.GetRequiredService<Mesh.Dht.IContentPeerHintService>());
            services.AddSingleton<Mesh.Health.IMeshHealthService, Mesh.Health.MeshHealthService>();

            // Service Fabric (client + directory + validation)
            services.AddSingleton<Mesh.ServiceFabric.IMeshServiceDescriptorValidator, Mesh.ServiceFabric.MeshServiceDescriptorValidator>();
            services.AddSingleton<Mesh.ServiceFabric.IMeshServiceDirectory, Mesh.ServiceFabric.DhtMeshServiceDirectory>();
            services.AddSingleton<Mesh.ServiceFabric.IMeshServiceClient, Mesh.ServiceFabric.MeshServiceClient>();
            services.AddOptions<Mesh.ServiceFabric.MeshServiceFabricOptions>().Bind(Configuration.GetSlskdSection("MeshServiceFabric"));
            services.AddSingleton<Mesh.ServiceFabric.MeshServiceRouter>();

            // MeshContentFetcher requires IMeshServiceClient, so register after it
            services.AddSingleton<IMeshContentFetcher, MeshContentFetcher>();

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
                Log.Debug("[DI] Constructing KademliaRpcClient...");
                Log.Debug("[DI] Resolving ILogger<KademliaRpcClient>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.KademliaRpcClient>>();
                Log.Debug("[DI] Resolving IMeshServiceClient for KademliaRpcClient...");
                var meshClient = sp.GetRequiredService<Mesh.ServiceFabric.IMeshServiceClient>();
                Log.Debug("[DI] Resolving KademliaRoutingTable for KademliaRpcClient...");
                var routingTable = sp.GetRequiredService<Mesh.Dht.KademliaRoutingTable>();
                Log.Debug("[DI] Resolving IDhtClient for KademliaRpcClient...");
                var dhtClient = sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>();
                Log.Debug("[DI] All KademliaRpcClient dependencies resolved, creating instance...");
                var service = new Mesh.Dht.KademliaRpcClient(logger, meshClient, routingTable, dhtClient);
                Log.Debug("[DI] KademliaRpcClient constructed");
                return service;
            });
            services.AddSingleton<Mesh.ServiceFabric.Services.DhtMeshService>();
            services.AddSingleton<Mesh.Dht.DhtService>(sp =>
            {
                Log.Debug("[DI] Constructing DhtService...");
                Log.Debug("[DI] Resolving ILogger<DhtService>...");
                var logger = sp.GetRequiredService<ILogger<Mesh.Dht.DhtService>>();
                Log.Debug("[DI] Resolving KademliaRoutingTable for DhtService...");
                var routingTable = sp.GetRequiredService<Mesh.Dht.KademliaRoutingTable>();
                Log.Debug("[DI] Resolving IDhtClient for DhtService...");
                var dhtClient = sp.GetRequiredService<VirtualSoulfind.ShadowIndex.IDhtClient>();
                Log.Debug("[DI] Resolving KademliaRpcClient for DhtService...");
                var rpcClient = sp.GetRequiredService<Mesh.Dht.KademliaRpcClient>();
                Log.Debug("[DI] Resolving IMeshMessageSigner for DhtService...");
                var messageSigner = sp.GetRequiredService<Mesh.IMeshMessageSigner>();
                Log.Debug("[DI] All DhtService dependencies resolved, creating instance...");
                var service = new Mesh.Dht.DhtService(logger, routingTable, dhtClient, rpcClient, messageSigner);
                Log.Debug("[DI] DhtService constructed");
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
                Log.Debug("[DI] Constructing CircuitMaintenanceService hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.CircuitMaintenanceService>(p);
                Log.Debug("[DI] CircuitMaintenanceService constructed");
                return service;
            });

            // Transport dialers (Tor/I2P integration Phase 2)
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows())
            {
                services.AddSingleton<Mesh.Transport.ITransportDialer, Mesh.Transport.DirectQuicDialer>();
            }

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
                Log.Debug("[DI] Constructing UdpOverlayServer hosted service...");
                var service = ActivatorUtilities.CreateInstance<Mesh.Overlay.UdpOverlayServer>(p);
                Log.Debug("[DI] UdpOverlayServer constructed");
                return service;
            });
            var overlayOptionsAtStartup = Configuration.GetSlskdSection("Overlay").Get<Mesh.Overlay.OverlayOptions>() ?? new Mesh.Overlay.OverlayOptions();
            var dataOverlayOptionsAtStartup = Configuration.GetSlskdSection("OverlayData").Get<Mesh.Overlay.DataOverlayOptions>() ?? new Mesh.Overlay.DataOverlayOptions();
            var quicPlatformSupported = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();
            var quicRuntimeAvailable = quicPlatformSupported && Mesh.QuicRuntime.IsAvailable();
            var quicOverlayRequested = overlayOptionsAtStartup.Enable && overlayOptionsAtStartup.EnableQuic;
            var quicDataRequested = dataOverlayOptionsAtStartup.Enable;

            if (quicOverlayRequested && quicRuntimeAvailable)
            {
#pragma warning disable CA1416 // Runtime platform guards apply in this branch
                services.AddHostedService(p =>
                {
                    Log.Debug("[DI] Constructing QuicOverlayServer hosted service...");
                    var service = CreateQuicOverlayServer(p);
                    Log.Debug("[DI] QuicOverlayServer constructed");
                    return service;
                });
#pragma warning restore CA1416
            }
            else if (quicOverlayRequested)
            {
                Log.Warning("[DI] QUIC overlay requested but runtime/platform support is unavailable; skipping QuicOverlayServer hosted service");
            }
            else
            {
                Log.Debug("[DI] QUIC overlay disabled by configuration; skipping QuicOverlayServer hosted service");
            }

            if (quicOverlayRequested && quicRuntimeAvailable)
            {
#pragma warning disable CA1416 // Runtime platform guards apply in this branch.
                services.AddSingleton<Mesh.Overlay.IOverlayClient>(sp =>
                {
                    return CreateQuicOverlayClient(sp);
                });
#pragma warning restore CA1416
            }
            else
            {
                services.AddSingleton<Mesh.Overlay.IOverlayClient>(sp =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Overlay.UdpOverlayClient>>();
                    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.Overlay.OverlayOptions>>();
                    var privacyLayer = sp.GetService<Mesh.Privacy.IPrivacyLayer>();
                    return new Mesh.Overlay.UdpOverlayClient(logger, options, privacyLayer);
                });
            }

            if (quicDataRequested && quicRuntimeAvailable)
            {
#pragma warning disable CA1416 // Runtime platform guards apply in this branch.
                services.AddSingleton<Mesh.Overlay.IOverlayDataPlane>(sp => CreateQuicDataClient(sp));
#pragma warning restore CA1416
            }

            if (quicDataRequested && quicRuntimeAvailable)
            {
                services.AddHostedService(p =>
                {
                    Log.Debug("[DI] Constructing QuicDataServer hosted service...");
                    var service = ActivatorUtilities.CreateInstance<Mesh.Overlay.QuicDataServer>(p);
                    Log.Debug("[DI] QuicDataServer constructed");
                    return service;
                });
            }
            else if (quicDataRequested)
            {
                Log.Warning("[DI] QUIC data overlay requested but runtime/platform support is unavailable; skipping QuicDataServer hosted service");
            }
            else
            {
                Log.Debug("[DI] QUIC data overlay disabled by configuration; skipping QuicDataServer hosted service");
            }

            // MediaCore publisher
            services.AddHostedService(p =>
            {
                Log.Debug("[DI] Constructing ContentPublisherService hosted service...");
                var service = ActivatorUtilities.CreateInstance<MediaCore.ContentPublisherService>(p);
                Log.Debug("[DI] ContentPublisherService constructed");
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
            services.AddSingleton<MeshOverlayRequestRouter>();
            services.AddSingleton<DhtRendezvous.Search.IMeshSearchRpcHandler, DhtRendezvous.Search.MeshSearchRpcHandler>();
            services.AddSingleton<DhtRendezvous.Search.IMeshOverlaySearchService, DhtRendezvous.Search.MeshOverlaySearchService>();
            services.AddSingleton<IMeshOverlayServer, MeshOverlayServer>();
            services.AddSingleton<IMeshOverlayConnector, MeshOverlayConnector>();
            services.AddHostedService<MeshNeighborPeerSyncService>();

            services.AddSingleton<IDhtRendezvousService, DhtRendezvousService>();
            services.AddHostedService(p =>
            {
                Log.Debug("[DI] Resolving DhtRendezvousService hosted service...");
                var service = (DhtRendezvousService)p.GetRequiredService<IDhtRendezvousService>();
                Log.Debug("[DI] DhtRendezvousService hosted service resolved");
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
            services.AddSingleton<Transfers.MultiSource.Analytics.ISwarmAnalyticsService, Transfers.MultiSource.Analytics.SwarmAnalyticsService>();
            services.AddSingleton<Transfers.MultiSource.Discovery.IAdvancedDiscoveryService, Transfers.MultiSource.Discovery.AdvancedDiscoveryService>();
            services.AddSingleton<IAcceleratedDownloadService, AcceleratedDownloadService>();
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
                var options = sp.GetRequiredService<IOptionsMonitor<slskd.Options>>();
                bool enableCostBasedScheduling = options.CurrentValue.Global.Download.CostBasedScheduling;
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
            services.AddSingleton<SourceFeeds.ISpotifyConnectionService, SourceFeeds.SpotifyConnectionService>();
            services.AddSingleton<SourceFeeds.ISourceFeedImportService, SourceFeeds.SourceFeedImportService>();

            // Auto-replace services
            services.AddSingleton<Transfers.AutoReplace.IAutoReplaceService, Transfers.AutoReplace.AutoReplaceService>();
            services.AddSingleton<Transfers.AutoReplace.AutoReplaceBackgroundService>();
            services.AddHostedService(provider => provider.GetRequiredService<Transfers.AutoReplace.AutoReplaceBackgroundService>());

            services.AddSingleton<IRelayService, RelayService>();

            // HARDENING-2026-04-20 H8: loud, periodic reminder when relay controller TLS validation is reduced.
            services.AddHostedService<Relay.RelayTlsWarningService>();

            // HARDENING-2026-04-20 H12: loud, periodic reminder that public DHT rendezvous publishes this node's IP.
            services.AddHostedService<DhtRendezvous.DhtExposureWarningService>();

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
            services.AddSingleton<ISongIdRunStore, SongIdRunStore>();
            services.AddSingleton<ISongIdService, SongIdService>();
            services.AddSingleton<DiscoveryGraph.IDiscoveryGraphService, DiscoveryGraph.DiscoveryGraphService>();
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
            services.AddSingleton<Sharing.ShareGrantAnnouncementService>();

            // Best-effort schema upgrade for sharing db (EnsureCreated does not apply schema changes)
            try
            {
                using (var collectionsContext = new Sharing.CollectionsDbContext(
                    new DbContextOptionsBuilder<Sharing.CollectionsDbContext>()
                        .UseSqlite($"Data Source={collectionsDbPath}")
                        .Options))
                {
                    collectionsContext.Database.ExecuteSqlRaw("ALTER TABLE ShareGrants ADD COLUMN OwnerEndpoint TEXT");
                }
            }
            catch
            {
                // Column already exists or DB is read-only; ignore.
            }

            try
            {
                using (var collectionsContext = new Sharing.CollectionsDbContext(
                    new DbContextOptionsBuilder<Sharing.CollectionsDbContext>()
                        .UseSqlite($"Data Source={collectionsDbPath}")
                        .Options))
                {
                    collectionsContext.Database.ExecuteSqlRaw("ALTER TABLE ShareGrants ADD COLUMN ShareToken TEXT");
                }
            }
            catch
            {
                // Column already exists or DB is read-only; ignore.
            }

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

            // Solid / WebID / Solid-OIDC (optional; gated per-request by Feature.Solid)
            services.AddSingleton<slskd.Solid.ISolidClientIdDocumentService, slskd.Solid.SolidClientIdDocumentService>();
            services.AddSingleton<slskd.Solid.ISolidWebIdResolver, slskd.Solid.SolidWebIdResolver>();
            services.AddSingleton<slskd.Solid.ISolidFetchPolicy, slskd.Solid.SolidFetchPolicy>();

            // Security services (zero-trust hardening)
            Log.Debug("[DI] About to call AddSlskdnSecurity...");
            services.AddSlskdnSecurity(Configuration);
            Log.Debug("[DI] AddSlskdnSecurity completed");

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
            Log.Debug("[ASP] Starting ConfigureAspDotNetServices...");

            services.AddCors(options =>
            {
                var c = OptionsAtStartup.Web.Cors;
                if (c.Enabled && c.AllowedOrigins != null && c.AllowedOrigins.Length > 0)
                {
                    options.AddPolicy("ConfiguredCors", b =>
                    {
                        // Handle wildcard origin for E2E tests (when credentials are disabled)
                        var hasWildcard = c.AllowedOrigins.Contains("*") || c.AllowedOrigins.Contains("/*");
                        if (hasWildcard && !c.AllowCredentials)
                        {
                            // E2E tests: allow any origin (no credentials)
                            b.AllowAnyOrigin();
                        }
                        else
                        {
                            b.WithOrigins(c.AllowedOrigins);
                            if (c.AllowCredentials)
                            {
                                b.AllowCredentials();
                            }
                        }

                        b.WithExposedHeaders("X-URL-Base", "X-Total-Count")
                            .SetPreflightMaxAge(TimeSpan.FromHours(1));
                        if (c.AllowedHeaders != null && c.AllowedHeaders.Length > 0)
                        {
                            b.WithHeaders(c.AllowedHeaders);
                        }
                        else
                        {
                            b.AllowAnyHeader();
                        }

                        if (c.AllowedMethods != null && c.AllowedMethods.Length > 0)
                        {
                            b.WithMethods(c.AllowedMethods);
                        }
                        else
                        {
                            b.AllowAnyMethod();
                        }
                    });
                }
            });

            // note: don't dispose this (or let it be disposed) or some of the stats, like those related
            // to the thread pool won't work
            DotNetRuntimeStats = DotNetRuntimeStatsBuilder.Default().StartCollecting();
            services.AddSystemMetrics();

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(DataDirectory, "misc", ".DataProtection-Keys")));

            // LOW-02: SHA256-hash the configured key so the signing key is always 32 raw bytes of key material
            // regardless of the string's encoding width, avoiding weak keys from short UTF-8 strings.
            var jwtSigningKey = new SymmetricSecurityKey(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(OptionsAtStartup.Web.Authentication.Jwt.Key)));

            // JwtOptions.Key defaults to a freshly generated random value when unset in config, so we
            // can't distinguish "configured" from "ephemeral" by inspecting the Options object itself.
            // Check the raw configuration tree instead — the warning only fires when no value was
            // actually provided by the user.
            var jwtKeyConfigured = !string.IsNullOrWhiteSpace(Configuration?["slskd:web:authentication:jwt:key"])
                || !string.IsNullOrWhiteSpace(Configuration?["web:authentication:jwt:key"])
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable($"{EnvironmentVariablePrefix}JWT_KEY"));

            if (!jwtKeyConfigured)
            {
                Log.Warning("JWT signing key is ephemeral (auto-generated per-process start). All sessions will be invalidated on restart. Set web.authentication.jwt.key in configuration to persist sessions.");
            }

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
                            ValidateAudience = true,
                            ValidAudiences = new[] { AppName },
                            IssuerSigningKey = jwtSigningKey,
                            ValidateIssuerSigningKey = true,
                        };

                        options.Events = new JwtBearerEvents
                        {
                            OnTokenValidated = context =>
                            {
                                // HIGH-04: check jti deny-list to support token revocation (e.g. logout)
                                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                                if (!string.IsNullOrEmpty(jti))
                                {
                                    var security = context.HttpContext.RequestServices.GetService<ISecurityService>();
                                    if (security?.IsTokenRevoked(jti) == true)
                                    {
                                        context.Fail("Token has been revoked");
                                    }
                                }

                                return Task.CompletedTask;
                            },
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
                                            var service = context.HttpContext.RequestServices.GetRequiredService<ISecurityService>();
                                            var remoteIpAddress = context.HttpContext.Connection.RemoteIpAddress;
                                            if (string.IsNullOrWhiteSpace(token) || remoteIpAddress == null)
                                            {
                                                throw new InvalidOperationException("API key token or caller IP address was unavailable.");
                                            }

                                            var (name, role, scopes) = service.AuthenticateWithApiKey(token, callerIpAddress: remoteIpAddress);

                                            // the API key is valid. create a new, short lived jwt for the key name and role.
                                            // HARDENING-2026-04-20 H13: propagate the key's scopes onto the promoted JWT so
                                            // RequireScopeAttribute works whether the caller presented an API key or a JWT.
                                            context.Token = service.GenerateJwt(name, role, ttl: 1000, scopes: scopes).Serialize();
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
                // Multi-instance (E2E) runs multiple nodes on the same host with different ports.
                // Cookies are host-scoped (not port-scoped), so both the antiforgery cookie token and the
                // JS-readable request-token cookie need stable per-port names that do not collide with each other.
                options.Cookie.Name = $"XSRF-COOKIE-{OptionsAtStartup.Web.Port}";
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
            services.AddProblemDetails();

            services.AddControllers(options =>
                {
                    options.Filters.Add(new AuthorizeFilter(AuthPolicy.Any));
                })
                .ConfigureApplicationPartManager(manager =>
                {
                    // Replace the default ControllerFeatureProvider with a resilient one that
                    // handles Assembly.GetTypes() failures for build-time-only dependencies.
                    // Needed because MSBuild task classes in this assembly inherit from
                    // Microsoft.Build.Utilities.Task, and patched Microsoft.Build.Utilities.Core
                    // (18.x+) targets net9.0+ so its runtime DLL is absent from a net8.0 output.
                    var existing = manager.FeatureProviders
                        .OfType<IApplicationFeatureProvider<ControllerFeature>>().ToList();
                    foreach (var p in existing)
                    {
                        manager.FeatureProviders.Remove(p);
                    }

                    manager.FeatureProviders.Add(new slskd.Common.CodeQuality.SafeControllerFeatureProvider());
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.SuppressInferBindingSourcesForParameters = true; // explicit [FromRoute], etc
                    options.SuppressMapClientErrors = true; // disables automatic ProblemDetails for 4xx

                    // PR-07: when EnforceSecurity, enable automatic 400 for invalid model (ValidationProblemDetails)
                    options.SuppressModelStateInvalidFilter = false;
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
                .AddMeshHealthCheck(
                    name: "mesh",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, // Don't fail entire health endpoint if mesh isn't ready
                    tags: new[] { "mesh", "network", "dht" },
                    timeout: TimeSpan.FromSeconds(5)); // 5 second timeout to prevent hanging

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
                        var path = context.Request.Path.Value ?? string.Empty;
                        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                        if (path.StartsWith("/mesh/", StringComparison.OrdinalIgnoreCase))
                        {
                            return RateLimitPartition.GetFixedWindowLimiter("mesh:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = meshPermit, Window = meshWindow });
                        }

                        if (path.Contains("/inbox", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            return RateLimitPartition.GetFixedWindowLimiter("fed:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = fedPermit, Window = fedWindow });
                        }

                        if (context.Request.Headers.ContainsKey("Authorization") || context.Request.Headers.ContainsKey("X-API-Key"))
                        {
                            return RateLimitPartition.GetNoLimiter("authenticated");
                        }

                        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                        {
                            return RateLimitPartition.GetNoLimiter("web");
                        }

                        return RateLimitPartition.GetFixedWindowLimiter("api:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = apiPermit, Window = apiWindow });
                    });
                });
            }

            if (OptionsAtStartup.Feature.Swagger)
            {
                services.AddSwaggerGen(options =>
                {
                    options.DescribeAllParametersInCamelCase();

                    // Use fully-qualified type name as schema ID to prevent conflicts between
                    // types with the same short name in different namespaces (e.g. slskd.Search.File
                    // vs Soulseek.File both map to "File" by default, crashing Swagger generation).
                    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
                    options.SwaggerDoc("v0", new OpenApiInfo
                    {
                        Version = "v0",
                        Title = "slskdN API",
                        Description = "slskdN is an unofficial fork of slskd for the Soulseek community service network",
                        Contact = new OpenApiContact
                        {
                            Name = "slskdN on GitHub",
                            Url = new Uri("https://github.com/snapetech/slskdn"),
                        },
                        License = new OpenApiLicense
                        {
                            Name = "AGPL-3.0 license",
                            Url = new Uri("https://github.com/snapetech/slskdn/blob/main/LICENSE"),
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
            Log.Debug("[Pipeline] Starting ConfigureAspDotNetPipeline...");

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
            {
                app.UseCors("ConfiguredCors");
            }

            if (OptionsAtStartup.Web.Https.Force)
            {
                app.UseHttpsRedirection();
                app.UseHsts();

                Log.Information($"Forcing HTTP requests to HTTPS");
            }

            // Security middleware (rate limiting, violation tracking, etc.)
            // MUST be FIRST in pipeline (before UsePathBase) to catch path traversal and other attacks
            // This ensures we check the raw request path before any path rewriting occurs
            Log.Debug("[Pipeline] About to call UseSlskdnSecurity (FIRST in pipeline)...");
            app.UseSlskdnSecurity();
            Log.Debug("[Pipeline] UseSlskdnSecurity completed");

            // allow users to specify a custom path base, for use behind a reverse proxy
            var urlBase = OptionsAtStartup.Web.UrlBase;
            urlBase = urlBase.StartsWith("/") ? urlBase : "/" + urlBase;

            // use urlBase. this effectively just removes urlBase from the path.
            // inject urlBase into any html files we serve, and rewrite links to ./static or /static to
            // prepend the url base.
            app.UsePathBase(urlBase);
            foreach (var (pattern, replacement) in CreateWebHtmlRewriteRules(urlBase))
            {
                app.UseHTMLRewrite(pattern, replacement);
            }

            // The main fix is making HTTP_ADDRESS configurable for proper binding
            app.UseHTMLInjection($"<script>window.urlBase=\"{urlBase}\";window.port={OptionsAtStartup.Web.Port}</script>", excludedRoutes: new[] { "/api", "/swagger" });
            app.UseAuthentication();

            // CSRF token middleware - generates tokens for cookie-based auth
            // This must run after path-base rewriting and after authentication so tokens are bound to the
            // principal that will later be used for validation on state-changing requests.
            app.Use(async (context, next) =>
            {
                // Log all requests to MediaCore endpoints for debugging
                var path = context.Request.Path.Value ?? string.Empty;
                if (path.Contains("mediacore", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug("[CSRF Middleware] Processing MediaCore request: {Method} {Path} (Raw: {RawPath})",
                        context.Request.Method, path, context.Request.Path);
                }

                if (HttpMethods.IsGet(context.Request.Method) ||
                    HttpMethods.IsHead(context.Request.Method) ||
                    HttpMethods.IsOptions(context.Request.Method) ||
                    HttpMethods.IsTrace(context.Request.Method))
                {
                    try
                    {
                        var antiforgery = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();

                        if (StripKnownAntiforgeryCookiesFromRequest(context))
                        {
                            ClearKnownAntiforgeryCookies(context);
                        }

                        // Only mint/store tokens on safe requests. Rotating them on the same unsafe request that
                        // later validates them can invalidate the frontend's header/cookie pair mid-flight.
                        var tokens = TryGetAndStoreAntiforgeryTokens(context, antiforgery);

                        // ASP.NET stores the antiforgery cookie token using the configured Cookie.Name.
                        // Only publish the JavaScript-readable request token here.
                        if (tokens?.RequestToken != null)
                        {
                            context.Response.Cookies.Append($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", tokens.RequestToken,
                                new CookieOptions
                                {
                                    HttpOnly = false,  // JavaScript needs to read this
                                    Secure = context.Request.IsHttps,
                                    SameSite = SameSiteMode.Strict,
                                    Path = "/",
                                });
                        }

                        // Clear the legacy request-token cookie so mixed old/new cookie sets cannot confuse the web client.
                        context.Response.Cookies.Delete("XSRF-TOKEN", new CookieOptions
                        {
                            Path = "/",
                            Secure = context.Request.IsHttps,
                            SameSite = SameSiteMode.Strict,
                        });
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
                }

                await next();
            });
            Log.Information("Using base url {UrlBase}", urlBase);

            // serve static content from the configured path
            FileServerOptions? fileServerOptions = null;
            var contentPath = Path.Combine(AppContext.BaseDirectory, OptionsAtStartup.Web.ContentPath);

            fileServerOptions = new FileServerOptions
            {
                FileProvider = CreateOwnedPhysicalFileProvider(contentPath),
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

                // Log static file requests for debugging
                Log.Debug("[FILE_SERVER] Serving static file: {Path}, Status: {Status}", path, context.Context.Response.StatusCode);

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

            app.UseRouting();
            if (OptionsAtStartup.Web.RateLimiting.Enabled)
            {
                app.UseRateLimiter();
            }

            app.UseAuthorization();

            if (OptionsAtStartup.Web.Logging)
            {
                app.UseSerilogRequestLogging();
            }

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
                var songIdHub = endpoints.MapHub<slskd.SongID.API.SongIdHub>("/hub/songid");
                var listeningPartyHub = endpoints.MapHub<ListeningPartyHub>("/hub/listening-party");
                if (OptionsAtStartup.Web.EnforceSecurity)
                {
                    searchHub.RequireAuthorization(AuthPolicy.Any);
                    songIdHub.RequireAuthorization(AuthPolicy.Any);
                    listeningPartyHub.RequireAuthorization(AuthPolicy.Any);
                }

                var relayHub = endpoints.MapHub<RelayHub>("/hub/relay");
                if (OptionsAtStartup.Web.EnforceSecurity)
                {
                    relayHub.RequireAuthorization(AuthPolicy.Any);
                }

                endpoints.MapControllers();

                // Solid-OIDC Client ID document (must be anonymous and return application/ld+json)
                endpoints.MapGet("/solid/clientid.jsonld", async context =>
                {
                    var opts = context.RequestServices.GetRequiredService<IOptionsMonitor<slskd.Options>>();
                    if (!opts.CurrentValue.Feature.Solid)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    var svc = context.RequestServices.GetRequiredService<slskd.Solid.ISolidClientIdDocumentService>();
                    context.Response.ContentType = "application/ld+json";
                    await svc.WriteClientIdDocumentAsync(context, context.RequestAborted).ConfigureAwait(false);
                }).AllowAnonymous();

                // Make /health explicitly anonymous to avoid auth issues in E2E harness
                endpoints.MapHealthChecks("/health").AllowAnonymous();
                endpoints.MapHealthChecks("/health/mesh", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("mesh"),
                }).AllowAnonymous();

                // Simple readiness endpoint for E2E tests - just checks if server is listening
                // This bypasses complex health checks that might hang during startup
                endpoints.MapGet("/health/ready", async context =>
                {
                    context.Response.StatusCode = 200;
                    await context.Response.WriteAsync("ready");
                });

                // Test-only route listing endpoint for E2E diagnostics
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SLSKDN_E2E_SHARE_ANNOUNCE")))
                {
                    endpoints.MapGet("/__routes", async context =>
                    {
                        var sources = context.RequestServices.GetRequiredService<IEnumerable<Microsoft.AspNetCore.Routing.EndpointDataSource>>();
                        var routes = sources
                            .SelectMany(s => s.Endpoints)
                            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
                            .Select(e => new { Pattern = e.RoutePattern.RawText ?? e.RoutePattern.ToString(), e.DisplayName })
                            .OrderBy(r => r.Pattern)
                            .ToList();
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(routes);
                    }).RequireAuthorization();
                }

                if (OptionsAtStartup.Metrics.Enabled)
                {
                    var options = OptionsAtStartup.Metrics;
                    var url = options.Url.StartsWith('/') ? options.Url : "/" + options.Url;

                    Log.Information("Publishing Prometheus metrics to {URL}", url);

                    if (options.Authentication.Disabled)
                    {
                        Log.Warning("Authentication for the metrics endpoint is DISABLED");
                    }
                    else if (string.IsNullOrWhiteSpace(options.Authentication.Password))
                    {
                        Log.Warning("[LOW-05] Prometheus metrics endpoint password is empty. " +
                            "Set metrics.authentication.password to a strong value, or set metrics.authentication.disabled=true to opt out of auth explicitly.");
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

                            // LOW-05: refuse to authenticate when password is empty — forces explicit configuration
                            if (string.IsNullOrWhiteSpace(options.Authentication.Password))
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                                await context.Response.WriteAsync("Metrics endpoint unavailable: authentication password is not configured.");
                                return;
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

                // SPA Fallback endpoint removed - using middleware instead (after file server)
                // This prevents the endpoint from intercepting static file requests
            });
#pragma warning restore ASP0014 // Suggest using top level route registrations

            // RESPONSE BODY FINALIZER: Ensures 4xx API responses have bodies (AFTER endpoints)
            // This is a workaround to fix empty response bodies for BadRequest/ProblemDetails
            // It buffers API responses and ensures the body is written even if other middleware clears it
            // Placed after UseEndpoints to catch what endpoints write and any middleware that runs after
            app.Use(async (ctx, next) =>
            {
                // Only buffer API routes to reduce overhead
                if (!ctx.Request.Path.StartsWithSegments("/api"))
                {
                    await next();
                    return;
                }

                var originalBody = ctx.Response.Body;

                await using var buffer = new MemoryStream();
                ctx.Response.Body = buffer;

                await next();

                // Restore original body
                ctx.Response.Body = originalBody;
                buffer.Position = 0;

                var bufferLen = buffer.Length;
                var statusCode = ctx.Response.StatusCode;
                var contentType = ctx.Response.ContentType;
                var contentLengthHeader = ctx.Response.ContentLength;

                // Log diagnostic info for API routes with 4xx status codes
                if (statusCode >= 400 && statusCode < 500)
                {
                    Log.Warning("[BodyFinalizer] {Method} {Path} -> {StatusCode} bufferLen={BufferLen} contentType={ContentType} contentLengthHeader={ContentLength}",
                        ctx.Request.Method, ctx.Request.Path, statusCode, bufferLen, contentType ?? "null", contentLengthHeader?.ToString() ?? "null");
                }

                // For 400-499 status codes, ensure the body is written
                if (statusCode >= 400 && statusCode < 500)
                {
                    if (bufferLen > 0)
                    {
                        // Body was written - ensure it's copied to original stream
                        // If Content-Length was set to 0 by another middleware, fix it
                        if (ctx.Response.ContentLength == 0 || ctx.Response.ContentLength == null)
                        {
                            ctx.Response.ContentLength = bufferLen;
                        }

                        await buffer.CopyToAsync(originalBody);
                    }
                    else
                    {
                        // Body is empty - already logged above with diagnostic info
                    }
                }
                else
                {
                    // For other status codes, just copy the buffer if it has content
                    if (bufferLen > 0)
                    {
                        await buffer.CopyToAsync(originalBody);
                    }
                }
            });

            // if this is an /api route and no API controller was matched, give up and return a 404.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    // Log 404s for API routes to help debug route mismatches
                    Log.Warning("[API404] {Method} {Path} - No matching endpoint found", context.Request.Method, context.Request.Path);
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

            // Old SPA fallback middleware removed - using fallback after file server instead

            // UseFileServer is placed AFTER UseEndpoints to ensure routing happens first, then static files.
            // This prevents static file middleware from short-circuiting requests before routing/security middleware runs.
            if (!OptionsAtStartup.Headless)
            {
                app.UseFileServer(fileServerOptions);
                Log.Information("Serving static content from {ContentPath}", contentPath);

                // SPA Fallback: Serve index.html for client-side routes AFTER file server
                // This runs AFTER file server so static files are served first, and only 404s get index.html
                var indexPath = Path.Combine(AppContext.BaseDirectory, OptionsAtStartup.Web.ContentPath, "index.html");
                if (System.IO.File.Exists(indexPath))
                {
                    app.Use(async (context, next) =>
                    {
                        await next(); // Let file server try first

                        // If file server returned 404 and this is a client-side route, serve index.html
                        if (context.Response.StatusCode == 404 && !context.Response.HasStarted)
                        {
                            var path = context.Request.Path.Value ?? string.Empty;

                            // Only serve index.html for non-API, non-file, non-static paths
                            var isApi = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
                            var isSwagger = path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
                            var isHub = path.StartsWith("/hub", StringComparison.OrdinalIgnoreCase);
                            var isHealth = path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
                            var isStatic = path.StartsWith("/static", StringComparison.OrdinalIgnoreCase);
                            var hasExtension = Path.GetExtension(path) != string.Empty;

                            if (!isApi && !isSwagger && !isHub && !isHealth && !isStatic && !hasExtension)
                            {
                                Log.Debug("[SPA Fallback Middleware] Serving index.html for {Path} (file server returned 404)", path);
                                context.Response.StatusCode = 200;
                                context.Response.ContentType = "text/html; charset=utf-8";
                                await context.Response.SendFileAsync(indexPath);
                            }
                        }
                    });
                    Log.Information("[SPA] Registered fallback to index.html for client-side routing (after file server)");
                }
            }
            else
            {
                Log.Warning("Running in headless mode; web UI is DISABLED");
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
                        textFormatter: new MessageTemplateTextFormatter("[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", null)))
                .WriteTo.Sink(new DelegatingSink(logEvent =>
                {
                    string message = string.Empty;

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
                            SubContext = logEvent.Properties.ContainsKey("SubContext") ? logEvent.Properties["SubContext"].ToString().TrimStart('"').TrimEnd('"') : string.Empty,
                            Level = logEvent.Level.ToString(),
                            Message = message.TrimStart('"').TrimEnd('"'),
                        };

                        LogBuffer.Enqueue(record);
                        RaiseLogEmitted(record);
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
                    Serilog.Log.Logger.Error(exception, "Unhandled exception: {Message}", exception?.Message ?? "Unknown exception");
                }
            };
        }

        /// <summary>
        /// Gets a configuration section under the slskd: namespace.
        /// This ensures all options bind correctly to the YAML provider's namespace.
        /// </summary>
        private static IConfigurationSection GetSlskdSection(this IConfiguration configuration, string sectionName)
        {
            return configuration.GetSection($"{AppName}:{sectionName}");
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
                .Select(v => v?.ToString())
                .Where(v => v != "\u0000")
                .OfType<string>()
                .ToArray();

            var configurationDirectory = Path.GetDirectoryName(configurationFile);
            if (string.IsNullOrWhiteSpace(configurationDirectory))
            {
                throw new InvalidOperationException($"Configuration file path '{configurationFile}' does not have a directory component.");
            }

            var result = builder
                .AddDefaultValues(
                    targetType: typeof(Options))
                .AddEnvironmentVariables(
                    targetType: typeof(Options),
                    prefix: environmentVariablePrefix)
#pragma warning disable CA2000 // Framework configuration infrastructure owns the file provider lifecycle.
                .AddYamlFile(
                    path: Path.GetFileName(configurationFile),
                    targetType: typeof(Options),
                    optional: true,
                    reloadOnChange: reloadOnChange,
                    provider: CreateOwnedPhysicalFileProvider(configurationDirectory, ExclusionFilters.None)) // required for locations outside of the app directory
#pragma warning restore CA2000
                .AddCommandLine(
                    targetType: typeof(Options),
                    multiValuedArguments,
                    commandLine: Environment.CommandLine)
                .Add(VolatileOverlayConfigurationSource); // this must come last in order to supersede all other sources

            Log.Information("[Config] Configuration providers added, YAML file: {ConfigFile}", configurationFile);
            return result;
        }

        internal static IReadOnlyList<string> GetConfigurationCompatibilityWarnings(string configurationFile, Options options)
        {
            if (!IOFile.Exists(configurationFile))
            {
                return Array.Empty<string>();
            }

            var warnings = new List<string>();
            var lines = IOFile.ReadAllLines(configurationFile);
            var hasCanonicalIntegrations = HasTopLevelKey(lines, "integrations");
            var hasCanonicalTransferGroups = HasDirectChildKey(lines, "transfers", "groups");
            var hasCanonicalUploadLimits = HasNestedChildKey(lines, new[] { "transfers", "upload" }, "limits");

            if (HasTopLevelKey(lines, "global"))
            {
                warnings.Add("Configuration key 'global' is deprecated; slskdN accepts it for now, but 'transfers' is the canonical transfer-rate and retry section.");
            }

            if (HasTopLevelKey(lines, "groups") && !hasCanonicalTransferGroups)
            {
                warnings.Add("Top-level configuration key 'groups' is accepted for compatibility; new configuration should place groups under 'transfers.groups'.");
            }

            if (HasDirectChildKey(lines, "transfers", "limits") && !hasCanonicalUploadLimits)
            {
                warnings.Add("Configuration key 'transfers.limits' is accepted for compatibility; new configuration should place global upload limits under 'transfers.upload.limits'.");
            }

            if (HasTopLevelKey(lines, "integration") && !hasCanonicalIntegrations)
            {
                warnings.Add("Configuration key 'integration' is deprecated; slskdN accepts it for now, but 'integrations' is the canonical external integration section.");
            }

            if (HasGroupLevelLimits(lines))
            {
                warnings.Add("Group-level 'limits' entries are accepted for compatibility; place them under each group's 'upload' section in new configuration files.");
            }

            if (options.Global.Download.Retry.MaxDelay < MinimumRetryMaxDelayMilliseconds)
            {
                warnings.Add($"Download retry max_delay is below {MinimumRetryMaxDelayMilliseconds}ms; slskdN will clamp retry scheduling to that floor.");
            }

            return warnings.AsReadOnly();
        }

        private const int MinimumRetryMaxDelayMilliseconds = 30_000;

        private static bool HasTopLevelKey(IEnumerable<string> lines, string key)
        {
            var prefix = $"{key}:";
            return lines
                .Select(StripYamlComment)
                .Any(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasDirectChildKey(IEnumerable<string> lines, string parentKey, string childKey)
            => HasNestedChildKey(lines, new[] { parentKey }, childKey);

        private static bool HasNestedChildKey(IEnumerable<string> lines, IReadOnlyList<string> parentPath, string childKey)
        {
            var matchedDepth = 0;
            var matchedIndents = new List<int>();
            var childIndent = -1;

            foreach (var rawLine in lines.Select(StripYamlComment))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                var indent = rawLine.TakeWhile(char.IsWhiteSpace).Count();
                var trimmed = rawLine.TrimStart();

                while (matchedDepth > 0 && indent <= matchedIndents[matchedDepth - 1])
                {
                    matchedDepth--;
                    matchedIndents.RemoveAt(matchedIndents.Count - 1);
                    childIndent = -1;
                }

                if (matchedDepth < parentPath.Count &&
                    trimmed.StartsWith($"{parentPath[matchedDepth]}:", StringComparison.OrdinalIgnoreCase))
                {
                    matchedDepth++;
                    matchedIndents.Add(indent);
                    childIndent = -1;
                    continue;
                }

                if (matchedDepth != parentPath.Count)
                {
                    continue;
                }

                if (childIndent < 0)
                {
                    childIndent = indent;
                }

                if (indent == childIndent && trimmed.StartsWith($"{childKey}:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasGroupLevelLimits(IEnumerable<string> lines)
        {
            var inGroups = false;
            var groupsIndent = 0;
            var groupIndent = 0;

            foreach (var rawLine in lines.Select(StripYamlComment))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                var indent = rawLine.TakeWhile(char.IsWhiteSpace).Count();
                var trimmed = rawLine.TrimStart();

                if (indent == 0)
                {
                    inGroups = trimmed.StartsWith("groups:", StringComparison.OrdinalIgnoreCase);
                    groupsIndent = 0;
                    groupIndent = 0;
                    continue;
                }

                if (!inGroups)
                {
                    continue;
                }

                if (indent <= groupsIndent)
                {
                    inGroups = false;
                    continue;
                }

                if (groupIndent == 0 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    groupIndent = indent;
                    continue;
                }

                if (groupIndent > 0 && indent == groupIndent + 2 && trimmed.StartsWith("limits:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripYamlComment(string line)
        {
            var index = line.IndexOf('#');
            return index >= 0 ? line[..index].TrimEnd() : line.TrimEnd();
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

                if (!string.Equals(journalMode, "WAL", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Failed to set database {Type} journal_mode PRAGMA to WAL; performance may be reduced", typeof(T).Name);
                }

                using var syncCmd = conn.CreateCommand();
                syncCmd.CommandText = "PRAGMA synchronous;";
                var sync = syncCmd.ExecuteScalar()?.ToString();

                if (!string.Equals(sync, "1", StringComparison.OrdinalIgnoreCase))
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

            using var cert = X509.Generate(subject: AppName, password, X509KeyStorageFlags.Exportable);
            IOFile.WriteAllBytes(filename, cert.Export(X509ContentType.Pkcs12, password));
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                try
                {
                    IOFile.SetUnixFileMode(filename, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not set restrictive permissions on generated certificate {Filename}", filename);
                }
            }

            return (filename, password);
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The assigned framework options/configuration source owns the file provider lifecycle.")]
        private static PhysicalFileProvider CreateOwnedPhysicalFileProvider(string root, ExclusionFilters exclusionFilters = ExclusionFilters.Sensitive)
            => new(root, exclusionFilters);

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
                            var shortName = attribute.ConstructorArguments[0].Value is char shortNameValue ? shortNameValue : default;
                            var longName = attribute.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
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
                            var name = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
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
                var expectedShutdown = Application.IsShuttingDown;
                var msg = expectedShutdown
                    ? "ProcessExit event fired during expected shutdown"
                    : "[FATAL] ProcessExit event fired - process terminating";
                if (!expectedShutdown)
                {
                    Console.Error.WriteLine(msg);
                }

                try
                {
                    if (expectedShutdown)
                    {
                        Log?.Information(msg);
                    }
                    else
                    {
                        Log?.Fatal(msg);
                    }
                }
                catch
                {
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                var msg = $"[FATAL] Unhandled exception: {ex?.Message ?? e.ExceptionObject?.ToString() ?? "unknown"}";
                Console.Error.WriteLine(msg);
                Console.Error.WriteLine(ex?.StackTrace ?? "no stack trace");
                try
                {
                    Log?.Fatal(ex, msg);
                }
                catch
                {
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                if (IsBenignUnobservedTaskException(e.Exception))
                {
                    var msg = $"[WARN] Ignoring benign unobserved task exception: {e.Exception.Message}";
                    Console.Error.WriteLine(msg);
                    try
                    {
                        Log?.Warning(e.Exception, msg);
                    }
                    catch
                    {
                    }

                    e.SetObserved();
                    return;
                }

                var baseException = e.Exception.GetBaseException();

                if (IsExpectedSoulseekNetworkException(e.Exception))
                {
                    var warningMessage = $"Ignoring expected Soulseek peer/distributed network exception: {baseException.Message}";
                    try
                    {
                        Log?.Debug(baseException, warningMessage);
                    }
                    catch
                    {
                    }

                    e.SetObserved();
                    return;
                }

                var fatalMessage = $"[FATAL] Unobserved task exception: {e.Exception.Message}";
                Console.Error.WriteLine(fatalMessage);
                Console.Error.WriteLine(e.Exception.StackTrace);
                try
                {
                    Log?.Fatal(e.Exception, fatalMessage);
                }
                catch
                {
                }

                e.SetObserved(); // Prevent process termination
            };
        }

        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        [System.Runtime.Versioning.SupportedOSPlatform("macos")]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static Mesh.Overlay.QuicOverlayClient CreateQuicOverlayClient(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Overlay.QuicOverlayClient>>();
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.Overlay.OverlayOptions>>();
            var signer = serviceProvider.GetRequiredService<Mesh.Overlay.IControlSigner>();
            var privacyLayer = serviceProvider.GetService<Mesh.Privacy.IPrivacyLayer>();
            return new Mesh.Overlay.QuicOverlayClient(logger, options, signer, privacyLayer);
        }

        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        [System.Runtime.Versioning.SupportedOSPlatform("macos")]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static Mesh.Overlay.QuicDataClient CreateQuicDataClient(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Mesh.Overlay.QuicDataClient>>();
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mesh.Overlay.DataOverlayOptions>>();
            return new Mesh.Overlay.QuicDataClient(logger, options);
        }

        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        [System.Runtime.Versioning.SupportedOSPlatform("macos")]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static Mesh.Overlay.QuicOverlayServer CreateQuicOverlayServer(IServiceProvider serviceProvider)
        {
            return ActivatorUtilities.CreateInstance<Mesh.Overlay.QuicOverlayServer>(serviceProvider);
        }

        internal static bool IsExpectedSoulseekNetworkException(Exception exception)
        {
            var flattened = FlattenExceptions(exception).ToList();

            return flattened.Count > 0 && flattened.All(IsExpectedSoulseekNetworkExceptionCore);
        }

        internal static Microsoft.AspNetCore.Antiforgery.AntiforgeryTokenSet? TryGetAndStoreAntiforgeryTokens(
            HttpContext context,
            Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
        {
            try
            {
                return antiforgery.GetAndStoreTokens(context);
            }
            catch (Exception ex) when (IsStaleAntiforgeryTokenException(ex))
            {
                ClearKnownAntiforgeryCookies(context);
                Log.Warning("[CSRF Middleware] Cleared stale antiforgery cookies for {Path} after key-ring mismatch", context.Request.Path);
                return antiforgery.GetAndStoreTokens(context);
            }
        }

        internal static bool IsStaleAntiforgeryTokenException(Exception exception)
        {
            return FlattenExceptions(exception).Any(innerException =>
                innerException is CryptographicException ||
                innerException.Message.Contains("could not be decrypted", StringComparison.OrdinalIgnoreCase) ||
                innerException.Message.Contains("key ring", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool StripKnownAntiforgeryCookiesFromRequest(HttpContext context)
        {
            var filteredSegments = new List<string>();
            var removed = false;

            foreach (var headerValue in context.Request.Headers.Cookie)
            {
                if (headerValue is null)
                {
                    continue;
                }

                foreach (var segment in headerValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var separatorIndex = segment.IndexOf('=');
                    var cookieName = separatorIndex >= 0 ? segment[..separatorIndex].Trim() : segment.Trim();

                    if (string.Equals(cookieName, $"XSRF-COOKIE-{OptionsAtStartup.Web.Port}", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cookieName, $"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cookieName, "XSRF-COOKIE", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cookieName, "XSRF-TOKEN", StringComparison.OrdinalIgnoreCase))
                    {
                        removed = true;
                        continue;
                    }

                    filteredSegments.Add(segment);
                }
            }

            if (!removed)
            {
                return false;
            }

            if (filteredSegments.Count == 0)
            {
                context.Request.Headers.Remove("Cookie");
            }
            else
            {
                context.Request.Headers.Cookie = string.Join("; ", filteredSegments);
            }

            context.Features.Set<Microsoft.AspNetCore.Http.Features.IRequestCookiesFeature>(
                new Microsoft.AspNetCore.Http.Features.RequestCookiesFeature(context.Features));

            return true;
        }

        internal static void ClearKnownAntiforgeryCookies(HttpContext context)
        {
            var options = new CookieOptions
            {
                Path = "/",
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
            };

            context.Response.Cookies.Delete($"XSRF-COOKIE-{OptionsAtStartup.Web.Port}", options);
            context.Response.Cookies.Delete($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", options);
            context.Response.Cookies.Delete("XSRF-COOKIE", options);
            context.Response.Cookies.Delete("XSRF-TOKEN", options);
        }

        private static IEnumerable<Exception> FlattenExceptions(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.Flatten().InnerExceptions)
                {
                    foreach (var flattenedInnerException in FlattenExceptions(innerException))
                    {
                        yield return flattenedInnerException;
                    }
                }

                yield break;
            }

            yield return exception;

            if (exception.InnerException is not null)
            {
                foreach (var innerException in FlattenExceptions(exception.InnerException))
                {
                    yield return innerException;
                }
            }
        }

        private static bool IsExpectedSoulseekNetworkExceptionCore(Exception exception)
        {
            var typeName = exception.GetType().FullName ?? exception.GetType().Name;
            var details = exception.ToString();
            var isSoulseekMessageConnectionClosed =
                exception is InvalidOperationException &&
                details.Contains("The underlying Tcp connection is closed", StringComparison.Ordinal) &&
                details.Contains("Soulseek.Network.MessageConnection.ReadContinuouslyAsync", StringComparison.Ordinal);
            var isSoulseekTimerResetReadRace =
                exception is NullReferenceException &&
                details.Contains("Soulseek.Extensions.Reset(", StringComparison.Ordinal) &&
                details.Contains("Soulseek.Network.MessageConnection.ReadContinuouslyAsync", StringComparison.Ordinal);
            var isSoulseekTimerResetWriteRace =
                exception is NullReferenceException &&
                details.Contains("Soulseek.Extensions.Reset(", StringComparison.Ordinal) &&
                details.Contains("Soulseek.Network.Tcp.Connection.WriteInternalAsync", StringComparison.Ordinal);
            var isSoulseekTcpDoubleDisconnectRace =
                exception is InvalidOperationException &&
                details.Contains("An attempt was made to transition a task to a final state", StringComparison.Ordinal) &&
                details.Contains("Soulseek.Network.Tcp.Connection.Disconnect", StringComparison.Ordinal);
            var isSoulseekListenerSocketDisposed =
                exception is ObjectDisposedException listenerDisposedException &&
                string.Equals(listenerDisposedException.ObjectName, "System.Net.Sockets.Socket", StringComparison.Ordinal) &&
                details.Contains("Soulseek.Network.Tcp.Listener.ListenContinuouslyAsync", StringComparison.Ordinal);

            var isNetworkFailure =
                exception is TimeoutException ||
                exception is OperationCanceledException ||
                exception is IOException ||
                (exception is ObjectDisposedException objectDisposedException && string.Equals(objectDisposedException.ObjectName, "Connection", StringComparison.Ordinal)) ||
                exception is System.Net.Sockets.SocketException ||
                isSoulseekMessageConnectionClosed ||
                isSoulseekTimerResetReadRace ||
                isSoulseekTimerResetWriteRace ||
                isSoulseekTcpDoubleDisconnectRace ||
                isSoulseekListenerSocketDisposed ||
                typeName.Contains("Soulseek.ConnectionReadException", StringComparison.Ordinal) ||
                typeName.Contains("Soulseek.ConnectionException", StringComparison.Ordinal) ||
                typeName.Contains("Soulseek.TransferException", StringComparison.Ordinal) ||
                typeName.Contains("Soulseek.TransferRejectedException", StringComparison.Ordinal) ||
                typeName.Contains("Soulseek.TransferReportedFailedException", StringComparison.Ordinal);

            if (!isNetworkFailure)
            {
                return false;
            }

            return details.Contains("Soulseek.Network.PeerConnectionManager", StringComparison.Ordinal) ||
                details.Contains("Soulseek.Network.DistributedConnectionManager", StringComparison.Ordinal) ||
                details.Contains("Soulseek.Network.Tcp.Connection", StringComparison.Ordinal) ||
                details.Contains("Soulseek.Network.Tcp.Listener", StringComparison.Ordinal) ||
                details.Contains("Failed to connect", StringComparison.Ordinal) ||
                details.Contains("Connection refused", StringComparison.Ordinal) ||
                details.Contains("Connection reset by peer", StringComparison.Ordinal) ||
                details.Contains("Remote connection closed", StringComparison.Ordinal) ||
                details.Contains("The underlying Tcp connection is closed", StringComparison.Ordinal) ||
                details.Contains("Download reported as failed by remote client", StringComparison.Ordinal) ||
                details.Contains("Enqueue failed due to internal error", StringComparison.Ordinal) ||
                details.Contains("Too many megabytes", StringComparison.Ordinal) ||
                details.Contains("Too many files", StringComparison.Ordinal) ||
                details.Contains("Transfer failed: Transfer complete", StringComparison.Ordinal) ||
                details.Contains("No route to host", StringComparison.Ordinal) ||
                details.Contains("Operation timed out", StringComparison.Ordinal) ||
                details.Contains("Connection timed out", StringComparison.Ordinal) ||
                details.Contains("The wait timed out", StringComparison.Ordinal) ||
                details.Contains("Inactivity timeout", StringComparison.Ordinal) ||
                details.Contains("Failed to read", StringComparison.Ordinal) ||
                details.Contains("Unable to read data from the transport connection", StringComparison.Ordinal) ||
                details.Contains("Operation canceled", StringComparison.Ordinal) ||
                details.Contains("Operation cancelled", StringComparison.Ordinal) ||
                details.Contains("Unknown PierceFirewall attempt", StringComparison.Ordinal) ||
                details.Contains("Cannot access a disposed object.", StringComparison.Ordinal);
        }

    }
}
