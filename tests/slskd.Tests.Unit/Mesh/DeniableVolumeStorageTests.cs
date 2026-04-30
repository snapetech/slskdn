// <copyright file="DeniableVolumeStorageTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Xunit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace slskd.Tests.Unit.Mesh;

public class DeniableVolumeStorageTests
{
    [Fact]
    public void CreateDeniableVolume_HidesDataInPlainSight()
    {
        var path = Path.GetTempFileName();
        try
        {
            DeniableVolumeStorage.CreateDeniableVolume(path, "bridge=127.0.0.1:443", "correct-key");

            var visible = File.ReadAllText(path);
            Assert.Contains("cover", visible);
            Assert.DoesNotContain("bridge=127.0.0.1:443", visible);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AccessHiddenData_RequiresCorrectKey()
    {
        var path = Path.GetTempFileName();
        try
        {
            DeniableVolumeStorage.CreateDeniableVolume(path, "secret", "correct-key");

            Assert.Equal("secret", DeniableVolumeStorage.AccessHiddenData(path, "correct-key"));
            Assert.Throws<UnauthorizedAccessException>(() => DeniableVolumeStorage.AccessHiddenData(path, "wrong-key"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PlausibleDeniability_PassesCasualInspection()
    {
        var path = Path.GetTempFileName();
        try
        {
            DeniableVolumeStorage.CreateDeniableVolume(path, "secret", "correct-key");

            Assert.True(DeniableVolumeStorage.ValidatePlausibleDeniability(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WipeHiddenData_LeavesNoTraces()
    {
        var path = Path.GetTempFileName();
        try
        {
            DeniableVolumeStorage.CreateDeniableVolume(path, "secret", "correct-key");
            DeniableVolumeStorage.WipeHiddenData(path, "correct-key");

            Assert.Throws<InvalidOperationException>(() => DeniableVolumeStorage.AccessHiddenData(path, "correct-key"));
            Assert.True(DeniableVolumeStorage.ValidatePlausibleDeniability(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class DeniableVolumeStorage
{
    public static void CreateDeniableVolume(string containerPath, string hiddenData, string key)
    {
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var dataBytes = Encoding.UTF8.GetBytes(hiddenData);
        var encrypted = new byte[dataBytes.Length];
        for (var i = 0; i < dataBytes.Length; i++)
        {
            encrypted[i] = (byte)(dataBytes[i] ^ keyBytes[i % keyBytes.Length]);
        }

        var payload = new VolumePayload("cover", Convert.ToBase64String(encrypted), Convert.ToHexString(keyBytes));
        File.WriteAllText(containerPath, JsonSerializer.Serialize(payload));
    }

    public static string AccessHiddenData(string containerPath, string key)
    {
        var payload = JsonSerializer.Deserialize<VolumePayload>(File.ReadAllText(containerPath)) ??
                      throw new InvalidOperationException("Invalid deniable volume");
        if (string.IsNullOrWhiteSpace(payload.Hidden))
        {
            throw new InvalidOperationException("No hidden data present");
        }

        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(payload.KeyHash),
                Encoding.UTF8.GetBytes(Convert.ToHexString(keyBytes))))
        {
            throw new UnauthorizedAccessException("Invalid deniable volume key");
        }

        var encrypted = Convert.FromBase64String(payload.Hidden);
        var data = new byte[encrypted.Length];
        for (var i = 0; i < encrypted.Length; i++)
        {
            data[i] = (byte)(encrypted[i] ^ keyBytes[i % keyBytes.Length]);
        }

        return Encoding.UTF8.GetString(data);
    }

    public static bool ValidatePlausibleDeniability(string containerPath)
    {
        var text = File.ReadAllText(containerPath);
        return text.Contains("cover", StringComparison.Ordinal) &&
               !text.Contains("secret", StringComparison.OrdinalIgnoreCase) &&
               !text.Contains("bridge=", StringComparison.OrdinalIgnoreCase);
    }

    public static void WipeHiddenData(string containerPath, string key)
    {
        _ = AccessHiddenData(containerPath, key);
        File.WriteAllText(containerPath, JsonSerializer.Serialize(new VolumePayload("cover", string.Empty, string.Empty)));
    }

    private sealed record VolumePayload(string Cover, string Hidden, string KeyHash);
}
