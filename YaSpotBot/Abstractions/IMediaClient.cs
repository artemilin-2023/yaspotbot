using YaSpotBot.Models;

namespace YaSpotBot.Abstractions;

internal interface IMediaClient
{
    Task<TrackInfo> GetTrackInfo(string trackId, CancellationToken cancellationToken);
    Task<string> GetUriAsync(TrackInfo info, CancellationToken cancellationToken);
}
