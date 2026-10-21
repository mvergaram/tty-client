namespace TtyClient.TTY;

/// <summary>
/// Canonical ITA-2 (Baudot) code tables — the single source of truth shared by
/// <see cref="BaudotDecoder"/> and <see cref="BaudotEncoder"/>.
///
/// ITA-2 is normally published as five bits written "bit1 bit2 bit3 bit4 bit5", where
/// bit 1 is the first bit on the wire. These tables are indexed by that same code read
/// as an integer with **bit 1 as the least significant bit**, which is exactly the order
/// <see cref="BitSynchronizer"/> assembles (first data bit -> LSB) and
/// <see cref="FSKSynthesizer"/> transmits (LSB first). So e.g. 'A' = 11000 -> 0b00011 = 3,
/// 'O' = 00011 -> 0b11000 = 24.
///
/// Indices <see cref="Figs"/> (27) and <see cref="Ltrs"/> (31) are shift codes, not
/// printable characters, and are left as '\0' in both tables; callers must intercept them
/// before indexing. Entries that are unassigned (or assigned to a control function this
/// client does not render) are also '\0'.
///
/// Keeping both tables here, side by side and index-aligned, is deliberate: when they were
/// maintained separately as hand-written literals, the letters from index 21 upward drifted
/// one slot out of alignment, which silently mistranslated Y/P/Q/O/B/G/M/X/V on every
/// received character.
/// </summary>
internal static class BaudotTables
{
    public const byte LineFeed = 2;
    public const byte Space = 4;
    public const byte CarriageReturn = 8;
    public const byte Figs = 27;
    public const byte Ltrs = 31;

    /// <summary>ITA-2 letters shift, indexed by 5-bit code (0-31).</summary>
    public static readonly char[] Letters =
    {
        /* 00 */ '\0', /* 01 */ 'E',  /* 02 */ '\n', /* 03 */ 'A',
        /* 04 */ ' ',  /* 05 */ 'S',  /* 06 */ 'I',  /* 07 */ 'U',
        /* 08 */ '\r', /* 09 */ 'D',  /* 10 */ 'R',  /* 11 */ 'J',
        /* 12 */ 'N',  /* 13 */ 'F',  /* 14 */ 'C',  /* 15 */ 'K',
        /* 16 */ 'T',  /* 17 */ 'Z',  /* 18 */ 'L',  /* 19 */ 'W',
        /* 20 */ 'H',  /* 21 */ 'Y',  /* 22 */ 'P',  /* 23 */ 'Q',
        /* 24 */ 'O',  /* 25 */ 'B',  /* 26 */ 'G',  /* 27 */ '\0', // FIGS
        /* 28 */ 'M',  /* 29 */ 'X',  /* 30 */ 'V',  /* 31 */ '\0'  // LTRS
    };

    /// <summary>ITA-2 figures shift, indexed by 5-bit code (0-31).</summary>
    public static readonly char[] Figures =
    {
        /* 00 */ '\0', /* 01 */ '3',  /* 02 */ '\n', /* 03 */ '-',
        /* 04 */ ' ',  /* 05 */ '\'', /* 06 */ '8',  /* 07 */ '7',
        /* 08 */ '\r', /* 09 */ '\0', /* 10 */ '4',  /* 11 */ '\0',
        /* 12 */ ',',  /* 13 */ '\0', /* 14 */ ':',  /* 15 */ '(',
        /* 16 */ '5',  /* 17 */ '"',  /* 18 */ ')',  /* 19 */ '2',
        /* 20 */ '#',  /* 21 */ '6',  /* 22 */ '0',  /* 23 */ '1',
        /* 24 */ '9',  /* 25 */ '?',  /* 26 */ '&',  /* 27 */ '\0', // FIGS
        /* 28 */ '.',  /* 29 */ '/',  /* 30 */ ';',  /* 31 */ '\0'  // LTRS
    };
}
