using System;

namespace TtyClient.TTY;

/// <summary>
/// Decodes Baudot ITA-2 5-bit codes to ASCII characters.
/// Manages LTRS/FIGS shift state.
/// </summary>
public class BaudotDecoder
{
    // ITA-2 LTRS table (index = 5-bit code, 0-31)
    private static readonly char[] LettersTable =
    {
        '\0', 'E',  '\n', 'A',  ' ',  'S',  'I',  'U',
        '\r', 'D',  'R',  'J',  'N',  'F',  'C',  'K',
        'T',  'Z',  'L',  'W',  'H',  '\0', 'Y',  'P',
        'Q',  'O',  'B',  'G',  '\0', 'M',  'X',  'V'
    };

    // ITA-2 FIGS table
    private static readonly char[] FiguresTable =
    {
        '\0', '3',  '\n', '-',  ' ',  '\'', '8',  '7',
        '\r', '\0', '4',  '\0', ',',  '\0', ':',  '(',
        '5',  '"',  ')',  '2',  '#',  '\0', '6',  '0',
        '1',  '9',  '?',  '&',  '\0', '.',  '/',  ';'
    };

    private const byte CodeLtrs = 31;
    private const byte CodeFigs = 27;

    private bool _figureShift;

    public event Action<char>? CharacterDecoded;

    public void OnByte(byte code)
    {
        if (code == CodeLtrs)
        {
            _figureShift = false;
            return;
        }
        if (code == CodeFigs)
        {
            _figureShift = true;
            return;
        }

        char ch = _figureShift ? FiguresTable[code] : LettersTable[code];
        if (ch != '\0')
            CharacterDecoded?.Invoke(ch);
    }

    public void Reset()
    {
        _figureShift = false;
    }
}
