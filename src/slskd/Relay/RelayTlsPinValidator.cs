// <copyright file="RelayTlsPinValidator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Relay;

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using slskd.Mesh.Transport;

/// <summary>
///     HARDENING-2026-04-20 H8-pin: validates that a presented X.509 certificate matches at
///     least one operator-configured SPKI pin.
/// </summary>
/// <remarks>
///     <para>
///         The Relay controller connection historically had two modes: fully CA-validated, or
///         constrained-bypass mode via <c>IgnoreCertificateErrors=true</c> which allows valid TLS
///         chains plus self-signed/untrusted-root chains. The bypass is convenient for lab setups
///         but unsafe on any network the operator doesn't fully own. Pinning adds a third mode:
///         "I don't care whether your CA chain verifies, but I *do* care that your SPKI matches this
///         exact fingerprint." This matches the mesh overlay's TOFU pinning model and defends against
///         MITM on controller→agent links even when the operator is using a private CA or self-signed
///         cert.
///     </para>
///     <para>
///         When <see cref="ParsePins"/> returns a non-empty array, callers must treat pin
///         validation as authoritative: the connection is allowed only if the presented cert's
///         SPKI pin matches one of the configured values. When pin list is empty, fall back to
///         legacy behavior (CA validation, or constrained bypass if <c>IgnoreCertificateErrors=true</c>).
///     </para>
/// </remarks>
public static class RelayTlsPinValidator
{
    /// <summary>
    ///     Parses a comma-separated pin string into a trimmed, de-duplicated array.
    /// </summary>
    /// <param name="pinnedSpki">Raw configuration value (may be null/empty).</param>
    /// <returns>Non-null array of pins (possibly empty).</returns>
    public static string[] ParsePins(string pinnedSpki)
    {
        if (string.IsNullOrWhiteSpace(pinnedSpki))
        {
            return Array.Empty<string>();
        }

        return pinnedSpki
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    ///     Returns true if <paramref name="certificate"/>'s SHA-256 SPKI pin matches one of
    ///     <paramref name="expectedPins"/>. Returns false for null cert, empty pin list, or
    ///     pin extraction failure — callers must treat "no pins configured" as a separate
    ///     state and not call this method in that case.
    /// </summary>
    public static bool IsPinned(X509Certificate2? certificate, string[] expectedPins)
    {
        if (certificate == null || expectedPins == null || expectedPins.Length == 0)
        {
            return false;
        }

        var pin = SecurityUtils.ExtractSpkiPin(certificate);
        if (string.IsNullOrEmpty(pin))
        {
            return false;
        }

        foreach (var expected in expectedPins)
        {
            if (string.Equals(pin, expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
