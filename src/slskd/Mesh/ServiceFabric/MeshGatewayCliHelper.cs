// <copyright file="MeshGatewayCliHelper.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Security.Cryptography;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// CLI helper for generating mesh gateway keys and tokens.
/// </summary>
public static class MeshGatewayCliHelper
{
    /// <summary>
    /// Generates and prints a new API key for the mesh gateway.
    /// Can be called from CLI: slskd generate-gateway-key
    /// </summary>
    public static void GenerateAndPrintApiKey()
    {
        var apiKey = GenerateSecureToken(32);
        var csrfToken = GenerateSecureToken(24);

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                  Mesh Gateway Keys Generated                         ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Add these to your configuration file (appsettings.yml or appsettings.json):");
        Console.WriteLine();
        Console.WriteLine("YAML format (appsettings.yml):");
        Console.WriteLine("------------------------------");
        Console.WriteLine("MeshGateway:");
        Console.WriteLine("  Enabled: true");
        Console.WriteLine($"  ApiKey: \"{apiKey}\"");
        Console.WriteLine($"  CsrfToken: \"{csrfToken}\"");
        Console.WriteLine("  BindAddress: \"127.0.0.1\"  # Change with caution!");
        Console.WriteLine("  AllowedServices:");
        Console.WriteLine("    - \"pods\"");
        Console.WriteLine("    - \"shadow-index\"");
        Console.WriteLine("    - \"mesh-introspect\"");
        Console.WriteLine();
        Console.WriteLine("JSON format (appsettings.json):");
        Console.WriteLine("--------------------------------");
        Console.WriteLine("{");
        Console.WriteLine("  \"MeshGateway\": {");
        Console.WriteLine("    \"Enabled\": true,");
        Console.WriteLine($"    \"ApiKey\": \"{apiKey}\",");
        Console.WriteLine($"    \"CsrfToken\": \"{csrfToken}\",");
        Console.WriteLine("    \"BindAddress\": \"127.0.0.1\",");
        Console.WriteLine("    \"AllowedServices\": [\"pods\", \"shadow-index\", \"mesh-introspect\"]");
        Console.WriteLine("  }");
        Console.WriteLine("}");
        Console.WriteLine();
        Console.WriteLine("Client usage:");
        Console.WriteLine("-------------");
        Console.WriteLine($"curl -H \"X-Slskdn-ApiKey: {apiKey}\" \\");
        Console.WriteLine($"     -H \"X-Slskdn-Csrf: {csrfToken}\" \\");
        Console.WriteLine("     -X POST http://localhost:5000/mesh/http/pods/List \\");
        Console.WriteLine("     -H \"Content-Type: application/json\" \\");
        Console.WriteLine("     -d '{}'");
        Console.WriteLine();
        Console.WriteLine("⚠️  SECURITY NOTES:");
        Console.WriteLine("- Keep these keys SECRET");
        Console.WriteLine("- Do not commit to version control");
        Console.WriteLine("- Only bind to non-localhost if you understand the risks");
        Console.WriteLine("- Use proper firewall rules if exposing remotely");
        Console.WriteLine();
    }

    /// <summary>
    /// Generates a cryptographically secure random token.
    /// </summary>
    private static string GenerateSecureToken(int byteLength = 32)
    {
        var bytes = new byte[byteLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
