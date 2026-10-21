using System;

namespace TtyClient.TTY;

/// <summary>
/// Detects power at 1400 Hz (MARK) and 1800 Hz (SPACE) using the Goertzel algorithm.
/// Runs on 20ms blocks at 8000 Hz sample rate (160 samples per block).
/// </summary>
public class GoertzelDetector
{
    private const int SampleRate = 8000;
    private const int BlockSize = 160; // 20ms at 8kHz
    private const double FreqMark = 1400.0;
    private const double FreqSpace = 1800.0;

    private readonly double _coeffMark;
    private readonly double _coeffSpace;

    private readonly short[] _buffer = new short[BlockSize];
    private int _bufferPos;

    public event Action<bool>? BitDetected; // true = MARK (1), false = SPACE (0)

    public GoertzelDetector()
    {
        _coeffMark = ComputeCoeff(FreqMark);
        _coeffSpace = ComputeCoeff(FreqSpace);
    }

    private static double ComputeCoeff(double freq)
    {
        double k = Math.Round(BlockSize * freq / SampleRate);
        double omega = 2.0 * Math.PI * k / BlockSize;
        return 2.0 * Math.Cos(omega);
    }

    public void ProcessSamples(short[] samples, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _buffer[_bufferPos++] = samples[i];
            if (_bufferPos == BlockSize)
            {
                ProcessBlock();
                _bufferPos = 0;
            }
        }
    }

    private void ProcessBlock()
    {
        double magMark = Goertzel(_buffer, _coeffMark);
        double magSpace = Goertzel(_buffer, _coeffSpace);
        BitDetected?.Invoke(magMark > magSpace);
    }

    private static double Goertzel(short[] samples, double coeff)
    {
        double q1 = 0, q2 = 0;
        for (int i = 0; i < BlockSize; i++)
        {
            double q0 = coeff * q1 - q2 + samples[i];
            q2 = q1;
            q1 = q0;
        }
        return Math.Sqrt(q1 * q1 + q2 * q2 - q1 * q2 * coeff);
    }
}
