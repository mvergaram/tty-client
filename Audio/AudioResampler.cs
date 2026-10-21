using System;

namespace TtyClient.Audio;

/// <summary>
/// Converts IEEE float32 stereo/mono at any sample rate to 16-bit mono at 8000 Hz.
/// Uses linear interpolation for resampling.
/// </summary>
public class AudioResampler
{
    private readonly int _inputSampleRate;
    private readonly int _inputChannels;
    private double _position;

    public AudioResampler(int inputSampleRate, int inputChannels)
    {
        _inputSampleRate = inputSampleRate;
        _inputChannels = inputChannels;
    }

    /// <summary>
    /// Converts a buffer of float32 samples to 16-bit mono at 8000 Hz.
    /// Input buffer contains interleaved float32 samples.
    /// </summary>
    public short[] Resample(byte[] inputBuffer, int byteCount)
    {
        int inputFrames = byteCount / (_inputChannels * sizeof(float));
        var monoFloat = new float[inputFrames];

        // Downmix to mono
        for (int i = 0; i < inputFrames; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < _inputChannels; ch++)
            {
                int offset = (i * _inputChannels + ch) * sizeof(float);
                sum += BitConverter.ToSingle(inputBuffer, offset);
            }
            monoFloat[i] = sum / _inputChannels;
        }

        // Resample to 8000 Hz with linear interpolation
        double ratio = (double)_inputSampleRate / 8000.0;
        int outputFrames = (int)Math.Ceiling((inputFrames - _position) / ratio);
        if (outputFrames < 0) outputFrames = 0;
        var output = new short[outputFrames];

        for (int i = 0; i < outputFrames; i++)
        {
            int idx = (int)_position;
            double frac = _position - idx;

            float a = idx < monoFloat.Length ? monoFloat[idx] : 0;
            float b = (idx + 1) < monoFloat.Length ? monoFloat[idx + 1] : a;
            float sample = (float)(a + frac * (b - a));

            output[i] = (short)Math.Clamp(sample * 32767.0, -32768, 32767);
            _position += ratio;
        }

        _position -= inputFrames;
        if (_position < 0) _position = 0;

        return output;
    }
}
