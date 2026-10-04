using System.Collections.Generic;
using System.Text;

// Converts Hebrew (right-to-left) text into "visual" order for TextMeshPro,
// with our own line wrapping, so multi-line Hebrew renders correctly.
public static class PM_Hebrew
{
    // Wraps logical text to lines of at most maxChars and returns visual text (lines joined by \n).
    public static string Visual(string logical, int maxChars)
    {
        if (string.IsNullOrEmpty(logical)) return "";
        var result = new StringBuilder();
        string[] paragraphs = logical.Replace("\r", "").Split('\n');
        for (int p = 0; p < paragraphs.Length; p++)
        {
            List<string> lines = Wrap(paragraphs[p], maxChars);
            if (lines.Count == 0) lines.Add("");
            foreach (string line in lines)
            {
                if (result.Length > 0) result.Append('\n');
                result.Append(VisualLine(line));
            }
        }
        return result.ToString();
    }

    static List<string> Wrap(string text, int maxChars)
    {
        var lines = new List<string>();
        string[] words = text.Split(' ');
        var current = new StringBuilder();
        foreach (string w in words)
        {
            if (w.Length == 0) continue;
            if (current.Length > 0 && current.Length + 1 + w.Length > maxChars)
            {
                lines.Add(current.ToString());
                current.Length = 0;
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(w);
        }
        if (current.Length > 0) lines.Add(current.ToString());
        return lines;
    }

    static bool IsHebrew(char c) { return c >= '֐' && c <= '׿'; }

    // Characters that belong to left-to-right runs (Latin, digits, number signs).
    static bool IsLtr(char c)
    {
        if (IsHebrew(c)) return false;
        if (char.IsLetterOrDigit(c)) return true;
        return c == '°' || c == '%';
    }

    // Characters that may sit inside a left-to-right run (e.g. "3.5", "Meta Quest", "110°C").
    static bool IsJoiner(char c)
    {
        return c == '.' || c == ',' || c == ':' || c == '/' || c == '-' || c == '–' || c == '+' || c == ' ' || c == '×' || c == '\'';
    }

    static char Mirror(char c)
    {
        switch (c)
        {
            case '(': return ')';
            case ')': return '(';
            case '[': return ']';
            case ']': return '[';
            case '<': return '>';
            case '>': return '<';
            case '{': return '}';
            case '}': return '{';
            default: return c;
        }
    }

    public static string VisualLine(string line)
    {
        bool hasHebrew = false;
        foreach (char c in line) if (IsHebrew(c)) { hasHebrew = true; break; }
        if (!hasHebrew) return line;

        char[] a = line.ToCharArray();
        System.Array.Reverse(a);
        for (int i = 0; i < a.Length; i++) a[i] = Mirror(a[i]);

        int n = a.Length;
        int k = 0;
        while (k < n)
        {
            if (!IsLtr(a[k])) { k++; continue; }
            int start = k;
            int end = k;
            int j = k + 1;
            while (j < n)
            {
                if (IsLtr(a[j])) { end = j; j++; continue; }
                if (IsJoiner(a[j]))
                {
                    int m = j;
                    while (m < n && IsJoiner(a[m])) m++;
                    if (m < n && IsLtr(a[m])) { j = m; continue; }
                }
                break;
            }
            System.Array.Reverse(a, start, end - start + 1);
            k = end + 1;
        }
        return new string(a);
    }
}
