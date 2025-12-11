namespace slskd.Integrations.MusicBrainz;

public class MbRecording
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? Length { get; set; }
    public List<MbArtistCredit> ArtistCredit { get; set; } = new();
}

public class MbRelease
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Date { get; set; }
    public string? Country { get; set; }
    public List<MbLabelInfo> LabelInfo { get; set; } = new();
    public List<MbMedium> Media { get; set; } = new();
}

public class MbArtistCredit
{
    public string? Name { get; set; }
    public MbArtist? Artist { get; set; }
}

public class MbArtist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SortName { get; set; }
}

public class MbLabelInfo
{
    public MbLabel? Label { get; set; }
}

public class MbLabel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class MbMedium
{
    public int Position { get; set; }
    public string? Format { get; set; }
    public int TrackCount { get; set; }
    public List<MbTrack> Tracks { get; set; } = new();
}

public class MbTrack
{
    public string Id { get; set; } = string.Empty;
    public string? Number { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Length { get; set; }
    public MbRecording? Recording { get; set; }
}

public class MbMediumTrack : MbTrack { }

public class MbSearchResults<T>
{
    public int Count { get; set; }
    public int Offset { get; set; }
    public List<T> Results { get; set; } = new();
}

