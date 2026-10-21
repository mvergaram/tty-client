using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TtyClient.Audio;

/// <summary>
/// Captures either a render device via WASAPI loopback (audio of a softphone call playing
/// through speakers/headset) or a real capture device (microphone/handset), depending
/// on the selected AudioEndpointInfo. Defaults to the default communications render
/// device when none is selected. Fires SamplesAvailable with resampled 16-bit mono
/// at 8000 Hz.
/// </summary>
public class AudioCapture : IDisposable
{
    private IWaveIn? _capture;
    private AudioResampler? _resampler;
    private bool _disposed;

    public event Action<short[], int>? SamplesAvailable;
    public bool IsCapturing { get; private set; }
    public string? DeviceName { get; private set; }
    public bool IsLoopback { get; private set; }

    public List<AudioEndpointInfo> GetInputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var list = new List<AudioEndpointInfo>();

        foreach (var d in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            list.Add(new AudioEndpointInfo(d.ID, d.FriendlyName, DataFlow.Render));
        foreach (var d in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            list.Add(new AudioEndpointInfo(d.ID, d.FriendlyName, DataFlow.Capture));

        return list;
    }

    public void Start(AudioEndpointInfo? endpoint = null)
    {
        if (IsCapturing) return;

        using var enumerator = new MMDeviceEnumerator();
        MMDevice? device = null;

        if (endpoint is not null && !string.IsNullOrEmpty(endpoint.Id))
        {
            try
            {
                var d = enumerator.GetDevice(endpoint.Id);
                if (d.State == DeviceState.Active) device = d;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                device = null; // disconnected since the last refresh
            }
        }
        device ??= enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);

        DeviceName = device.FriendlyName;
        IsLoopback = device.DataFlow == DataFlow.Render;

        IWaveIn capture = IsLoopback
            ? new WasapiLoopbackCapture(device)
            : new WasapiCapture(device);

        EnsureSupportedFormat(capture.WaveFormat, DeviceName);

        var wf = capture.WaveFormat;
        _resampler = new AudioResampler(wf.SampleRate, wf.Channels);

        capture.DataAvailable += OnDataAvailable;
        capture.StartRecording();

        _capture = capture;
        IsCapturing = true;
    }

    private static void EnsureSupportedFormat(WaveFormat wf, string deviceName)
    {
        bool isFloat32 =
            wf.BitsPerSample == 32 &&
            (wf.Encoding == WaveFormatEncoding.IeeeFloat || wf.Encoding == WaveFormatEncoding.Extensible);

        if (isFloat32) return;

        throw new NotSupportedException(
            $"Device «{deviceName}» provides audio in {wf.Encoding} format at {wf.BitsPerSample} bits. " +
            "The decoder only supports 32-bit floating point. Try another device or change its " +
            "default format in Sound Settings → Device Properties → Advanced.");
    }

    public void Stop()
    {
        if (!IsCapturing) return;
        _capture?.StopRecording();
        IsCapturing = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;
        short[] samples = _resampler!.Resample(e.Buffer, e.BytesRecorded);
        if (samples.Length > 0)
            SamplesAvailable?.Invoke(samples, samples.Length);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _capture?.Dispose();
        _disposed = true;
    }
}
