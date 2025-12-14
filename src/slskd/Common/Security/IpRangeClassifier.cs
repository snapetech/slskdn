// <copyright file="IpRangeClassifier.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace slskd.Common.Security;

/// <summary>
/// Classifies IP addresses into security-relevant categories for VPN and network access control.
/// </summary>
public static class IpRangeClassifier
{
    /// <summary>
    /// IP address classification results.
    /// </summary>
    public enum IpClassification
    {
        /// <summary>
        /// Public internet address (routable).
        /// </summary>
        Public,

        /// <summary>
        /// Private RFC1918 address (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16).
        /// </summary>
        PrivateRfc1918,

        /// <summary>
        /// IPv6 Unique Local Address (fc00::/7).
        /// </summary>
        PrivateUla,

        /// <summary>
        /// Loopback address (127.0.0.0/8, ::1).
        /// </summary>
        Loopback,

        /// <summary>
        /// Link-local address (169.254.0.0/16, fe80::/10).
        /// </summary>
        LinkLocal,

        /// <summary>
        /// Multicast address (224.0.0.0/4, ff00::/8).
        /// </summary>
        Multicast,

        /// <summary>
        /// Broadcast address (limited support).
        /// </summary>
        Broadcast,

        /// <summary>
        /// Cloud metadata service IP (169.254.169.254, etc.).
        /// </summary>
        CloudMetadata,

        /// <summary>
        /// Reserved or special purpose address.
        /// </summary>
        Reserved,

        /// <summary>
        /// Invalid or unparseable address.
        /// </summary>
        Invalid
    }

    /// <summary>
    /// Classifies an IP address string.
    /// </summary>
    /// <param name="ipString">The IP address string to classify.</param>
    /// <returns>The classification result.</returns>
    public static IpClassification Classify(string ipString)
    {
        if (string.IsNullOrWhiteSpace(ipString))
            return IpClassification.Invalid;

        if (!IPAddress.TryParse(ipString, out var ip))
            return IpClassification.Invalid;

        return Classify(ip);
    }

    /// <summary>
    /// Classifies an IPAddress object.
    /// </summary>
    /// <param name="ip">The IP address to classify.</param>
    /// <returns>The classification result.</returns>
    public static IpClassification Classify(IPAddress ip)
    {
        if (ip == null)
            return IpClassification.Invalid;

        // Check for cloud metadata services first (always blocked)
        if (IsCloudMetadata(ip))
            return IpClassification.CloudMetadata;

        // Check loopback
        if (IPAddress.IsLoopback(ip))
            return IpClassification.Loopback;

        if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
        {
            return ClassifyIpv4(ip);
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6) // IPv6
        {
            return ClassifyIpv6(ip);
        }

        return IpClassification.Invalid;
    }

    /// <summary>
    /// Determines if an IP address is considered "private" for VPN purposes.
    /// Private addresses are generally safe for tunneling within a pod.
    /// </summary>
    /// <param name="ipString">The IP address string to check.</param>
    /// <returns>True if the address is in a private range.</returns>
    public static bool IsPrivate(string ipString)
    {
        var classification = Classify(ipString);
        return classification == IpClassification.PrivateRfc1918 ||
               classification == IpClassification.PrivateUla;
    }

    /// <summary>
    /// Determines if an IP address is considered "private" for VPN purposes.
    /// </summary>
    /// <param name="ip">The IP address to check.</param>
    /// <returns>True if the address is in a private range.</returns>
    public static bool IsPrivate(IPAddress ip)
    {
        var classification = Classify(ip);
        return classification == IpClassification.PrivateRfc1918 ||
               classification == IpClassification.PrivateUla;
    }

    /// <summary>
    /// Determines if an IP address should be blocked for security reasons.
    /// </summary>
    /// <param name="ipString">The IP address string to check.</param>
    /// <returns>True if the address should be blocked.</returns>
    public static bool IsBlocked(string ipString)
    {
        var classification = Classify(ipString);
        return classification == IpClassification.Loopback ||
               classification == IpClassification.LinkLocal ||
               classification == IpClassification.Multicast ||
               classification == IpClassification.Broadcast ||
               classification == IpClassification.CloudMetadata ||
               classification == IpClassification.Reserved ||
               classification == IpClassification.Invalid;
    }

    /// <summary>
    /// Determines if an IP address should be blocked for security reasons.
    /// </summary>
    /// <param name="ip">The IP address to check.</param>
    /// <returns>True if the address should be blocked.</returns>
    public static bool IsBlocked(IPAddress ip)
    {
        var classification = Classify(ip);
        return classification == IpClassification.Loopback ||
               classification == IpClassification.LinkLocal ||
               classification == IpClassification.Multicast ||
               classification == IpClassification.Broadcast ||
               classification == IpClassification.CloudMetadata ||
               classification == IpClassification.Reserved ||
               classification == IpClassification.Invalid;
    }

    /// <summary>
    /// Determines if an IP address is safe for private service tunneling.
    /// Combines private range check with blocked address exclusion.
    /// </summary>
    /// <param name="ipString">The IP address string to check.</param>
    /// <returns>True if the address is safe for tunneling.</returns>
    public static bool IsSafeForTunneling(string ipString)
    {
        var classification = Classify(ipString);
        return classification == IpClassification.PrivateRfc1918 ||
               classification == IpClassification.PrivateUla ||
               classification == IpClassification.Public;
    }

    /// <summary>
    /// Determines if an IP address is safe for private service tunneling.
    /// </summary>
    /// <param name="ip">The IP address to check.</param>
    /// <returns>True if the address is safe for tunneling.</returns>
    public static bool IsSafeForTunneling(IPAddress ip)
    {
        var classification = Classify(ip);
        return classification == IpClassification.PrivateRfc1918 ||
               classification == IpClassification.PrivateUla ||
               classification == IpClassification.Public;
    }

    private static IpClassification ClassifyIpv4(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();

        // RFC1918 Private ranges
        if (bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168))
        {
            return IpClassification.PrivateRfc1918;
        }

        // Link-local (169.254.0.0/16) - but check for metadata first
        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return IpClassification.LinkLocal;
        }

        // Loopback (127.0.0.0/8) - already handled by IPAddress.IsLoopback

        // Multicast (224.0.0.0/4)
        if (bytes[0] >= 224 && bytes[0] <= 239)
        {
            return IpClassification.Multicast;
        }

        // Broadcast (255.255.255.255) - limited support
        if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255)
        {
            return IpClassification.Broadcast;
        }

        // Reserved ranges (0.0.0.0/8, 240.0.0.0/4, etc.)
        if (bytes[0] == 0 || bytes[0] >= 240)
        {
            return IpClassification.Reserved;
        }

        // Everything else is public
        return IpClassification.Public;
    }

    private static IpClassification ClassifyIpv6(IPAddress ip)
    {
        // Loopback (::1) - already handled by IPAddress.IsLoopback

        // Link-local (fe80::/10)
        if (ip.IsIPv6LinkLocal)
        {
            return IpClassification.LinkLocal;
        }

        // Unique Local Address (fc00::/7)
        if (ip.IsIPv6UniqueLocal)
        {
            return IpClassification.PrivateUla;
        }

        // Multicast (ff00::/8)
        if (ip.IsIPv6Multicast)
        {
            return IpClassification.Multicast;
        }

        // Everything else is public (for IPv6, most addresses are public)
        return IpClassification.Public;
    }

    private static bool IsCloudMetadata(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
        {
            var bytes = ip.GetAddressBytes();

            // AWS, Azure, GCP, DigitalOcean metadata service
            if (bytes[0] == 169 && bytes[1] == 254 && bytes[2] == 169 && bytes[3] == 254)
            {
                return true;
            }

            // Other cloud provider metadata IPs can be added here
        }

        // IPv6 cloud metadata services can be added here if needed

        return false;
    }

    /// <summary>
    /// Gets a human-readable description of an IP classification.
    /// </summary>
    /// <param name="classification">The classification to describe.</param>
    /// <returns>A human-readable description.</returns>
    public static string GetDescription(IpClassification classification)
    {
        return classification switch
        {
            IpClassification.Public => "Public internet address",
            IpClassification.PrivateRfc1918 => "Private RFC1918 address (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16)",
            IpClassification.PrivateUla => "Private IPv6 Unique Local Address (fc00::/7)",
            IpClassification.Loopback => "Loopback address (127.0.0.0/8, ::1)",
            IpClassification.LinkLocal => "Link-local address (169.254.0.0/16, fe80::/10)",
            IpClassification.Multicast => "Multicast address (224.0.0.0/4, ff00::/8)",
            IpClassification.Broadcast => "Broadcast address",
            IpClassification.CloudMetadata => "Cloud metadata service IP (169.254.169.254, etc.)",
            IpClassification.Reserved => "Reserved or special purpose address",
            IpClassification.Invalid => "Invalid or unparseable address",
            _ => "Unknown classification"
        };
    }
}

