// <copyright file="AudioCore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.AudioCore
{
    /// <summary>
    ///     Defines the AudioCore API boundary: the set of interfaces and types that constitute
    ///     the audio domain module. T-913. Enables testing, replacement, and future VideoCore/ImageCore symmetry.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Fingerprinting</b>: <see cref="slskd.Integrations.Chromaprint.IChromaprintService"/>,
    ///         <see cref="slskd.Integrations.Chromaprint.IFingerprintExtractionService"/> (maps to design IAudioFingerprinter).
    ///     </para>
    ///     <para>
    ///         <b>Variants and storage</b>: <see cref="slskd.HashDb.IHashDbService"/>,
    ///         <see cref="slskd.MediaCore.IMediaVariantStore"/> (Music domain; maps to design IAudioVariantStore).
    ///     </para>
    ///     <para>
    ///         <b>Canonical and health</b>: <see cref="slskd.Audio.ICanonicalStatsService"/>,
    ///         <see cref="slskd.LibraryHealth.ILibraryHealthService"/>,
    ///         <see cref="slskd.LibraryHealth.Remediation.ILibraryHealthRemediationService"/>.
    ///     </para>
    ///     <para>
    ///         <b>Migration and dedupe</b>: <see cref="slskd.Audio.IAnalyzerMigrationService"/>,
    ///         <see cref="slskd.Audio.IDedupeService"/>.
    ///     </para>
    ///     <para>
    ///         <b>Music domain provider</b>: <see cref="slskd.VirtualSoulfind.Core.Music.IMusicContentDomainProvider"/>,
    ///         <see cref="slskd.VirtualSoulfind.Core.Music.MusicContentDomainProvider"/>.
    ///     </para>
    ///     <para>
    ///         <b>Analyzers</b>: <see cref="slskd.Audio.Analyzers.FlacAnalyzer"/>,
    ///         <see cref="slskd.Audio.Analyzers.Mp3Analyzer"/>,
    ///         <see cref="slskd.Audio.Analyzers.OpusAnalyzer"/>,
    ///         <see cref="slskd.Audio.Analyzers.AacAnalyzer"/> (used internally by <see cref="slskd.HashDb.HashDbService"/>).
    ///     </para>
    /// </remarks>
    public static class AudioCore
    {
    }
}
