using System;
using System.Globalization;
using System.Text;

namespace ClipLite
{
    internal enum CaseMode { Lower, Upper, Mixed, Sentence, Invert }

    /// <summary>Case conversions for the clip editor (ClipMate's Ctrl+Alt+L/U/M/S/I set).</summary>
    internal static class TextCase
    {
        public static string Label(CaseMode mode)
        {
            switch (mode)
            {
                case CaseMode.Lower: return "lower case";
                case CaseMode.Upper: return "UPPER CASE";
                case CaseMode.Mixed: return "Mixed Case";
                case CaseMode.Sentence: return "Sentence case.";
                default: return "iNVERT cASE";
            }
        }

        public static string Shortcut(CaseMode mode)
        {
            switch (mode)
            {
                case CaseMode.Lower: return "Ctrl+Alt+L";
                case CaseMode.Upper: return "Ctrl+Alt+U";
                case CaseMode.Mixed: return "Ctrl+Alt+M";
                case CaseMode.Sentence: return "Ctrl+Alt+S";
                default: return "Ctrl+Alt+I";
            }
        }

        public static string Apply(string text, CaseMode mode)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var ci = Loc.Culture;
            switch (mode)
            {
                case CaseMode.Lower: return text.ToLower(ci);
                case CaseMode.Upper: return text.ToUpper(ci);
                case CaseMode.Mixed: return Mixed(text, ci);
                case CaseMode.Sentence: return Sentence(text, ci);
                default: return Invert(text, ci);
            }
        }

        /// <summary>Every Word Starts With A Capital.</summary>
        static string Mixed(string text, CultureInfo ci)
        {
            var sb = new StringBuilder(text.ToLower(ci));
            bool start = true;
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                // apostrophes stay inside a word: don't capitalise O'brien -> O'Brien
                if (char.IsLetterOrDigit(c) || c == '\'' || c == '\u2019')
                {
                    if (start && char.IsLetter(c)) sb[i] = char.ToUpper(c, ci);
                    start = false;
                }
                else start = true;
            }
            return sb.ToString();
        }

        /// <summary>Lower case with a capital after every sentence end and line break.</summary>
        static string Sentence(string text, CultureInfo ci)
        {
            var sb = new StringBuilder(text.ToLower(ci));
            bool start = true;
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (c == '.' || c == '!' || c == '?' || c == '\n' || c == '\r' || c == '\u2026') { start = true; continue; }
                if (start && char.IsLetter(c)) { sb[i] = char.ToUpper(c, ci); start = false; }
                else if (!char.IsWhiteSpace(c) && !IsSentencePunctuation(c)) start = false;
            }
            return sb.ToString();
        }

        /// <summary>Characters allowed between a sentence end and its first letter, e.g. »"(«.</summary>
        static bool IsSentencePunctuation(char c)
        {
            return c == '"' || c == '\'' || c == '(' || c == '[' || c == '\u00ab' || c == '\u201c' || c == '\u2018' || c == '-' || c == '\u2014';
        }

        static string Invert(string text, CultureInfo ci)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
                sb.Append(char.IsUpper(c) ? char.ToLower(c, ci) : char.IsLower(c) ? char.ToUpper(c, ci) : c);
            return sb.ToString();
        }
    }
}
