using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TtyClient.Audio;

/// <summary>
/// Plays synthesized FSK audio to a virtual audio device (e.g. VB-Audio Virtual Cable)
/// that Skype for Business uses as its microphone input.
/// </summary>
public class VirtualMicInjector : IDisposable
{
    private WasapiOut? _player;
    private bool _disposed;

    // 16-bit PCM mono at 8000 Hz
    private static readonly WaveFormat TtyFormat = new WaveFormat(8000, 16, 1);

    public List<string> GetVirtualDevices()
    {
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        return devices
            .Select(d => d.FriendlyName)
            .Where(n => n.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Plays FSK-encoded PCM bytes to the named output device.
    /// The device should be configured as SfB's microphone via Windows Sound settings.
    /// Pass null to use the default communications device.
    /// </summary>
    public void Play(byte[] pcmBytes, string? deviceName = null)
    {
        _player?.Stop();
        _player?.Dispose();

        MMDevice? device = null;
        if (deviceName != null)
        {
            var enumerator = new MMDeviceEnumerator();
            device = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .FirstOrDefault(d => d.FriendlyName.Contains(deviceName, StringComparison.OrdinalIgnoreCase));
        }

        _player = device != null
            ? new WasapiOut(device, AudioClientShareMode.Shared, true, 50)
            : new WasapiOut(AudioClientShareMode.Shared, true, 50);

        var provider = new RawSourceWaveStream(
            new System.IO.MemoryStream(pcmBytes), TtyFormat);

        _player.Init(provider);
        _player.Play();
    }

    public void Stop() => _player?.Stop();

    public void Dispose()
    {
        if (_disposed) return;
        _player?.Stop();
        _player?.Dispose();
        _disposed = true;
    }
}
