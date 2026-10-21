using System;

namespace TtyClient.TTY;

/// <summary>
/// Decodes Baudot ITA-2 5-bit codes to ASCII characters.
/// Manages LTRS/FIGS shift state.
/// Code tables live in <see cref="BaudotTables"/>, shared with <see cref="BaudotEncoder"/>.
/// </summary>
public class BaudotDecoder
{
    private bool _figureShift;

    public event Action<char>? CharacterDecoded;

    public void OnByte(byte code)
    {
        if (code == BaudotTables.Ltrs)
        {
            _figureShift = false;
            return;
        }
        if (code == BaudotTables.Figs)
        {
            _figureShift = true;
            return;
        }

        char ch = _figureShift ? BaudotTables.Figures[code] : BaudotTables.Letters[code];
        if (ch != '\0')
            CharacterDecoded?.Invoke(ch);
    }

    public void Reset()
    {
        _figureShift = false;
    }
}
