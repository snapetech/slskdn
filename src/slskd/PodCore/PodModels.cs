namespace slskd.PodCore;

/// <summary>
/// Pod visibility.
/// </summary>
public enum PodVisibility
{
    Listed,
    Unlisted,
    Private
}

/// <summary>
/// Pod channel kind.
/// </summary>
public enum PodChannelKind
{
    General,
    Custom,
    Bound // e.g., bound to Soulseek room
}

/// <summary>
/// Pod metadata.
/// </summary>
public class Pod
{
    public string PodId { get; set; } = string.Empty; // pod:<hash>
    public string Name { get; set; } = string.Empty;
    public PodVisibility Visibility { get; set; } = PodVisibility.Unlisted;
    public string? FocusContentId { get; set; } // e.g., content:mb:recording:<mbid>
    public List<string> Tags { get; set; } = new();
    public List<PodChannel> Channels { get; set; } = new();
    public List<ExternalBinding> ExternalBindings { get; set; } = new();
}

public class PodChannel
{
    public string ChannelId { get; set; } = string.Empty;
    public PodChannelKind Kind { get; set; } = PodChannelKind.General;
    public string Name { get; set; } = string.Empty;
    public string? BindingInfo { get; set; } // e.g., soulseek-room:techno
}

public class ExternalBinding
{
    public string Kind { get; set; } = string.Empty; // soulseek-room
    public string Mode { get; set; } = "readonly"; // readonly | mirror
    public string Identifier { get; set; } = string.Empty; // room name/id
}

public class PodMember
{
    public string PeerId { get; set; } = string.Empty;
    public string Role { get; set; } = "member"; // owner|mod|member
    public bool IsBanned { get; set; }
}

public class PodMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string SenderPeerId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public long TimestampUnixMs { get; set; }
    public string Signature { get; set; } = string.Empty;
}

public class PodVariantOpinion
{
    public string ContentId { get; set; } = string.Empty;
    public string VariantHash { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Note { get; set; } = string.Empty;
    public string SenderPeerId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
