using System.Globalization;

namespace DrawnUi.Draw
{
    public partial class SkiaLabel
    {
        /// <summary>
        /// Unicode bidirectional algorithm (UAX #9) for the lines of a label: implicit levels (rules W1-W7, N1-N2, I1-I2)
        /// and the visual reordering of rule L2, the same rules DrawnUi.Rust (unicode-bidi) and DrawnUi.React (ICU) follow.
        /// Explicit embeddings and isolates (LRE, RLI...) are read as neutrals. The paragraph direction comes from its first
        /// strong letter (P2, P3).
        /// </summary>
        public static class Bidi
        {
            /// <summary>Bidi character types used by the implicit algorithm.</summary>
            public enum CharType : byte { L, R, AL, EN, AN, ES, ET, CS, NSM, BN, B, S, WS, ON }

            /// <summary>True when the text holds a right-to-left letter: only then a line needs the bidi pass.</summary>
            public static bool HasRightToLeft(ReadOnlySpan<char> text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    int cp = text[i];
                    if (cp < 0x0590)
                        continue;
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        cp = char.ConvertToUtf32(text[i], text[i + 1]);
                        i++;
                    }
                    var type = GetCharType(cp);
                    if (type == CharType.R || type == CharType.AL)
                        return true;
                }
                return false;
            }

            /// <summary>Paragraph direction from the first strong letter: true = right to left.</summary>
            public static bool IsRightToLeftParagraph(ReadOnlySpan<char> text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    int cp = text[i];
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        cp = char.ConvertToUtf32(text[i], text[i + 1]);
                        i++;
                    }
                    var type = GetCharType(cp);
                    if (type == CharType.L)
                        return false;
                    if (type == CharType.R || type == CharType.AL)
                        return true;
                }
                return false;
            }

            /// <summary>Bidi type of a code point, from the Unicode blocks and the general category.</summary>
            public static CharType GetCharType(int cp)
            {
                switch (cp)
                {
                    case 0x200E: return CharType.L;  // LRM
                    case 0x200F: return CharType.R;  // RLM
                    case 0x061C: return CharType.AL; // ALM
                    case 0x0009: case 0x000B: case 0x001F: return CharType.S;
                    case 0x000A: case 0x000D: case 0x001C: case 0x001D: case 0x001E: case 0x0085: case 0x2029:
                        return CharType.B;
                    case 0x0020: case 0x000C: case 0x2028: case 0x205F: case 0x3000: return CharType.WS;
                    case 0x002B: case 0x002D: case 0x207A: case 0x207B: case 0x208A: case 0x208B: case 0xFF0B: case 0xFF0D:
                        return CharType.ES;
                    case 0x002C: case 0x002E: case 0x002F: case 0x003A: case 0x00A0: case 0x060C: case 0x2044:
                    case 0xFE50: case 0xFE52: case 0xFE55: case 0xFF0C: case 0xFF0E: case 0xFF0F: case 0xFF1A:
                        return CharType.CS;
                    case 0x0023: case 0x0024: case 0x0025: case 0x00B0: case 0x00B1: case 0x066A: case 0x2030:
                    case 0x2031: case 0x2032: case 0x2033: case 0x2034: case 0x212E: case 0x2213:
                        return CharType.ET;
                    case 0x00AD: case 0xFEFF: return CharType.BN;
                }

                if (cp >= 0x2000 && cp <= 0x200A) return CharType.WS;
                if ((cp >= 0x200B && cp <= 0x200D) || (cp >= 0x2060 && cp <= 0x2064)) return CharType.BN;
                if (cp < 0x20 || (cp >= 0x7F && cp <= 0x9F)) return CharType.BN;
                if (cp >= 0x0030 && cp <= 0x0039) return CharType.EN;
                if (cp == 0x00B2 || cp == 0x00B3 || cp == 0x00B9) return CharType.EN;
                if ((cp >= 0x2070 && cp <= 0x2079) || (cp >= 0x2080 && cp <= 0x2089)) return CharType.EN;
                if (cp >= 0xFF10 && cp <= 0xFF19) return CharType.EN;
                if (cp >= 0x06F0 && cp <= 0x06F9) return CharType.EN; // extended Arabic-Indic digits
                if ((cp >= 0x0660 && cp <= 0x0669) || cp == 0x066B || cp == 0x066C) return CharType.AN;
                if (cp >= 0x0600 && cp <= 0x0605) return CharType.AN;
                if (cp >= 0x00A2 && cp <= 0x00A5) return CharType.ET;
                if (cp >= 0x20A0 && cp <= 0x20CF) return CharType.ET;

                UnicodeCategory category;
                if (cp > 0xFFFF)
                    category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
                else
                    category = CharUnicodeInfo.GetUnicodeCategory((char)cp);

                if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark)
                    return CharType.NSM;

                // right-to-left blocks
                if ((cp >= 0x0590 && cp <= 0x05FF) || (cp >= 0x07C0 && cp <= 0x089F) || (cp >= 0xFB1D && cp <= 0xFB4F)
                    || (cp >= 0x10800 && cp <= 0x10FFF) || (cp >= 0x1E800 && cp <= 0x1EDFF))
                    return IsLetterOrMark(category) ? CharType.R : CharType.ON;
                if ((cp >= 0x0600 && cp <= 0x07BF) || (cp >= 0x08A0 && cp <= 0x08FF) || (cp >= 0xFB50 && cp <= 0xFDFF)
                    || (cp >= 0xFE70 && cp <= 0xFEFF) || (cp >= 0x1EE00 && cp <= 0x1EEFF))
                    return IsLetterOrMark(category) ? CharType.AL : CharType.ON;

                switch (category)
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.SpacingCombiningMark:
                    case UnicodeCategory.DecimalDigitNumber: // digits of left-to-right scripts (Devanagari...)
                    case UnicodeCategory.LetterNumber:
                        return CharType.L;
                    case UnicodeCategory.SpaceSeparator:
                        return CharType.WS;
                    case UnicodeCategory.Format:
                        return CharType.BN;
                    default:
                        return CharType.ON;
                }
            }

            static bool IsLetterOrMark(UnicodeCategory c) =>
                c is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
                    or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.SpacingCombiningMark
                    or UnicodeCategory.OtherNumber;

            /// <summary>
            /// Embedding level of every UTF-16 unit of <paramref name="text"/>, one line of a paragraph whose direction is
            /// <paramref name="rightToLeft"/>. Even = left to right, odd = right to left. Trailing whitespace gets the
            /// paragraph level (rule L1).
            /// </summary>
            public static byte[] GetLevels(ReadOnlySpan<char> text, bool rightToLeft)
            {
                int n = text.Length;
                var types = new CharType[n];
                for (int i = 0; i < n; i++)
                {
                    if (char.IsHighSurrogate(text[i]) && i + 1 < n && char.IsLowSurrogate(text[i + 1]))
                    {
                        var t = GetCharType(char.ConvertToUtf32(text[i], text[i + 1]));
                        types[i] = t;
                        types[i + 1] = t;
                        i++;
                    }
                    else
                    {
                        types[i] = GetCharType(text[i]);
                    }
                }

                var original = (CharType[])types.Clone();
                byte baseLevel = (byte)(rightToLeft ? 1 : 0);
                var sos = rightToLeft ? CharType.R : CharType.L;

                // W1: NSM (and BN, read as removed) take the type of the character before them
                var prev = sos;
                for (int i = 0; i < n; i++)
                {
                    if (types[i] == CharType.NSM || types[i] == CharType.BN)
                        types[i] = prev;
                    else
                        prev = types[i];
                }

                // W2: EN after AL becomes AN; W3: AL becomes R
                var lastStrong = sos;
                for (int i = 0; i < n; i++)
                {
                    var t = types[i];
                    if (t == CharType.L || t == CharType.R || t == CharType.AL)
                        lastStrong = t;
                    else if (t == CharType.EN && lastStrong == CharType.AL)
                        types[i] = CharType.AN;
                }
                for (int i = 0; i < n; i++)
                    if (types[i] == CharType.AL)
                        types[i] = CharType.R;

                // W4: one separator between two numbers of the same type joins them
                for (int i = 1; i < n - 1; i++)
                {
                    if (types[i] == CharType.ES && types[i - 1] == CharType.EN && types[i + 1] == CharType.EN)
                        types[i] = CharType.EN;
                    else if (types[i] == CharType.CS && types[i - 1] == CharType.EN && types[i + 1] == CharType.EN)
                        types[i] = CharType.EN;
                    else if (types[i] == CharType.CS && types[i - 1] == CharType.AN && types[i + 1] == CharType.AN)
                        types[i] = CharType.AN;
                }

                // W5: terminators next to European numbers become numbers
                for (int i = 0; i < n; i++)
                {
                    if (types[i] != CharType.ET)
                        continue;
                    int start = i, end = i;
                    while (end < n && types[end] == CharType.ET)
                        end++;
                    bool touches = (start > 0 && types[start - 1] == CharType.EN) || (end < n && types[end] == CharType.EN);
                    if (touches)
                        for (int k = start; k < end; k++)
                            types[k] = CharType.EN;
                    i = end - 1;
                }

                // W6: remaining separators and terminators are neutral
                for (int i = 0; i < n; i++)
                    if (types[i] is CharType.ES or CharType.ET or CharType.CS)
                        types[i] = CharType.ON;

                // W7: European numbers after a left-to-right letter are left to right
                lastStrong = sos;
                for (int i = 0; i < n; i++)
                {
                    var t = types[i];
                    if (t == CharType.L || t == CharType.R)
                        lastStrong = t;
                    else if (t == CharType.EN && lastStrong == CharType.L)
                        types[i] = CharType.L;
                }

                // N1, N2: neutrals take the direction around them when both sides agree, else the paragraph's
                for (int i = 0; i < n; i++)
                {
                    if (!IsNeutral(types[i]))
                        continue;
                    int start = i, end = i;
                    while (end < n && IsNeutral(types[end]))
                        end++;
                    var before = start == 0 ? sos : Strong(types[start - 1]);
                    var after = end == n ? sos : Strong(types[end]);
                    var resolved = before == after ? before : (rightToLeft ? CharType.R : CharType.L);
                    for (int k = start; k < end; k++)
                        types[k] = resolved;
                    i = end - 1;
                }

                // I1, I2: implicit levels
                var levels = new byte[n];
                for (int i = 0; i < n; i++)
                {
                    var t = types[i];
                    if (baseLevel == 0)
                        levels[i] = (byte)(t == CharType.R ? 1 : t == CharType.AN || t == CharType.EN ? 2 : 0);
                    else
                        levels[i] = (byte)(t == CharType.L || t == CharType.EN || t == CharType.AN ? 2 : 1);
                }

                // L1: separators and the whitespace at the end of the line take the paragraph level
                for (int i = n - 1; i >= 0; i--)
                {
                    var o = original[i];
                    if (o == CharType.WS || o == CharType.BN || o == CharType.S || o == CharType.B)
                        levels[i] = baseLevel;
                    else
                        break;
                }
                for (int i = 0; i < n; i++)
                {
                    if (original[i] == CharType.S || original[i] == CharType.B)
                    {
                        levels[i] = baseLevel;
                        for (int k = i - 1; k >= 0 && (original[k] == CharType.WS || original[k] == CharType.BN); k--)
                            levels[k] = baseLevel;
                    }
                }

                return levels;
            }

            static bool IsNeutral(CharType t) => t is CharType.B or CharType.S or CharType.WS or CharType.ON;

            // numbers count as right to left when they sit between neutrals (N1)
            static CharType Strong(CharType t) => t == CharType.L ? CharType.L : CharType.R;

            /// <summary>
            /// Visual order of runs given their levels (rule L2): from the highest level down to the lowest odd one, every
            /// stretch of runs at that level or above is reversed. Returns run indices left to right on screen.
            /// </summary>
            public static int[] VisualOrder(IReadOnlyList<byte> runLevels)
            {
                int count = runLevels.Count;
                var order = new int[count];
                for (int i = 0; i < count; i++)
                    order[i] = i;
                if (count < 2)
                    return order;

                byte highest = 0, lowestOdd = byte.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var l = runLevels[i];
                    if (l > highest) highest = l;
                    if ((l & 1) == 1 && l < lowestOdd) lowestOdd = l;
                }
                if (lowestOdd == byte.MaxValue)
                    return order; // all left to right

                for (int level = highest; level >= lowestOdd; level--)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (runLevels[order[i]] < level)
                            continue;
                        int start = i;
                        while (i < count && runLevels[order[i]] >= level)
                            i++;
                        Array.Reverse(order, start, i - start);
                    }
                }

                return order;
            }
        }
    }
}
