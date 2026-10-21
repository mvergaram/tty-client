using NAudio.CoreAudioApi;

namespace TtyClient.Audio;

/// <summary>
/// Un endpoint de audio seleccionable como "Entrada de audio".
/// Render  => se captura por loopback (audio de una llamada SfB que suena por ese dispositivo).
/// Capture => se captura directo (micrófono, handset USB, CABLE Output…).
/// </summary>
public sealed record AudioEndpointInfo(string Id, string FriendlyName, DataFlow Flow)
{
    public bool IsLoopback => Flow == DataFlow.Render;

    public string DisplayName => IsLoopback
        ? $"{FriendlyName}  —  Salida (loopback)"
        : $"{FriendlyName}  —  Micrófono";
}
