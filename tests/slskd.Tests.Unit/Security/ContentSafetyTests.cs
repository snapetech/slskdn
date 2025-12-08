// <copyright file="ContentSafetyTests.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Security;

using System;
using slskd.Common.Security;
using Xunit;

public class ContentSafetyTests
{
    // FLAC magic bytes
    private static readonly byte[] FlacMagic = { 0x66, 0x4C, 0x61, 0x43 }; // "fLaC"
    
    // MP3 ID3v2 magic
    private static readonly byte[] Mp3Id3Magic = { 0x49, 0x44, 0x33 }; // "ID3"
    
    // MP3 frame sync
    private static readonly byte[] Mp3FrameMagic = { 0xFF, 0xFB };
    
    // PE/DOS executable
    private static readonly byte[] PeMagic = { 0x4D, 0x5A }; // "MZ"
    
    // ELF executable
    private static readonly byte[] ElfMagic = { 0x7F, 0x45, 0x4C, 0x46 }; // "\x7FELF"
    
    // PNG
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    
    // OGG
    private static readonly byte[] OggMagic = { 0x4F, 0x67, 0x67, 0x53 }; // "OggS"

    [Fact]
    public void VerifyHeader_FlacExtension_FlacContent_ReturnsValid()
    {
        var header = CreateHeader(FlacMagic);
        var result = ContentSafety.VerifyHeader(header, ".flac");
        
        Assert.True(result.IsValid);
        Assert.False(result.IsWarning);
        Assert.Equal(ContentThreatLevel.Safe, result.ThreatLevel);
    }

    [Fact]
    public void VerifyHeader_Mp3Extension_Id3Content_ReturnsValid()
    {
        var header = CreateHeader(Mp3Id3Magic);
        var result = ContentSafety.VerifyHeader(header, ".mp3");
        
        Assert.True(result.IsValid);
        Assert.Equal(ContentThreatLevel.Safe, result.ThreatLevel);
    }

    [Fact]
    public void VerifyHeader_Mp3Extension_FrameContent_ReturnsValid()
    {
        var header = CreateHeader(Mp3FrameMagic);
        var result = ContentSafety.VerifyHeader(header, ".mp3");
        
        Assert.True(result.IsValid);
    }

    [Fact]
    public void VerifyHeader_OggExtension_OggContent_ReturnsValid()
    {
        var header = CreateHeader(OggMagic);
        var result = ContentSafety.VerifyHeader(header, ".ogg");
        
        Assert.True(result.IsValid);
    }

    [Fact]
    public void VerifyHeader_FlacExtension_PeContent_ReturnsDangerous()
    {
        var header = CreateHeader(PeMagic);
        var result = ContentSafety.VerifyHeader(header, ".flac");
        
        Assert.False(result.IsValid);
        Assert.Equal(ContentThreatLevel.Dangerous, result.ThreatLevel);
        Assert.Contains("executable", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyHeader_Mp3Extension_ElfContent_ReturnsDangerous()
    {
        var header = CreateHeader(ElfMagic);
        var result = ContentSafety.VerifyHeader(header, ".mp3");
        
        Assert.False(result.IsValid);
        Assert.Equal(ContentThreatLevel.Dangerous, result.ThreatLevel);
    }

    [Fact]
    public void VerifyHeader_PngExtension_PeContent_ReturnsDangerous()
    {
        var header = CreateHeader(PeMagic);
        var result = ContentSafety.VerifyHeader(header, ".png");
        
        Assert.False(result.IsValid);
        Assert.Equal(ContentThreatLevel.Dangerous, result.ThreatLevel);
    }

    [Fact]
    public void VerifyHeader_ExeExtension_PeContent_ReturnsExecutableWarning()
    {
        var header = CreateHeader(PeMagic);
        var result = ContentSafety.VerifyHeader(header, ".exe");
        
        // EXE with PE content is valid but flagged as executable
        Assert.True(result.IsValid);
        Assert.True(result.IsWarning);
        Assert.Equal(ContentThreatLevel.Executable, result.ThreatLevel);
    }

    [Fact]
    public void VerifyHeader_FlacExtension_UnknownContent_ReturnsMismatch()
    {
        var header = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 };
        var result = ContentSafety.VerifyHeader(header, ".flac");
        
        Assert.True(result.IsValid); // Not blocked, just warned
        Assert.True(result.IsWarning);
        Assert.Equal(ContentThreatLevel.Mismatch, result.ThreatLevel);
    }

    [Fact]
    public void IsExecutable_PeContent_ReturnsTrue()
    {
        var header = CreateHeader(PeMagic);
        Assert.True(ContentSafety.IsExecutable(header));
    }

    [Fact]
    public void IsExecutable_ElfContent_ReturnsTrue()
    {
        var header = CreateHeader(ElfMagic);
        Assert.True(ContentSafety.IsExecutable(header));
    }

    [Fact]
    public void IsExecutable_FlacContent_ReturnsFalse()
    {
        var header = CreateHeader(FlacMagic);
        Assert.False(ContentSafety.IsExecutable(header));
    }

    [Fact]
    public void IsExecutable_ShellScript_ReturnsTrue()
    {
        // "#!/bin/bash"
        var header = new byte[] { 0x23, 0x21, 0x2F, 0x62, 0x69, 0x6E, 0x2F, 0x62 };
        Assert.True(ContentSafety.IsExecutable(header));
    }

    [Fact]
    public void DetectFileType_FlacContent_ReturnsFlac()
    {
        var header = CreateHeader(FlacMagic);
        var type = ContentSafety.DetectFileType(header);
        
        Assert.NotNull(type);
        Assert.Contains("FLAC", type, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectFileType_PeContent_ReturnsPe()
    {
        var header = CreateHeader(PeMagic);
        var type = ContentSafety.DetectFileType(header);
        
        Assert.NotNull(type);
        Assert.Contains("executable", type, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectFileType_UnknownContent_ReturnsNull()
    {
        var header = new byte[] { 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x9A };
        var type = ContentSafety.DetectFileType(header);
        
        Assert.Null(type);
    }

    [Fact]
    public void VerifyHeader_TooShortHeader_ReturnsUnknown()
    {
        var header = new byte[] { 0x00 };
        var result = ContentSafety.VerifyHeader(header, ".flac");
        
        Assert.False(result.IsValid);
        Assert.Equal(ContentThreatLevel.Unknown, result.ThreatLevel);
    }

    [Fact]
    public void VerifyHeader_NullHeader_ReturnsUnknown()
    {
        var result = ContentSafety.VerifyHeader(null!, ".flac");
        
        Assert.False(result.IsValid);
        Assert.Equal(ContentThreatLevel.Unknown, result.ThreatLevel);
    }

    [Fact]
    public void VerifyHeader_NullExtension_ReturnsUnknown()
    {
        var header = CreateHeader(FlacMagic);
        var result = ContentSafety.VerifyHeader(header, null!);
        
        Assert.False(result.IsValid);
        Assert.Equal(ContentThreatLevel.Unknown, result.ThreatLevel);
    }

    private static byte[] CreateHeader(byte[] magic)
    {
        // Create a 16-byte header with magic bytes at the beginning
        var header = new byte[16];
        Array.Copy(magic, header, magic.Length);
        return header;
    }
}

