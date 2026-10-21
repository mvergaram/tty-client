using System;
using System.Collections.Generic;

namespace TtyClient.TTY;

/// <summary>
/// Encodes ASCII text to a sequence of Baudot ITA-2 5-bit codes, managing LTRS/FIGS shift.
/// </summary>
public static class BaudotEncoder
{
    private const byte CodeLtrs = 31;
    private const byte CodeFigs = 27;
    private const byte CodeSpace = 4;
    private const byte CodeCr = 8;
    private const byte CodeLf = 2;

    // Map from character to (code, requiresFigs)
    private static readonly Dictionary<char, (byte Code, bool IsFigs)> CharMap = BuildCharMap();

    private static Dictionary<char, (byte, bool)> BuildCharMap()
    {
        var map = new Dictionary<char, (byte, bool)>();
        // LTRS characters
        string ltrs = "\0E\nA SIUD\0RJNFCKTZLWH\0YPQOBG\0MXV";
        string figs = "\03\n- '87\r\04\0,\0:(\05\"(2#\060\019?&\0./;";

        for (int i = 0; i < 32; i++)
        {
            char lc = ltrs[i];
            if (lc != '\0' && !map.ContainsKey(lc))
                map[lc] = ((byte)i, false);
            if (lc != '\0' && char.IsLetter(lc))
            {
                char lower = char.ToLowerInvariant(lc);
                if (!map.ContainsKey(lower))
                    map[lower] = ((byte)i, false);
            }

            char fc = figs[i];
            if (fc != '\0' && !map.ContainsKey(fc))
                map[fc] = ((byte)i, true);
        }

        // Explicitly add common ones
        map[' '] = (CodeSpace, false);
        map['\r'] = (CodeCr, false);
        map['\n'] = (CodeLf, false);
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
        result.Add(CodeLtrs);

        foreach (char ch in text.ToUpperInvariant())
        {
            if (!CharMap.TryGetValue(ch, out var entry))
                continue;

            bool needsFigs = entry.IsFigs;
            if (needsFigs && !currentFigs)
            {
                result.Add(CodeFigs);
                currentFigs = true;
            }
            else if (!needsFigs && currentFigs)
            {
                result.Add(CodeLtrs);
                currentFigs = false;
            }

            result.Add(entry.Code);
        }

        // End with LTRS
        result.Add(CodeLtrs);
        return result;
    }
}
