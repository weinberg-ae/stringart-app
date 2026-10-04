using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// Converts Hebrew (right-to-left) text into "visual" order for TextMeshPro,
// with our own line wrapping, so multi-line Hebrew renders correctly.
// Rich mode: colors and bold for labels ("מקור:"), fiber families, temperature dots and warnings.
public static class PM_Hebrew
{
    // Color code shared by all screens.
    public const string HexNatural = "#3DFF7A";     // טבעי
    public const string HexArtificial = "#B784FF";  // מלאכותי
    public const string HexSynthetic = "#1AF2FF";   // סינתטי
    public const string HexWarning = "#FFA040";     // safety words
    public const string HexHigh = "#FF4D8F";        // ••• 200°C
    public const string HexMid = "#FFD91A";         // •• 150°C
    public const string HexLow = "#5C9DFF";         // • 110°C

    struct SC { public char c; public int s; }   // character + style index

    // Wraps logical text to lines of at most maxChars and returns visual text (lines joined by \n).
    public static string Visual(string logical, int maxChars)
    {
        return Visual(logical, maxChars, null);
    }

    // labelHex != null: rich mode (automatic highlighting, labels in this color).
    public static string Visual(string logical, int maxChars, string labelHex)
    {
        if (string.IsNullOrEmpty(logical)) return "";
        logical = logical.Replace("\r", "");
        if (labelHex != null) logical = Rich(logical, labelHex);

        var styles = new List<string[]>();   // style index -> open tags
        styles.Add(new string[0]);
        List<SC> all = Parse(logical, styles);

        var result = new StringBuilder();
        var para = new List<SC>();
        for (int i = 0; i <= all.Count; i++)
        {
            if (i < all.Count && all[i].c != '\n') { para.Add(all[i]); continue; }
            List<List<SC>> lines = Wrap(para, maxChars);
            if (lines.Count == 0) lines.Add(new List<SC>());
            bool rtl = false;   // a wrapped line without Hebrew (e.g. "200°C).") is still part of a Hebrew paragraph
            foreach (SC x in para) if (IsHebrew(x.c)) { rtl = true; break; }
            foreach (List<SC> line in lines)
            {
                if (result.Length > 0) result.Append('\n');
                Emit(VisualLine(line, rtl), styles, result);
            }
            para = new List<SC>();
        }
        return result.ToString();
    }

    public static string VisualLine(string line)
    {
        var l = new List<SC>();
        foreach (char c in line) l.Add(new SC { c = c, s = 0 });
        var sb = new StringBuilder();
        bool rtl = false;
        foreach (char c in line) if (IsHebrew(c)) { rtl = true; break; }
        foreach (SC x in VisualLine(l, rtl)) sb.Append(x.c);
        return sb.ToString();
    }

    // ---------- automatic highlighting ----------
    const string Heb = "א-ת";

    static string Tag(string text, string hex) { return "<b><color=" + hex + ">" + text + "</color></b>"; }

    public static string Rich(string t, string labelHex)
    {
        // "מקור:" at the start of a paragraph (optionally after a bullet)
        t = Regex.Replace(t, @"(^|\n)(•\s+)?([^:\n\d•<>]{1,18}:)", m => m.Groups[1].Value + m.Groups[2].Value + Tag(m.Groups[3].Value, labelHex));
        // fiber families
        t = Regex.Replace(t, "(?<![" + Heb + "])([והבלמש]?)(טבעי(?:ים|ת|יים)?)(?![" + Heb + "])", m => m.Groups[1].Value + Tag(m.Groups[2].Value, HexNatural));
        t = Regex.Replace(t, "(?<![" + Heb + "])([והבלמש]?)(מלאכותי(?:ים|ת|יים)?)(?![" + Heb + "])", m => m.Groups[1].Value + Tag(m.Groups[2].Value, HexArtificial));
        t = Regex.Replace(t, "(?<![" + Heb + "])([והבלמש]?)(סינתטי(?:ים|ת|יים)?)(?![" + Heb + "])", m => m.Groups[1].Value + Tag(m.Groups[2].Value, HexSynthetic));
        // color names of the legend
        t = Regex.Replace(t, "ורוד-אדום", m => Tag(m.Value, HexHigh));
        t = Regex.Replace(t, "(?<![" + Heb + "])צהוב(?![" + Heb + "])", m => Tag(m.Value, HexMid));
        t = Regex.Replace(t, "(?<![" + Heb + "])כחול(?![" + Heb + "])", m => Tag(m.Value, HexLow));
        // temperature dots (a single bullet "• " before a word is a list bullet, not a temperature)
        t = Regex.Replace(t, "(^|\n)?(•{1,3})(\\s(?:<[^>]+>)*[" + Heb + "])?", m =>
        {
            if (m.Groups[1].Success && m.Groups[2].Value.Length == 1 && m.Groups[3].Success) return m.Value;
            string hex = m.Groups[2].Value.Length == 3 ? HexHigh : m.Groups[2].Value.Length == 2 ? HexMid : HexLow;
            return m.Groups[1].Value + Tag(m.Groups[2].Value, hex) + m.Groups[3].Value;
        });
        t = Regex.Replace(t, "200°C", m => Tag(m.Value, HexHigh));
        t = Regex.Replace(t, "150°C", m => Tag(m.Value, HexMid));
        t = Regex.Replace(t, "110°C", m => Tag(m.Value, HexLow));
        // safety words
        t = Regex.Replace(t, "(?<![" + Heb + "])(ו?)(חובה|אסור|בלי קיטור|בלי ריסוס|לא מעמידים|לא מכוונים|לא מתחילים|נמס|הורס)(?![" + Heb + "])", m => m.Groups[1].Value + Tag(m.Groups[2].Value, HexWarning));
        // a second label inside a paragraph ("... חומר: תאית")
        t = Regex.Replace(t, "(?<=[.] )(חומר:)", m => Tag(m.Value, labelHex));
        return t;
    }

    // ---------- tags -> styled characters ----------
    static readonly Regex TagRx = new Regex(@"\G<(/?)(b|i|u|color|size)(=[^>]*)?>");

    static List<SC> Parse(string text, List<string[]> styles)
    {
        var res = new List<SC>();
        var stack = new List<string>();
        int cur = 0;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                Match m = TagRx.Match(text, i);
                if (m.Success)
                {
                    string name = m.Groups[2].Value;
                    if (m.Groups[1].Value == "/")
                    {
                        for (int k = stack.Count - 1; k >= 0; k--)
                            if (TagName(stack[k]) == name) { stack.RemoveAt(k); break; }
                    }
                    else stack.Add(m.Value);
                    cur = StyleIndex(stack, styles);
                    i += m.Length;
                    continue;
                }
            }
            res.Add(new SC { c = text[i], s = cur });
            i++;
        }
        return res;
    }

    static string TagName(string open)
    {
        int e = open.IndexOfAny(new[] { '=', '>' });
        return open.Substring(1, e - 1);
    }

    static int StyleIndex(List<string> stack, List<string[]> styles)
    {
        if (stack.Count == 0) return 0;
        for (int i = 1; i < styles.Count; i++)
        {
            string[] s = styles[i];
            if (s.Length != stack.Count) continue;
            bool same = true;
            for (int k = 0; k < s.Length; k++) if (s[k] != stack[k]) { same = false; break; }
            if (same) return i;
        }
        styles.Add(stack.ToArray());
        return styles.Count - 1;
    }

    static void Emit(List<SC> line, List<string[]> styles, StringBuilder sb)
    {
        int cur = 0;
        foreach (SC x in line)
        {
            if (x.s != cur)
            {
                Close(styles[cur], sb);
                foreach (string open in styles[x.s]) sb.Append(open);
                cur = x.s;
            }
            sb.Append(x.c);
        }
        Close(styles[cur], sb);
    }

    static void Close(string[] tags, StringBuilder sb)
    {
        for (int k = tags.Length - 1; k >= 0; k--) sb.Append("</").Append(TagName(tags[k])).Append('>');
    }

    // ---------- wrapping and reordering ----------
    static List<List<SC>> Wrap(List<SC> text, int maxChars)
    {
        var lines = new List<List<SC>>();
        var current = new List<SC>();
        int i = 0;
        while (i < text.Count)
        {
            if (text[i].c == ' ') { i++; continue; }
            int j = i;
            while (j < text.Count && text[j].c != ' ') j++;
            int wordLen = j - i;
            if (current.Count > 0 && current.Count + 1 + wordLen > maxChars)
            {
                lines.Add(current);
                current = new List<SC>();
            }
            if (current.Count > 0) current.Add(new SC { c = ' ', s = text[i - 1].c == ' ' ? text[i - 1].s : text[i].s });
            for (int k = i; k < j; k++) current.Add(text[k]);
            i = j;
        }
        if (current.Count > 0) lines.Add(current);
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

    static List<SC> VisualLine(List<SC> line, bool rtl)
    {
        if (!rtl) return line;

        SC[] a = line.ToArray();
        System.Array.Reverse(a);
        for (int i = 0; i < a.Length; i++) a[i].c = Mirror(a[i].c);

        int n = a.Length;
        int k = 0;
        while (k < n)
        {
            if (!IsLtr(a[k].c)) { k++; continue; }
            int start = k;
            int end = k;
            int j = k + 1;
            while (j < n)
            {
                if (IsLtr(a[j].c)) { end = j; j++; continue; }
                if (IsJoiner(a[j].c))
                {
                    int m = j;
                    while (m < n && IsJoiner(a[m].c)) m++;
                    if (m < n && IsLtr(a[m].c)) { j = m; continue; }
                }
                break;
            }
            System.Array.Reverse(a, start, end - start + 1);
            k = end + 1;
        }
        return new List<SC>(a);
    }
}
