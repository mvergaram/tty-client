using System;

namespace TtyClient.TTY;

/// <summary>
/// State machine that reconstructs Baudot characters from a stream of MARK/SPACE bit decisions.
/// TTY frame: 1 START bit (SPACE) + 5 data bits (LSB first) + 1.5 STOP bits (MARK).
/// At 45.45 baud, one bit = ~22ms. The GoertzelDetector emits one decision per 20ms block.
/// </summary>
public class BitSynchronizer
{
    private enum State { Idle, StartBit, DataBits, StopBit }

    private State _state = State.Idle;
    private int _dataBuffer;
    private int _bitCount;
    private int _stopBitCount;

    private int _holdCount;

    public event Action<byte>? CharacterReceived;

    // Called once per 20ms block with the dominant tone decision
    public void OnBit(bool isMark)
    {
        switch (_state)
        {
            case State.Idle:
                // Wait for a SPACE (start bit transition from idle MARK)
                if (!isMark)
                {
                    _state = State.StartBit;
                    _holdCount = 1;
                }
                break;

            case State.StartBit:
                // Confirm the start bit lasts ~1 bit period
                if (!isMark)
                {
                    _holdCount++;
                    if (_holdCount >= 1) // 1 block ≈ 20ms ≈ 1 bit
                    {
                        _dataBuffer = 0;
                        _bitCount = 0;
                        _holdCount = 0;
                        _state = State.DataBits;
                    }
                }
                else
                {
                    // False start
                    _state = State.Idle;
                }
                break;

            case State.DataBits:
                // Sample each bit; 5 bits LSB first
                _dataBuffer |= (isMark ? 1 : 0) << _bitCount;
                _bitCount++;
                if (_bitCount == 5)
                {
                    _stopBitCount = 0;
                    _state = State.StopBit;
                }
                break;

            case State.StopBit:
                // Expect MARK (stop condition) for 1.5 bit periods ≈ 1-2 blocks
                if (isMark)
                {
                    _stopBitCount++;
                    if (_stopBitCount >= 1)
                    {
                        CharacterReceived?.Invoke((byte)_dataBuffer);
                        _state = State.Idle;
                    }
                }
                else
                {
                    // Could be back-to-back character; treat as new start bit
                    CharacterReceived?.Invoke((byte)_dataBuffer);
                    _dataBuffer = 0;
                    _bitCount = 0;
                    _holdCount = 1;
                    _state = State.StartBit;
                }
                break;
        }
    }

    public void Reset()
    {
        _state = State.Idle;
        _dataBuffer = 0;
        _bitCount = 0;
        _holdCount = 0;
        _stopBitCount = 0;
    }
}
