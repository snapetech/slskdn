namespace slskd.Integrations.MusicBrainz.Models
{
    /// <summary>
    ///     Aggregated label presence statistics based on locally known releases.
    /// </summary>
    public sealed class LabelPresence
    {
        /// <summary>Label name.</summary>
        public string Label { get; init; }

        /// <summary>Number of releases observed for this label.</summary>
        public int ReleaseCount { get; init; }
    }
}

