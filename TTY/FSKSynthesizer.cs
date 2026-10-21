using System;
using System.Collections.Generic;

namespace TtyClient.TTY;

/// <summary>
/// Synthesizes FSK audio samples (1400 Hz MARK / 1800 Hz SPACE) at 45.45 baud.
/// Produces 16-bit PCM at 8000 Hz sample rate.
/// Frame: 1 START(SPACE) + 5 data bits + 1.5 STOP(MARK).
/// </summary>
public class FSKSynthesizer
{
    private const int SampleRate = 8000;
    private const double Baud = 45.45;
    private const double FreqMark = 1400.0;
    private const double FreqSpace = 1800.0;
    private const double Amplitude = 25000.0; // 16-bit scale

    // Samples per bit at 45.45 baud and 8000 Hz
    private static readonly int SamplesPerBit = (int)Math.Round(SampleRate / Baud); // 176

    private double _phase;

    /// <summary>
    /// Converts a list of Baudot codes to a PCM byte array (16-bit little-endian, 8000 Hz mono).
    /// </summary>
    public byte[] Synthesize(List<byte> baudotCodes)
    {
        var samples = new List<short>();
        _phase = 0;

        foreach (byte code in baudotCodes)
            AppendCharacter(samples, code);

        // 200ms of idle MARK at the end
        AppendTone(samples, FreqMark, (int)(SampleRate * 0.2));

        // Convert short[] to byte[]
        var bytes = new byte[samples.Count * 2];
        for (int i = 0; i < samples.Count; i++)
        {
            bytes[i * 2] = (byte)(samples[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)(samples[i] >> 8);
        }
        return bytes;
    }

    private void AppendCharacter(List<short> samples, byte code)
    {
        // START bit: SPACE
        AppendTone(samples, FreqSpace, SamplesPerBit);

        // 5 data bits LSB first
        for (int bit = 0; bit < 5; bit++)
        {
            bool isMark = ((code >> bit) & 1) == 1;
            AppendTone(samples, isMark ? FreqMark : FreqSpace, SamplesPerBit);
        }

        // STOP bits: MARK (1.5 bit periods)
        AppendTone(samples, FreqMark, (int)(SamplesPerBit * 1.5));
    }

    private void AppendTone(List<short> samples, double freq, int count)
    {
        double phaseIncrement = 2.0 * Math.PI * freq / SampleRate;
        for (int i = 0; i < count; i++)
        {
            samples.Add((short)(Amplitude * Math.Sin(_phase)));
            _phase += phaseIncrement;
            if (_phase > 2.0 * Math.PI)
                _phase -= 2.0 * Math.PI;
        }
    }
}
