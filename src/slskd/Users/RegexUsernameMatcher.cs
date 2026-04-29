// <copyright file="RegexUsernameMatcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Users;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

public sealed class RegexUsernameMatcher : IUsernameMatcher, IDisposable
{
    public RegexUsernameMatcher(
        IOptionsMonitor<global::slskd.Options> optionsMonitor,
        IMemoryCache memoryCache)
    {
        OptionsMonitor = optionsMonitor;
        Cache = memoryCache;
        Configure(optionsMonitor.CurrentValue);
        OptionsChangeRegistration = optionsMonitor.OnChange(Configure);
    }

    private IMemoryCache Cache { get; }
    private IOptionsMonitor<global::slskd.Options> OptionsMonitor { get; }
    private IDisposable? OptionsChangeRegistration { get; set; }
    private CompiledPatterns Patterns { get; set; } = CompiledPatterns.Empty;

    public bool IsMatch(string username)
    {
        if (Patterns.Expressions.Length == 0)
        {
            return false;
        }

        var cacheKey = $"blacklist-pattern:{Patterns.Signature}:{username}";
        return Cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);
            return Patterns.Expressions.Any(pattern => pattern.IsMatch(username));
        });
    }

    public void Dispose()
    {
        OptionsChangeRegistration?.Dispose();
    }

    private void Configure(global::slskd.Options options)
    {
        var rawPatterns = options.Groups.Blacklisted.Patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (rawPatterns.Length == 0)
        {
            Patterns = CompiledPatterns.Empty;
            return;
        }

        var expressions = rawPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            .ToArray();

        Patterns = new CompiledPatterns(CreateSignature(rawPatterns), expressions);
    }

    private static string CreateSignature(IEnumerable<string> patterns)
    {
        var joined = string.Join("\n", patterns);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash);
    }

    private sealed record CompiledPatterns(string Signature, Regex[] Expressions)
    {
        public static CompiledPatterns Empty { get; } = new(string.Empty, []);
    }
}
