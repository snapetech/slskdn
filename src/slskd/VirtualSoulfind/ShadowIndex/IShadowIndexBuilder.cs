using System.Threading;
using System.Threading.Tasks;

namespace slskd.VirtualSoulfind.ShadowIndex;

public interface IShadowIndexBuilder
{
    Task AddVariantObservationAsync(string username, string recordingId, slskd.Audio.AudioVariant variant, CancellationToken ct = default);
    Task<ShadowIndexShard?> BuildShardAsync(string recordingId, CancellationToken ct = default);
}















