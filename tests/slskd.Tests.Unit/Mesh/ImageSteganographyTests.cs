// <copyright file="ImageSteganographyTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class ImageSteganographyTests
{
    [Fact]
    public void EmbedBridgeInfo_HidesDataInImage()
    {
        var image = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();

        ImageSteganography.EmbedBridgeInfo(image, "obfs4 192.0.2.1:443", out var stegoImage);

        Assert.NotEqual(image, stegoImage);
        Assert.DoesNotContain("obfs4", Encoding.UTF8.GetString(stegoImage.Take(image.Length).ToArray()));
    }

    [Fact]
    public void ExtractBridgeInfo_RecoversHiddenData()
    {
        ImageSteganography.EmbedBridgeInfo(new byte[64], "bridge-info", out var stegoImage);

        Assert.Equal("bridge-info", ImageSteganography.ExtractBridgeInfo(stegoImage));
    }

    [Fact]
    public void ValidateImageIntegrity_DetectsTampering()
    {
        ImageSteganography.EmbedBridgeInfo(new byte[64], "bridge-info", out var stegoImage);
        Assert.True(ImageSteganography.ValidateImageIntegrity(stegoImage));

        stegoImage[^1] ^= 0xff;
        Assert.False(ImageSteganography.ValidateImageIntegrity(stegoImage));
    }
}

public class ImageSteganography
{
    public static void EmbedBridgeInfo(byte[] imageData, string bridgeInfo, out byte[] stegoImage)
    {
        var payload = Encoding.UTF8.GetBytes(bridgeInfo);
        var output = new List<byte>(imageData);
        output.AddRange(Marker);
        output.AddRange(BitConverter.GetBytes(payload.Length));
        output.AddRange(payload);
        output.AddRange(SHA256.HashData(payload));
        stegoImage = output.ToArray();
    }

    public static string ExtractBridgeInfo(byte[] stegoImage)
    {
        var (offset, length) = LocatePayload(stegoImage);
        return Encoding.UTF8.GetString(stegoImage, offset, length);
    }

    public static bool ValidateImageIntegrity(byte[] imageData)
    {
        try
        {
            var (offset, length) = LocatePayload(imageData);
            var expected = SHA256.HashData(imageData.AsSpan(offset, length).ToArray());
            var actual = imageData.AsSpan(offset + length, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    private static readonly byte[] Marker = Encoding.ASCII.GetBytes("SLSKDNSTEG");

    private static (int Offset, int Length) LocatePayload(byte[] imageData)
    {
        for (var i = 0; i <= imageData.Length - Marker.Length - sizeof(int); i++)
        {
            if (!imageData.AsSpan(i, Marker.Length).SequenceEqual(Marker))
            {
                continue;
            }

            var length = BitConverter.ToInt32(imageData, i + Marker.Length);
            var offset = i + Marker.Length + sizeof(int);
            if (length < 0 || offset + length + SHA256.HashSizeInBytes > imageData.Length)
            {
                throw new InvalidDataException("Invalid steganography payload");
            }

            return (offset, length);
        }

        throw new InvalidDataException("No steganography payload found");
    }
}
