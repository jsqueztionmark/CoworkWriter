using System.Globalization;
using System.Text;

namespace CoworkWriter.Core.Scrivener;

internal static class RtfStripper
{
    private static readonly HashSet<string> ParagraphBreakWords = ["par", "line", "page", "sect"];

    public static string ToPlainText(string rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
            return string.Empty;

        var sb = new StringBuilder(rtf.Length);
        var skipDepths = new Stack<int>();
        int depth = 0;
        int i = 0;

        while (i < rtf.Length)
        {
            char c = rtf[i];
            bool inSkip = skipDepths.Count > 0 && depth >= skipDepths.Peek();

            switch (c)
            {
                case '{':
                    depth++;
                    if (!inSkip && i + 2 < rtf.Length && rtf[i + 1] == '\\' && rtf[i + 2] == '*')
                        skipDepths.Push(depth);
                    i++;
                    break;

                case '}':
                    if (skipDepths.Count > 0 && skipDepths.Peek() == depth)
                        skipDepths.Pop();
                    depth--;
                    i++;
                    break;

                case '\\':
                    i++;
                    if (i >= rtf.Length) break;
                    char next = rtf[i];

                    if (next == '\'')
                    {
                        i++;
                        if (i + 1 < rtf.Length)
                        {
                            var hex = rtf.Substring(i, 2);
                            if (!inSkip && int.TryParse(hex, NumberStyles.HexNumber, null, out int code))
                            {
                                try { sb.Append(Encoding.GetEncoding(1252).GetString([(byte)code])); }
                                catch { sb.Append((char)code); }
                            }
                            i += 2;
                        }
                    }
                    else if (next is '\\' or '{' or '}')
                    {
                        if (!inSkip) sb.Append(next);
                        i++;
                    }
                    else if (next is '\n' or '\r')
                    {
                        if (!inSkip) sb.Append('\n');
                        i++;
                        if (i < rtf.Length && rtf[i] == '\n') i++;
                    }
                    else if (next == '~')
                    {
                        if (!inSkip) sb.Append(' ');
                        i++;
                    }
                    else if (char.IsAsciiLetter(next))
                    {
                        int wordStart = i;
                        while (i < rtf.Length && char.IsAsciiLetter(rtf[i])) i++;
                        var word = rtf[wordStart..i];

                        if (i < rtf.Length && rtf[i] == '-') i++;
                        while (i < rtf.Length && char.IsAsciiDigit(rtf[i])) i++;
                        if (i < rtf.Length && rtf[i] == ' ') i++;

                        if (!inSkip && ParagraphBreakWords.Contains(word))
                            sb.Append('\n');
                    }
                    else
                    {
                        i++;
                    }
                    break;

                default:
                    if (!inSkip && c is not '\r' and not '\n')
                        sb.Append(c);
                    i++;
                    break;
            }
        }

        return sb.ToString().Trim();
    }
}
