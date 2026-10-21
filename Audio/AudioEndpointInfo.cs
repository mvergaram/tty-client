using NAudio.CoreAudioApi;

namespace TtyClient.Audio;

/// <summary>
/// An audio endpoint selectable as "Audio input".
/// Render  => captured via loopback (audio from a softphone call playing through that device).
/// Capture => captured directly (microphone, USB handset, CABLE Output…).
/// </summary>
public sealed record AudioEndpointInfo(string Id, string FriendlyName, DataFlow Flow)
{
    public bool IsLoopback => Flow == DataFlow.Render;

    public string DisplayName => IsLoopback
        ? $"{FriendlyName}  —  Output (loopback)"
        : $"{FriendlyName}  —  Microphone";
}
