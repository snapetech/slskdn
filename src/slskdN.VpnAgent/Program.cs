using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.FirstOrDefault();
if (string.IsNullOrWhiteSpace(command) || command is "-h" or "--help")
{
    Usage();
    return command is "-h" or "--help" ? 0 : 64;
}

try
{
    return command switch
    {
        "api" => await Commands.Api(args.Skip(1).ToArray()),
        "cleanup-ingress" => await Commands.CleanupIngress(),
        "ingress" => await Commands.Ingress(),
        "split" => await Commands.Split(),
        "platform-split" => await Commands.PlatformSplit(),
        "verify" => await Commands.Verify(args.Skip(1).Contains("--quiet")),
        "status" => await Commands.Verify(quiet: false),
        "watchdog" => await Commands.Watchdog(),
        _ => Unknown(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"slskdN-vpn-agent: {ex.Message}");
    return 1;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Usage();
    return 64;
}

static void Usage()
{
    Console.WriteLine("""
    Usage: slskdN-vpn-agent <command>

    Commands:
      api        Run Gluetun-compatible API for slskdN
      cleanup-ingress
                 Remove transparent VPN ingress network namespaces/routes
      ingress    Configure transparent VPN ingress forwards
      split      Configure UID policy routing through the VPN table
      platform-split
                 Configure platform-native fail-closed routing/firewall
      verify     Verify slskdN VPN health
      status     Human-readable status check
      watchdog   Run one watchdog check and recover ingress after repeated failures
    """);
}

static class Commands
{
    public static async Task<int> Api(string[] args)
    {
        var host = Option(args, "--host") ?? Env.Get("GLUETUN_COMPAT_HOST", "127.0.0.1");
        var portText = Option(args, "--port") ?? Env.Get("GLUETUN_COMPAT_PORT", "8010");
        var port = int.TryParse(portText, out var parsed) ? parsed : 8010;
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://{host}:{port}/");
        listener.Start();

        while (true)
        {
            var context = await listener.GetContextAsync();
            _ = Task.Run(() => HandleApiRequest(context));
        }
    }

    public static async Task<int> Verify(bool quiet)
    {
        var verifier = new Verifier(quiet);

        var requiredCommands = new List<string> { "systemctl", "curl", "jq", "ip", "sudo" };
        if (AppConfig.TunnelType == "wireguard")
        {
            requiredCommands.Add("wg");
        }

        foreach (var command in requiredCommands)
        {
            verifier.Check(await ProcessUtil.CommandExists(command), $"command present: {command}", $"missing command: {command}");
        }

        foreach (var unit in new[]
        {
            AppConfig.ApplicationService,
            AppConfig.TunnelService,
            AppConfig.SplitService,
            AppConfig.ApiService,
            AppConfig.IngressRenewTimer
        }.Where(unit => !string.IsNullOrWhiteSpace(unit)))
        {
            var active = (await ProcessUtil.Run("systemctl", "is-active", "--quiet", unit)).ExitCode == 0;
            verifier.Check(active, $"unit active: {unit}", $"unit not active: {unit}");
        }

        var apiKey = Slskd.ApiKey();
        verifier.Check(!string.IsNullOrWhiteSpace(apiKey), "slskdN API key found", $"could not find slskdN API key in {AppConfig.SlskdConfig}; set SLSKD_API_KEY");

        SlskdApplication? application = null;
        SlskdOptions? options = null;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                application = await Slskd.GetJson<SlskdApplication>("/api/v0/application", apiKey);
                verifier.Pass("slskdN application API reachable");
            }
            catch
            {
                verifier.Fail("slskdN application API unreachable");
            }

            try
            {
                options = await Slskd.GetJson<SlskdOptions>("/api/v0/options", apiKey);
                verifier.Pass("slskdN options API reachable");
            }
            catch
            {
                verifier.Fail("slskdN options API unreachable");
            }
        }

        var serverState = application?.Server?.State ?? "";
        var vpn = application?.Vpn;
        var vpnForwarded = vpn?.ForwardedPort;
        var listenPort = options?.Soulseek?.ListenPort;

        verifier.Check(serverState == "Connected, LoggedIn", "slskdN server connected/logged in", $"slskdN server state: {Blank(serverState)}");
        verifier.Check(vpn?.IsReady == true, "slskdN VPN ready", "slskdN VPN not ready");
        verifier.Check(vpn?.IsConnected == true, "slskdN VPN connected", "slskdN VPN not connected");
        verifier.Check(vpnForwarded.HasValue, $"slskdN forwarded port: {vpnForwarded}", "slskdN forwarded port missing");
        verifier.Check(listenPort.HasValue, $"slskdN listen port: {listenPort}", "slskdN listen port missing");

        var summary = EnvFile.Read(AppConfig.StateFile("summary.env"));
        var claimed = summary.GetValueOrDefault("claimed", "");
        verifier.Check(int.TryParse(claimed, out var claimedCount) && claimedCount > 0, $"ingress claimed mappings: {claimed}", $"ingress claimed mappings invalid: {Blank(claimed)}");

        var pf0 = EnvFile.Read(AppConfig.StateFile("pf0.env"));
        var pfPublic = pf0.GetValueOrDefault("public_port", "");
        var pfTarget = pf0.GetValueOrDefault("target_port", "");
        var pfIp = pf0.GetValueOrDefault("public_ip", "");
        verifier.Check(int.TryParse(pfPublic, out var pfPublicPort), $"pf0 public port: {pfPublic}", "pf0 public port missing");
        verifier.Check(int.TryParse(pfTarget, out var pfTargetPort), $"pf0 target port: {pfTarget}", "pf0 target port missing");
        verifier.Check(!string.IsNullOrWhiteSpace(pfIp), $"pf0 public IP: {pfIp}", "pf0 public IP missing");
        if (vpnForwarded.HasValue && int.TryParse(pfPublic, out pfPublicPort))
        {
            verifier.Check(pfPublicPort == vpnForwarded.Value, "pf0 public_port matches slskdN forwardedPort", $"pf0 public_port {pfPublic} does not match slskdN forwardedPort {vpnForwarded}");
        }
        if (listenPort.HasValue && int.TryParse(pfTarget, out pfTargetPort))
        {
            verifier.Check(pfTargetPort == listenPort.Value, "pf0 target_port matches slskdN listenPort", $"pf0 target_port {pfTarget} does not match slskdN listenPort {listenPort}");
        }

        var routes = await ProcessUtil.Run("ip", "route", "show", "table", AppConfig.VpnTable);
        verifier.Check(routes.Stdout.Contains($"default dev {AppConfig.VpnIface}", StringComparison.Ordinal), $"VPN table default route uses {AppConfig.VpnIface}", $"VPN table {AppConfig.VpnTable} missing default route via {AppConfig.VpnIface}");
        verifier.Check(routes.Stdout.Contains("blackhole default", StringComparison.Ordinal), "VPN table has blackhole fallback", $"VPN table {AppConfig.VpnTable} missing blackhole fallback");

        if (AppConfig.TunnelType == "wireguard")
        {
            var handshakes = await ProcessUtil.Run("wg", "show", AppConfig.VpnIface, "latest-handshakes");
            var hasHandshake = handshakes.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Any(parts => parts.Length >= 2 && long.TryParse(parts[1], out var ts) && ts > 0);
            verifier.Check(hasHandshake, $"WireGuard handshake present on {AppConfig.VpnIface}", $"no WireGuard handshake on {AppConfig.VpnIface}");
        }
        else
        {
            verifier.Pass($"external tunnel mode: {AppConfig.TunnelType}");
        }

        var slskdIp = (await ProcessUtil.Run("sudo", "-u", AppConfig.ServiceUser, "curl", "-4", "-m", AppConfig.VerifyTimeoutSeconds.ToString(), "-s", "https://ifconfig.me/ip")).Stdout.Trim();
        string hostIp;
        try
        {
            hostIp = await Http.Text("https://ifconfig.me/ip", AppConfig.VerifyTimeoutSeconds);
        }
        catch
        {
            hostIp = "";
        }

        verifier.Check(!string.IsNullOrWhiteSpace(slskdIp), $"slskdN egress IP: {slskdIp}", "could not determine slskdN egress IP");
        if (!string.IsNullOrWhiteSpace(hostIp))
        {
            verifier.Pass($"host egress IP: {hostIp}");
        }
        else
        {
            verifier.Warn("could not determine host egress IP");
        }
        if (!string.IsNullOrWhiteSpace(slskdIp) && !string.IsNullOrWhiteSpace(hostIp))
        {
            verifier.Check(slskdIp != hostIp, "slskdN egress differs from host egress", "slskdN egress matches host egress");
        }

        if (verifier.Failures == 0)
        {
            verifier.Say("OK: slskdN VPN integration verified");
            return 0;
        }

        verifier.Say($"FAILED: {verifier.Failures} check(s) failed");
        return 1;
    }

    public static async Task<int> Split()
    {
        if (OperatingSystem.IsLinux())
        {
            return await SplitLinux();
        }

        return await PlatformSplit();
    }

    public static async Task<int> PlatformSplit()
    {
        if (OperatingSystem.IsLinux())
        {
            return await SplitLinux();
        }

        if (OperatingSystem.IsWindows())
        {
            return await SplitWindows();
        }

        if (OperatingSystem.IsMacOS())
        {
            return await SplitMacOS();
        }

        Console.Error.WriteLine($"Unsupported platform: {RuntimeInformation.OSDescription}");
        return 2;
    }

    private static async Task<int> SplitLinux()
    {
        for (var i = 0; i < 20; i++)
        {
            if ((await ProcessUtil.Run("ip", "link", "show", AppConfig.VpnIface)).ExitCode == 0)
            {
                break;
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var ifaceCheck = await ProcessUtil.Run("ip", "link", "show", AppConfig.VpnIface);
        if (ifaceCheck.ExitCode != 0)
        {
            Console.Error.WriteLine($"VPN interface not found: {AppConfig.VpnIface}");
            return 1;
        }

        while (true)
        {
            var rules = await ProcessUtil.Run("ip", "rule", "show");
            if (!rules.Stdout.Contains($"uidrange {AppConfig.ServiceUid}-{AppConfig.ServiceUid}", StringComparison.Ordinal) ||
                !rules.Stdout.Contains($"lookup {AppConfig.VpnTable}", StringComparison.Ordinal))
            {
                break;
            }

            var deleted = await ProcessUtil.Run("ip", "rule", "del", "uidrange", $"{AppConfig.ServiceUid}-{AppConfig.ServiceUid}", "lookup", AppConfig.VpnTable);
            if (deleted.ExitCode != 0)
            {
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(AppConfig.ProviderGateway))
        {
            await DeleteRuleUntilGone("to", $"{AppConfig.ProviderGateway}/32", "lookup", AppConfig.VpnTable);
        }
        foreach (var cidr in AppConfig.LocalCidrs)
        {
            await DeleteRuleUntilGone("uidrange", $"{AppConfig.ServiceUid}-{AppConfig.ServiceUid}", "to", cidr, "lookup", "main");
        }
        foreach (var nameserver in ResolvConfNameservers())
        {
            await DeleteRuleUntilGone("uidrange", $"{AppConfig.ServiceUid}-{AppConfig.ServiceUid}", "to", $"{nameserver}/32", "lookup", "main");
        }

        await MustRun("ip", "route", "replace", "default", "dev", AppConfig.VpnIface, "table", AppConfig.VpnTable);
        await MustRun("ip", "route", "replace", "blackhole", "default", "table", AppConfig.VpnTable, "metric", "32767");
        await MustRun("ip", "rule", "add", "pref", "32760", "uidrange", $"{AppConfig.ServiceUid}-{AppConfig.ServiceUid}", "lookup", AppConfig.VpnTable);
        if (!string.IsNullOrWhiteSpace(AppConfig.ProviderGateway))
        {
            await ProcessUtil.Run("ip", "rule", "add", "pref", "32758", "to", $"{AppConfig.ProviderGateway}/32", "lookup", AppConfig.VpnTable);
        }

        foreach (var cidr in AppConfig.LocalCidrs)
        {
            await ProcessUtil.Run("ip", "rule", "add", "pref", "32755", "uidrange", $"{AppConfig.ServiceUid}-{AppConfig.ServiceUid}", "to", cidr, "lookup", "main");
        }

        foreach (var nameserver in ResolvConfNameservers())
        {
            await ProcessUtil.Run("ip", "rule", "add", "pref", "32759", "uidrange", $"{AppConfig.ServiceUid}-{AppConfig.ServiceUid}", "to", $"{nameserver}/32", "lookup", "main");
        }

        Console.WriteLine($"Configured UID {AppConfig.ServiceUid} ({AppConfig.ServiceUser}) routing through {AppConfig.VpnIface} table {AppConfig.VpnTable}");
        return 0;
    }

    private static async Task<int> SplitWindows()
    {
        var appPath = AppConfig.ApplicationPath;
        if (string.IsNullOrWhiteSpace(appPath))
        {
            Console.Error.WriteLine("SLSKDN_APP_PATH is required for Windows per-program VPN enforcement");
            return 2;
        }

        var script = $$$"""
        $ErrorActionPreference = 'Stop'
        $group = 'slskdN VPN'
        $app = '{{{EscapePowerShell(appPath)}}}'
        $vpn = '{{{EscapePowerShell(AppConfig.VpnIface)}}}'
        if (-not (Test-Path -LiteralPath $app)) { throw "Application path not found: $app" }
        Get-NetFirewallRule -Group $group -ErrorAction SilentlyContinue | Remove-NetFirewallRule
        $interfaces = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.Name -ne $vpn }
        foreach ($iface in $interfaces) {
            New-NetFirewallRule -DisplayName "slskdN VPN fail-closed $($iface.Name)" -Group $group -Direction Outbound -Program $app -Action Block -InterfaceAlias $iface.Name -Profile Any | Out-Null
        }
        New-NetFirewallRule -DisplayName 'slskdN VPN allow loopback' -Group $group -Direction Outbound -Program $app -Action Allow -RemoteAddress LocalSubnet -Profile Any | Out-Null
        Write-Output "Configured Windows firewall fail-closed rules for $app; allowed VPN interface: $vpn"
        """;
        var shell = await ProcessUtil.CommandExists("pwsh") ? "pwsh" : "powershell";
        var result = await ProcessUtil.Run(shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script);
        Console.Write(result.Stdout);
        if (result.ExitCode != 0)
        {
            Console.Error.Write(result.Stderr);
        }

        return result.ExitCode;
    }

    private static async Task<int> SplitMacOS()
    {
        var anchor = AppConfig.PfAnchorName;
        var rules = $$$"""
        pass out quick on lo0 all
        block drop out quick on ! {{{AppConfig.VpnIface}}} proto { tcp udp } user {{{AppConfig.ServiceUser}}}
        """;
        var temp = Path.GetTempFileName();
        await File.WriteAllTextAsync(temp, rules);
        try
        {
            await MustRun("pfctl", "-a", anchor, "-f", temp);
            var enabled = await ProcessUtil.Run("pfctl", "-s", "info");
            if (!enabled.Stdout.Contains("Status: Enabled", StringComparison.OrdinalIgnoreCase))
            {
                await MustRun("pfctl", "-e");
            }
        }
        finally
        {
            File.Delete(temp);
        }

        Console.WriteLine($"Configured macOS pf anchor {anchor}: user {AppConfig.ServiceUser} can egress only on {AppConfig.VpnIface}");
        return 0;
    }

    public static async Task<int> Ingress()
    {
        Directory.CreateDirectory(AppConfig.StateDir.FullName);
        if (AppConfig.TunnelType == "wireguard")
        {
            await RequireCommand("wg-quick");
        }

        if (AppConfig.PortForwardBackend == "natpmp")
        {
            await RequireCommand("natpmpc");
        }

        if (AppConfig.TunnelType == "wireguard")
        {
            await ProcessUtil.Run("sysctl", "-qw", "net.ipv4.ip_forward=1");
            await EnsureIptablesRule("iptables", "-C", "FORWARD", "-s", $"{AppConfig.IngressHostPrefix}.0.0/16", "-j", "ACCEPT",
                "iptables", "-A", "FORWARD", "-s", $"{AppConfig.IngressHostPrefix}.0.0/16", "-j", "ACCEPT");
            await EnsureIptablesRule("iptables", "-C", "FORWARD", "-d", $"{AppConfig.IngressHostPrefix}.0.0/16", "-j", "ACCEPT",
                "iptables", "-A", "FORWARD", "-d", $"{AppConfig.IngressHostPrefix}.0.0/16", "-j", "ACCEPT");
            await EnsureIptablesRule("iptables", "-t", "nat", "-C", "POSTROUTING", "-s", $"{AppConfig.IngressHostPrefix}.0.0/16", "!", "-o", HostVeth(0), "-j", "MASQUERADE",
                "iptables", "-t", "nat", "-A", "POSTROUTING", "-s", $"{AppConfig.IngressHostPrefix}.0.0/16", "!", "-o", HostVeth(0), "-j", "MASQUERADE");
        }

        var ports = await DiscoverIngressPorts();
        var configs = AppConfig.TunnelType == "wireguard" && AppConfig.IngressConfigDir.Exists
            ? AppConfig.IngressConfigDir.GetFiles("*.conf").OrderBy(file => file.Name, StringComparer.Ordinal).ToArray()
            : [];

        if (ports.Count == 0)
        {
            Console.Error.WriteLine("No slskdN listener ports found");
            return 2;
        }
        if (AppConfig.TunnelType == "wireguard" && configs.Length == 0)
        {
            Console.Error.WriteLine($"No ingress VPN configs found in {AppConfig.IngressConfigDir.FullName}");
            return 3;
        }

        var ok = 0;
        var limit = AppConfig.TunnelType == "wireguard" ? Math.Min(ports.Count, configs.Length) : ports.Count;
        for (var i = 0; i < limit; i++)
        {
            var port = ports[i];
            if (AppConfig.TunnelType == "wireguard")
            {
                Console.Error.WriteLine($"pf{i}: setting up {configs[i].Name} for {port.Protocol}/{port.PrivatePort} -> {port.TargetPort}");
                await SetupIngressSlot(i, configs[i], port);
            }
            else
            {
                Console.Error.WriteLine($"pf{i}: using {AppConfig.TunnelType} tunnel interface {AppConfig.VpnIface} for {port.Protocol}/{port.PrivatePort} -> {port.TargetPort}");
            }

            if (await ClaimIngressSlot(i, port))
            {
                ok++;
            }
        }

        for (var i = limit; i < AppConfig.MaxIngressSlots; i++)
        {
            await CleanupIngressSlot(i);
            var stale = AppConfig.StateFile($"pf{i}.env");
            if (stale.Exists)
            {
                stale.Delete();
            }
        }

        await File.WriteAllTextAsync(AppConfig.StateFile("summary.env").FullName, $"claimed={ok}\nmode={AppConfig.IngressMode}\n");
        foreach (var state in AppConfig.StateDir.GetFiles("pf*.env").OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            Console.Write(await File.ReadAllTextAsync(state.FullName));
        }

        return ok > 0 ? 0 : 4;
    }

    public static async Task<int> CleanupIngress()
    {
        for (var i = 0; i < AppConfig.MaxIngressSlots; i++)
        {
            await CleanupIngressSlot(i);
        }

        return 0;
    }

    public static async Task<int> Watchdog()
    {
        Directory.CreateDirectory(AppConfig.StateDir.FullName);
        var failFile = AppConfig.StateFile("watchdog.failures");
        var result = await Verify(quiet: true);
        if (result == 0)
        {
            if (failFile.Exists)
            {
                failFile.Delete();
            }
            await ProcessUtil.Run("logger", "-t", AppConfig.WatchdogLogTag, "slskdN VPN verification OK");
            return 0;
        }

        var failures = 0;
        if (failFile.Exists)
        {
            _ = int.TryParse(await File.ReadAllTextAsync(failFile.FullName), out failures);
        }
        failures++;
        await File.WriteAllTextAsync(failFile.FullName, $"{failures}\n");
        await ProcessUtil.Run("logger", "-t", AppConfig.WatchdogLogTag, $"slskdN VPN verification failed ({failures}/{AppConfig.WatchdogThreshold})");

        if (failures >= AppConfig.WatchdogThreshold)
        {
            await ProcessUtil.Run("logger", "-t", AppConfig.WatchdogLogTag, $"restarting {AppConfig.IngressService} after repeated failures");
            await ProcessUtil.Run("systemctl", "restart", AppConfig.IngressService);
            await File.WriteAllTextAsync(failFile.FullName, "0\n");
        }

        return 0;
    }

    private static async Task HandleApiRequest(HttpListenerContext context)
    {
        var state = EnvFile.Read(AppConfig.StateFile("pf0.env"));
        var path = context.Request.Url?.AbsolutePath ?? "";
        var forwards = ReadPortForwards();
        if (path == "/v1/publicip/ip")
        {
            var publicIp = state.GetValueOrDefault("public_ip", "");
            if (string.IsNullOrWhiteSpace(publicIp))
            {
                await JsonResponse(context, 503, new { public_ip = "" });
                return;
            }

            await JsonResponse(context, 200, new
            {
                public_ip = publicIp,
                city = "",
                country = AppConfig.Country,
                region = "",
                location = "",
                organization = "VPN",
                postal_code = "",
                timezone = ""
            });
            return;
        }

        if (path is "/v1/portforward" or "/v1/openvpn/portforwarded")
        {
            var portText = state.GetValueOrDefault("public_port", "0");
            _ = int.TryParse(portText, out var port);
            await JsonResponse(context, 200, new { port });
            return;
        }

        if (path is "/v1/slskdn/portforwards" or "/v1/slskdN/portforwards")
        {
            var summary = EnvFile.Read(AppConfig.StateFile("summary.env"));
            _ = int.TryParse(summary.GetValueOrDefault("claimed", "0"), out var claimed);
            await JsonResponse(context, 200, new
            {
                mode = summary.GetValueOrDefault("mode", AppConfig.IngressMode),
                claimed,
                forwards
            });
            return;
        }

        if (path is "/v1/openvpn/status" or "/v1/wireguard/status" or "/v1/vpn/status")
        {
            await JsonResponse(context, 200, new { status = state.ContainsKey("public_ip") ? "running" : "stopped" });
            return;
        }

        await JsonResponse(context, 404, new { error = "not found" });
    }

    private static List<PortForwardState> ReadPortForwards()
    {
        var forwards = new List<PortForwardState>();
        foreach (var file in AppConfig.StateDir.GetFiles("pf*.env").OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            var match = Regex.Match(file.Name, @"^pf(\d+)\.env$");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var slot))
            {
                continue;
            }

            var state = EnvFile.Read(file);
            if (!int.TryParse(state.GetValueOrDefault("local_port", ""), out var localPort) ||
                !int.TryParse(state.GetValueOrDefault("target_port", ""), out var targetPort) ||
                !int.TryParse(state.GetValueOrDefault("public_port", ""), out var publicPort))
            {
                continue;
            }

            forwards.Add(new PortForwardState(
                slot,
                localPort,
                targetPort,
                state.GetValueOrDefault("proto", ""),
                publicPort,
                state.GetValueOrDefault("public_ip", ""),
                state.GetValueOrDefault("namespace", "")));
        }

        return forwards;
    }

    private static async Task JsonResponse(HttpListenerContext context, int status, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json.Options);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static string? Option(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static async Task MustRun(params string[] args)
    {
        var result = await ProcessUtil.Run(args);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{string.Join(' ', args)} failed: {result.Stderr.Trim()}");
        }
    }

    private static async Task RequireCommand(string command)
    {
        if (!await ProcessUtil.CommandExists(command))
        {
            throw new InvalidOperationException($"missing command: {command}");
        }
    }

    private static async Task EnsureIptablesRule(string checkCommand, params string[] args)
    {
        var separator = Array.IndexOf(args, checkCommand);
        if (separator < 0)
        {
            throw new InvalidOperationException("invalid iptables rule arguments");
        }
        var check = args.Take(separator).Prepend(checkCommand).ToArray();
        var add = args.Skip(separator + 1).Prepend(checkCommand).ToArray();
        if ((await ProcessUtil.Run(check)).ExitCode != 0)
        {
            await MustRun(add);
        }
    }

    private static async Task<List<IngressPort>> DiscoverIngressPorts()
    {
        var discovered = new SortedSet<int>();
        var sockets = await ProcessUtil.Run("ss", "-H", "-ltnup");
        var exclude = new Regex(AppConfig.ExcludePortRegex, RegexOptions.Compiled);

        foreach (var line in sockets.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains($"users:((\"{AppConfig.ProcessName}\"", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                continue;
            }

            var local = parts[4];
            var portText = local.Split(':').LastOrDefault() ?? "";
            if (int.TryParse(portText, out var port) && !exclude.IsMatch(portText))
            {
                discovered.Add(port);
            }
        }

        var soulseekTarget = await SoulseekListenPort();
        var allPorts = new List<IngressPort>
        {
            new(AppConfig.SoulseekPrivatePort, soulseekTarget, "tcp")
        };

        foreach (var port in discovered)
        {
            if (port == AppConfig.SoulseekPrivatePort || port == soulseekTarget)
            {
                continue;
            }

            var tcp = await ProcessUtil.Run("ss", "-H", "-ltnp", "sport", "=", $":{port}");
            allPorts.Add(new IngressPort(port, port, tcp.Stdout.Contains($"users:((\"{AppConfig.ProcessName}\"", StringComparison.Ordinal) ? "tcp" : "udp"));
        }

        if (AppConfig.IngressMode == "all")
        {
            return allPorts;
        }

        var compactPorts = AppConfig.IngressMode == "core"
            ? allPorts.Where(port => port.Protocol == "tcp").ToList()
            : new List<IngressPort> { allPorts[0] };
        var udpPorts = allPorts.Where(port => port.Protocol == "udp").ToArray();
        var selectedUdp = AppConfig.CompactUdpPort is > 0
            ? udpPorts.FirstOrDefault(port => port.PrivatePort == AppConfig.CompactUdpPort || port.TargetPort == AppConfig.CompactUdpPort)
            : udpPorts.FirstOrDefault();

        if (selectedUdp is not null)
        {
            compactPorts.Add(selectedUdp);
        }

        var skipped = allPorts
            .Except(compactPorts)
            .Select(port => $"{port.Protocol}/{port.PrivatePort}->{port.TargetPort}")
            .ToArray();
        if (skipped.Length > 0)
        {
            Console.Error.WriteLine($"{AppConfig.IngressMode} ingress mode: not forwarding {string.Join(", ", skipped)}");
        }

        return compactPorts;
    }

    private static async Task<int> SoulseekListenPort()
    {
        var key = Slskd.ApiKey();
        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                var options = await Slskd.GetJson<SlskdOptions>("/api/v0/options", key);
                if (options?.Soulseek?.ListenPort is > 0 and var port)
                {
                    return port;
                }
            }
            catch
            {
                // Fall back to the private stable listener below.
            }
        }

        return AppConfig.SoulseekPrivatePort;
    }

    private static async Task CleanupIngressSlot(int index)
    {
        var ns = Namespace(index);
        var vhost = HostVeth(index);
        var table = (12100 + index).ToString();
        var pids = await ProcessUtil.Run("ip", "netns", "pids", ns);
        foreach (var pid in pids.Stdout.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            await ProcessUtil.Run("kill", pid);
        }
        await ProcessUtil.Run("ip", "netns", "del", ns);
        await ProcessUtil.Run("ip", "link", "del", vhost);
        foreach (var legacyPrefix in AppConfig.LegacyIngressNamespacePrefixes)
        {
            await ProcessUtil.Run("ip", "netns", "del", $"{legacyPrefix}{index}");
            await ProcessUtil.Run("ip", "link", "del", $"v-{legacyPrefix}{index}h");
        }
        await ProcessUtil.Run("ip", "rule", "del", "pref", table, "from", $"{AppConfig.IngressHostPrefix}.{index}.1/32", "lookup", table);
        await ProcessUtil.Run("ip", "route", "flush", "table", table);
    }

    private static async Task SetupIngressSlot(int index, FileInfo config, IngressPort port)
    {
        var ns = Namespace(index);
        var wg = $"pf{index}";
        var vhost = HostVeth(index);
        var vns = NamespaceVeth(index);
        var hostIp = $"{AppConfig.IngressHostPrefix}.{index}.1";
        var nsIp = $"{AppConfig.IngressHostPrefix}.{index}.2";
        var table = (12100 + index).ToString();

        await CleanupIngressSlot(index);
        await MustRun("ip", "netns", "add", ns);
        await MustRun("ip", "link", "add", vhost, "type", "veth", "peer", "name", vns);
        await MustRun("ip", "link", "set", vns, "netns", ns);
        await MustRun("ip", "addr", "add", $"{hostIp}/30", "dev", vhost);
        await MustRun("ip", "link", "set", vhost, "up");
        await MustRun("ip", "netns", "exec", ns, "ip", "addr", "add", $"{nsIp}/30", "dev", vns);
        await MustRun("ip", "netns", "exec", ns, "ip", "link", "set", "lo", "up");
        await MustRun("ip", "netns", "exec", ns, "ip", "link", "set", vns, "up");
        await ProcessUtil.Run("ip", "link", "del", wg);
        await MustRun("ip", "link", "add", wg, "type", "wireguard");

        var stripped = await ProcessUtil.Run("wg-quick", "strip", config.FullName);
        if (stripped.ExitCode != 0)
        {
            throw new InvalidOperationException($"wg-quick strip {config.FullName} failed: {stripped.Stderr.Trim()}");
        }
        var tempConfig = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempConfig, stripped.Stdout);
            await MustRun("wg", "setconf", wg, tempConfig);
        }
        finally
        {
            File.Delete(tempConfig);
        }

        await MustRun("ip", "link", "set", wg, "netns", ns);
        await MustRun("ip", "netns", "exec", ns, "ip", "addr", "add", "10.2.0.2/32", "dev", wg);
        await MustRun("ip", "netns", "exec", ns, "ip", "link", "set", "mtu", "1280", "up", "dev", wg);
        await MustRun("ip", "netns", "exec", ns, "ip", "route", "replace", "default", "dev", wg);
        await MustRun("ip", "netns", "exec", ns, "ip", "route", "replace", $"{hostIp}/32", "dev", vns);
        await MustRun("ip", "route", "replace", "default", "via", nsIp, "dev", vhost, "table", table);
        await ProcessUtil.Run("ip", "rule", "add", "pref", table, "from", $"{hostIp}/32", "lookup", table);
        await ProcessUtil.Run("ip", "netns", "exec", ns, "sysctl", "-qw", "net.ipv4.ip_forward=1");
        await ProcessUtil.Run("ip", "netns", "exec", ns, "iptables", "-t", "nat", "-F", "PREROUTING");
        await MustRun("ip", "netns", "exec", ns, "iptables", "-t", "nat", "-A", "PREROUTING", "-p", port.Protocol, "--dport", port.PrivatePort.ToString(), "-j", "DNAT", "--to-destination", $"{hostIp}:{port.TargetPort}");
    }

    private static async Task<bool> ClaimIngressSlot(int index, IngressPort port)
    {
        return AppConfig.PortForwardBackend switch
        {
            "natpmp" => await ClaimNatPmp(index, port),
            "static" => await ClaimStatic(index, port),
            _ => throw new InvalidOperationException($"unsupported VPN_PORT_FORWARD_BACKEND={AppConfig.PortForwardBackend}")
        };
    }

    private static async Task<bool> ClaimNatPmp(int index, IngressPort port)
    {
        var ns = Namespace(index);
        var state = EnvFile.Read(AppConfig.StateFile($"pf{index}.env"));
        state.TryGetValue("public_port", out var oldPublic);

        for (var attempt = 0; attempt < AppConfig.PortForwardAttempts; attempt++)
        {
            if (!string.IsNullOrWhiteSpace(oldPublic))
            {
                var renewed = await NatPmp(index, ns, oldPublic, port);
                if (renewed.PublicPort == oldPublic)
                {
                    await WriteIngressState(index, port, renewed.PublicPort, renewed.PublicIp);
                    Console.Error.WriteLine($"pf{index}: renewed {port.Protocol} public {renewed.PublicPort} -> private {port.PrivatePort} -> target {port.TargetPort}");
                    return true;
                }
            }

            var claimed = await NatPmp(index, ns, "0", port);
            if (!string.IsNullOrWhiteSpace(claimed.PublicPort))
            {
                await WriteIngressState(index, port, claimed.PublicPort, claimed.PublicIp);
                Console.Error.WriteLine($"pf{index}: {port.Protocol} public {claimed.PublicPort} -> private {port.PrivatePort} -> target {port.TargetPort}");
                return true;
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return false;
    }

    private static async Task<NatPmpResult> NatPmp(int index, string ns, string requestedPublicPort, IngressPort port)
    {
        if (string.IsNullOrWhiteSpace(AppConfig.ProviderGateway))
        {
            throw new InvalidOperationException("PF_GATEWAY is required when VPN_PORT_FORWARD_BACKEND=natpmp");
        }

        var args = AppConfig.TunnelType == "wireguard"
            ? new[] { "ip", "netns", "exec", ns, "timeout", "8", "natpmpc", "-g", AppConfig.ProviderGateway, "-a", requestedPublicPort, port.PrivatePort.ToString(), port.Protocol, AppConfig.PortForwardLifetime.ToString() }
            : new[] { "timeout", "8", "natpmpc", "-g", AppConfig.ProviderGateway, "-a", requestedPublicPort, port.PrivatePort.ToString(), port.Protocol, AppConfig.PortForwardLifetime.ToString() };
        var result = await ProcessUtil.Run(args);
        var output = result.Stdout + result.Stderr;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Console.Error.WriteLine($"pf{index} {port.Protocol}.{port.PrivatePort}: {line}");
        }

        var mapped = Regex.Match(output, @"Mapped public port\s+(\d+)");
        var publicIp = Regex.Match(output, @"Public IP address\s*:\s*(\S+)");
        return new NatPmpResult(
            mapped.Success ? mapped.Groups[1].Value : "",
            publicIp.Success ? publicIp.Groups[1].Value : "");
    }

    private static async Task<bool> ClaimStatic(int index, IngressPort port)
    {
        var config = new FileInfo(Path.Combine(AppConfig.StaticForwardDir.FullName, $"pf{index}.env"));
        var values = EnvFile.Read(config);
        if (values.Count == 0)
        {
            Console.Error.WriteLine($"pf{index}: missing static forward config {config.FullName}");
            return false;
        }

        var publicPort = values.GetValueOrDefault("public_port", "");
        var publicIp = values.GetValueOrDefault("public_ip", "");
        var localPort = values.GetValueOrDefault("local_port", "");
        var proto = values.GetValueOrDefault("proto", "");
        if (!int.TryParse(publicPort, out _) || string.IsNullOrWhiteSpace(publicIp))
        {
            Console.Error.WriteLine($"pf{index}: {config.FullName} must define public_port and public_ip");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(localPort) && localPort != port.PrivatePort.ToString())
        {
            Console.Error.WriteLine($"pf{index}: {config.FullName} local_port={localPort} does not match discovered private port {port.PrivatePort}");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(proto) && proto != port.Protocol)
        {
            Console.Error.WriteLine($"pf{index}: {config.FullName} proto={proto} does not match discovered protocol {port.Protocol}");
            return false;
        }

        await WriteIngressState(index, port, publicPort, publicIp);
        Console.Error.WriteLine($"pf{index}: static {port.Protocol} public {publicPort} -> private {port.PrivatePort} -> target {port.TargetPort}");
        return true;
    }

    private static Task WriteIngressState(int index, IngressPort port, string publicPort, string publicIp)
    {
        var content = $"""
        local_port={port.PrivatePort}
        target_port={port.TargetPort}
        proto={port.Protocol}
        public_port={publicPort}
        public_ip={publicIp}
        namespace={(AppConfig.TunnelType == "wireguard" ? Namespace(index) : "")}

        """;
        return File.WriteAllTextAsync(AppConfig.StateFile($"pf{index}.env").FullName, content);
    }

    private static string Namespace(int index) => $"{AppConfig.IngressNamespacePrefix}{index}";

    private static string HostVeth(int index) => $"v-{AppConfig.IngressNamespacePrefix}{index}h";

    private static string NamespaceVeth(int index) => $"v-{AppConfig.IngressNamespacePrefix}{index}n";

    private static async Task DeleteRuleUntilGone(params string[] args)
    {
        for (var i = 0; i < 50; i++)
        {
            var fullArgs = new[] { "ip", "rule", "del" }.Concat(args).ToArray();
            var result = await ProcessUtil.Run(fullArgs);
            if (result.ExitCode != 0)
            {
                return;
            }
        }
    }

    private static IEnumerable<string> ResolvConfNameservers()
    {
        const string resolvConf = "/etc/resolv.conf";
        if (!File.Exists(resolvConf))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(resolvConf))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0] == "nameserver")
            {
                yield return parts[1];
            }
        }
    }
}

static class AppConfig
{
    public static string SlskdUrl { get; } = Env.Get("SLSKD_API_URL", "http://127.0.0.1:5030");
    public static FileInfo SlskdConfig { get; } = new(Env.Get("SLSKD_CONFIG", "/etc/slskd/slskd.yml"));
    public static string ServiceUser { get; } = Env.GetAny(["SLSKDN_SERVICE_USER", "SLSKD_USER"], "slskd");
    public static int ServiceUid => ResolveServiceUid();
    public static string ProcessName { get; } = Env.GetAny(["SLSKDN_PROCESS_NAME", "SLSKD_PROCESS_NAME"], "slskd");
    public static string ApplicationService { get; } = Env.GetAny(["SLSKDN_SERVICE_NAME", "SLSKD_SERVICE_NAME"], "slskd");
    public static string ApplicationPath { get; } = Env.Get("SLSKDN_APP_PATH", "");
    public static string TunnelType { get; } = Env.Get("SLSKDN_VPN_TUNNEL_TYPE", "wireguard").Trim().ToLowerInvariant();
    public static string VpnIface { get; } = Env.GetAny(["SLSKDN_VPN_IFACE", "SLSKD_VPN_IFACE"], "slskdN-vpn");
    public static string VpnTable { get; } = Env.GetAny(["SLSKDN_VPN_TABLE", "SLSKD_VPN_TABLE"], "51820");
    public static string TunnelService { get; } = Env.Get("SLSKDN_VPN_TUNNEL_SERVICE", TunnelType == "wireguard" ? $"wg-quick@{VpnIface}" : "");
    public static string ProviderGateway { get; } = Env.Get("PF_GATEWAY", TunnelType == "wireguard" ? "10.2.0.1" : "");
    public static string[] LocalCidrs { get; } = Env.GetAny(["SLSKDN_LOCAL_CIDRS", "SLSKD_LOCAL_CIDRS"], "127.0.0.0/8 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public static DirectoryInfo StateDir { get; } = new(Env.Get("SLSKDN_VPN_STATE_DIR", "/var/lib/slskdN-vpn"));
    public static DirectoryInfo IngressConfigDir { get; } = new(Env.Get("SLSKDN_VPN_INGRESS_CONFIG_DIR", "/etc/wireguard/slskdN-vpn-ingress"));
    public static DirectoryInfo StaticForwardDir { get; } = new(Env.GetAny(["SLSKDN_VPN_STATIC_FORWARD_DIR", "VPN_STATIC_FORWARD_DIR"], "/etc/slskdN-vpn/static-forwards"));
    public static string IngressHostPrefix { get; } = Env.Get("SLSKDN_VPN_INGRESS_HOST_PREFIX", "10.251");
    public static string IngressNamespacePrefix { get; } = Env.Get("SLSKDN_VPN_INGRESS_NAMESPACE_PREFIX", "slskdNpf");
    public static string[] LegacyIngressNamespacePrefixes { get; } = Env.Get("SLSKDN_VPN_LEGACY_INGRESS_NAMESPACE_PREFIXES", "slskdpf")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public static string PortForwardBackend { get; } = Env.Get("VPN_PORT_FORWARD_BACKEND", "natpmp");
    public static string IngressMode { get; } = Env.Get("SLSKDN_VPN_INGRESS_MODE", "core").Trim().ToLowerInvariant();
    public static int CompactUdpPort { get; } = Env.GetInt("SLSKDN_VPN_COMPACT_UDP_PORT", 0);
    public static string ExcludePortRegex { get; } = Env.Get("PF_EXCLUDE_RE", "^(5030|5031|5353)$");
    public static int PortForwardLifetime { get; } = Env.GetInt("PF_LIFETIME", 60);
    public static int PortForwardAttempts { get; } = Env.GetInt("PF_ATTEMPTS", 90);
    public static int SoulseekPrivatePort { get; } = Env.GetInt("SOULSEEK_PRIVATE_PORT", 50300);
    public static int MaxIngressSlots { get; } = Env.GetInt("SLSKDN_VPN_MAX_INGRESS_SLOTS", 20);
    public static string Country { get; } = Env.Get("SLSKDN_VPN_INGRESS_REGION", "");
    public static int VerifyTimeoutSeconds { get; } = Env.GetIntAny(["SLSKDN_VPN_VERIFY_TIMEOUT", "SLSKD_VPN_VERIFY_TIMEOUT"], 10);
    public static int WatchdogThreshold { get; } = Env.GetIntAny(["SLSKDN_VPN_WATCHDOG_THRESHOLD", "SLSKD_VPN_WATCHDOG_THRESHOLD"], 3);
    public static string SplitService { get; } = Env.Get("SLSKDN_VPN_SPLIT_SERVICE", "slskdN-vpn-split");
    public static string ApiService { get; } = Env.Get("SLSKDN_VPN_API_SERVICE", "slskdN-vpn-gluetun-compat");
    public static string IngressService { get; } = Env.GetAny(["SLSKDN_VPN_INGRESS_SERVICE", "SLSKD_VPN_INGRESS_SERVICE"], "slskdN-vpn-ingress.service");
    public static string IngressRenewTimer { get; } = Env.Get("SLSKDN_VPN_INGRESS_RENEW_TIMER", "slskdN-vpn-ingress-renew.timer");
    public static string WatchdogLogTag { get; } = Env.Get("SLSKDN_VPN_WATCHDOG_LOG_TAG", "slskdN-vpn-watchdog");
    public static string PfAnchorName { get; } = Env.Get("SLSKDN_VPN_PF_ANCHOR", "slskdN/vpn");
    public static FileInfo StateFile(string name) => new(Path.Combine(StateDir.FullName, name));

    private static int ResolveServiceUid()
    {
        var fromEnv = Env.GetIntNullable("SLSKDN_SERVICE_UID") ?? Env.GetIntNullable("SLSKD_UID");
        if (fromEnv.HasValue)
        {
            return fromEnv.Value;
        }

        foreach (var line in File.ReadLines("/etc/passwd"))
        {
            var parts = line.Split(':');
            if (parts.Length >= 3 && parts[0] == ServiceUser && int.TryParse(parts[2], out var uid))
            {
                return uid;
            }
        }

        throw new InvalidOperationException($"could not resolve UID for service user '{ServiceUser}'; set SLSKDN_SERVICE_UID");
    }
}

static class Env
{
    public static string Get(string name, string fallback) => Environment.GetEnvironmentVariable(name) ?? fallback;

    public static string GetAny(string[] names, string fallback)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return fallback;
    }

    public static int GetInt(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public static int GetIntAny(string[] names, int fallback)
    {
        foreach (var name in names)
        {
            var value = GetIntNullable(name);
            if (value.HasValue)
            {
                return value.Value;
            }
        }
        return fallback;
    }

    public static int? GetIntNullable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}

static class Slskd
{
    public static string ApiKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable("SLSKD_API_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        try
        {
            var inApi = false;
            foreach (var line in File.ReadLines(AppConfig.SlskdConfig.FullName))
            {
                var trimmed = line.Trim();
                if (trimmed == "api_keys:")
                {
                    inApi = true;
                    continue;
                }
                if (inApi && trimmed.StartsWith("jwt:", StringComparison.Ordinal))
                {
                    return "";
                }
                if (inApi && trimmed.StartsWith("key:", StringComparison.Ordinal))
                {
                    return trimmed.Split(':', 2)[1].Trim();
                }
            }
        }
        catch (IOException)
        {
            return "";
        }
        catch (UnauthorizedAccessException)
        {
            return "";
        }

        return "";
    }

    public static async Task<T?> GetJson<T>(string path, string apiKey)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);
        return await client.GetFromJsonAsync<T>(AppConfig.SlskdUrl.TrimEnd('/') + path, Json.Options);
    }
}

static class Http
{
    public static async Task<string> Text(string url, int timeoutSeconds)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        return (await client.GetStringAsync(url)).Trim();
    }
}

static class EnvFile
{
    public static Dictionary<string, string> Read(FileSystemInfo path)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path.FullName))
        {
            return data;
        }

        foreach (var line in File.ReadLines(path.FullName))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('='))
            {
                continue;
            }
            var parts = trimmed.Split('=', 2);
            data[parts[0].Trim()] = parts[1].Trim();
        }
        return data;
    }
}

static class ProcessUtil
{
    public static async Task<bool> CommandExists(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            var windowsResult = await Run("where.exe", name);
            return windowsResult.ExitCode == 0;
        }

        var result = await Run("sh", "-c", $"command -v {EscapeForShell(name)} >/dev/null 2>&1");
        return result.ExitCode == 0;
    }

    public static async Task<ProcessResult> Run(params string[] args)
    {
        var psi = new ProcessStartInfo(args[0])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args.Skip(1))
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {args[0]}");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string EscapeForShell(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
}

sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

sealed record IngressPort(int PrivatePort, int TargetPort, string Protocol);

sealed record NatPmpResult(string PublicPort, string PublicIp);

sealed record PortForwardState(
    int Slot,
    int LocalPort,
    int TargetPort,
    string Proto,
    int PublicPort,
    string PublicIp,
    string Namespace);

sealed class Verifier(bool quiet)
{
    public int Failures { get; private set; }

    public void Say(string message)
    {
        if (!quiet)
        {
            Console.WriteLine(message);
        }
    }

    public void Pass(string message) => Say($"PASS: {message}");

    public void Fail(string message)
    {
        Failures++;
        Say($"FAIL: {message}");
    }

    public void Warn(string message) => Say($"WARN: {message}");

    public void Check(bool condition, string ok, string bad)
    {
        if (condition)
        {
            Pass(ok);
        }
        else
        {
            Fail(bad);
        }
    }
}

sealed class SlskdApplication
{
    [JsonPropertyName("server")]
    public SlskdServer? Server { get; set; }

    [JsonPropertyName("vpn")]
    public VpnState? Vpn { get; set; }
}

sealed class SlskdServer
{
    [JsonPropertyName("state")]
    public string? State { get; set; }
}

sealed class VpnState
{
    [JsonPropertyName("isReady")]
    public bool IsReady { get; set; }

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; }

    [JsonPropertyName("forwardedPort")]
    public int? ForwardedPort { get; set; }
}

sealed class SlskdOptions
{
    [JsonPropertyName("soulseek")]
    public SoulseekOptions? Soulseek { get; set; }
}

sealed class SoulseekOptions
{
    [JsonPropertyName("listenPort")]
    public int? ListenPort { get; set; }
}

static class Json
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
