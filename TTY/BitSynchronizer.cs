using System;

namespace TtyClient.TTY;

/// <summary>
/// Reconstructs Baudot characters from a stream of oversampled MARK/SPACE decisions.
/// TTY frame: 1 START bit (SPACE) + 5 data bits (LSB first) + 1.5 STOP bits (MARK).
///
/// Async start-stop framing only fixes bit timing *within* one character; the gap after
/// the stop bit (idle mark, or a human typist's pause) is unconstrained. So instead of
/// counting bits from a free-running clock since the start of the stream, this locks onto
/// each character's own START bit edge and samples every subsequent bit at a fixed offset
/// *from that edge* - exactly how a real asynchronous serial receiver resynchronizes on
/// every start bit. That makes it immune to drift from the 1.5-bit stop period (or any
/// other inter-character gap), which a single global bit counter cannot tolerate.
/// </summary>
public class BitSynchronizer
{
    // Must match GoertzelDetector.OversampleFactor: OnBit is called once per quarter bit
    // period, and bits are sampled at their mid-point (quarter-tick index 2 of 0..3).
    private const int OversampleFactor = GoertzelDetector.OversampleFactor;
    private const int SampleTickPhase = OversampleFactor / 2;

    private enum State { Idle, Framing }

    private State _state = State.Idle;
    private int _tick;
    private int _dataBuffer;

    public event Action<byte>? CharacterReceived;

    // Called once per quarter-bit period with the dominant tone decision for that slice.
    public void OnBit(bool isMark)
    {
        if (_state == State.Idle)
        {
            if (isMark) return; // still idle (MARK), keep waiting for a start-bit edge
            // Falling edge candidate: SPACE right after idle MARK. Lock onto it - this
            // quarter-tick becomes tick 0 of the new frame's START bit.
            _state = State.Framing;
            _tick = 0;
            _dataBuffer = 0;
        }

        int bitIndex = _tick / OversampleFactor; // 0 = start bit, 1..5 = data bits, 6 = stop bit
        int phase = _tick % OversampleFactor;

        if (phase == SampleTickPhase)
        {
            if (bitIndex == 0)
            {
                if (isMark)
                {
                    // The edge didn't hold to the start bit's center - false trigger (noise).
                    _state = State.Idle;
                    return;
                }
            }
            else if (bitIndex <= 5)
            {
                _dataBuffer |= (isMark ? 1 : 0) << (bitIndex - 1);
            }
            else // bitIndex == 6: stop bit center
            {
                CharacterReceived?.Invoke((byte)_dataBuffer);
                // Whether or not a clean MARK stop was observed, go back to Idle and look
                // for the next start-bit edge fresh - this naturally handles both a normal
                // (>=1 bit) stop gap and back-to-back framing with no gap at all.
                _state = State.Idle;
                return;
            }
        }

        _tick++;
    }

    public void Reset()
    {
        _state = State.Idle;
        _tick = 0;
        _dataBuffer = 0;
    }
}
