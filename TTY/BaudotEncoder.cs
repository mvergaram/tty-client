using System;
using System.Collections.Generic;

namespace TtyClient.TTY;

/// <summary>
/// Encodes ASCII text to a sequence of Baudot ITA-2 5-bit codes, managing LTRS/FIGS shift.
/// The character-to-code map is derived from <see cref="BaudotTables"/>, the same tables the
/// decoder uses, so the two can never drift apart.
/// </summary>
public static class BaudotEncoder
{
    // Map from character to (code, requiresFigs)
    private static readonly Dictionary<char, (byte Code, bool IsFigs)> CharMap = BuildCharMap();

    private static Dictionary<char, (byte, bool)> BuildCharMap()
    {
        var map = new Dictionary<char, (byte, bool)>();

        for (int i = 0; i < 32; i++)
        {
            if (i == BaudotTables.Figs || i == BaudotTables.Ltrs)
                continue; // shift codes, emitted explicitly

            char lc = BaudotTables.Letters[i];
            if (lc != '\0' && !map.ContainsKey(lc))
            {
                map[lc] = ((byte)i, false);
                if (char.IsLetter(lc))
                {
                    char lower = char.ToLowerInvariant(lc);
                    if (!map.ContainsKey(lower))
                        map[lower] = ((byte)i, false);
                }
            }

            char fc = BaudotTables.Figures[i];
            if (fc != '\0' && !map.ContainsKey(fc))
                map[fc] = ((byte)i, true);
        }

        return map;
    }

    /// <summary>
    /// Converts a text string to a list of 5-bit Baudot codes including necessary shift codes.
    /// </summary>
    public static List<byte> Encode(string text)
    {
        var result = new List<byte>();
        bool currentFigs = false;

        // Always start with LTRS
        result.Add(BaudotTables.Ltrs);

        foreach (char ch in text.ToUpperInvariant())
        {
            if (!CharMap.TryGetValue(ch, out var entry))
                continue;

            bool needsFigs = entry.IsFigs;
            if (needsFigs && !currentFigs)
            {
                result.Add(BaudotTables.Figs);
                currentFigs = true;
            }
            else if (!needsFigs && currentFigs)
            {
                result.Add(BaudotTables.Ltrs);
                currentFigs = false;
            }

            result.Add(entry.Code);
        }

        // End with LTRS
        result.Add(BaudotTables.Ltrs);
        return result;
    }
}
