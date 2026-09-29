using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace BYOCCore
{
    // Assembly source, one statement per line:
    //
    //     [label:] [MNEMONIC [operand, operand ...]] [; comment]
    //
    // Columns are separated by any whitespace. Operands are 123, 0x1F or 'A' literals (an older leading # is
    // allowed), "strings" (in .BYTE and .WORD), or label names. The older tab separated form parses the same way.
    // Labels start with a letter, _ or ., so a literal is never mistaken for a label.
    public enum TokenKind { Label, Mnemonic, Directive, Number, Character, String, LabelReference, Comma, Comment, Error }

    public class SourceToken
    {
        public TokenKind Kind { get; set; }
        public string Text { get; set; }
        // 1 based columns; End is one past the last character.
        public int Start { get; set; }
        public int End { get; set; }
        // Literal values: one for a number or character, one per character for a string.
        public int[] Values { get; set; } = Array.Empty<int>();
        // For Label and LabelReference, the name without the colon.
        public string Name { get; set; }
        public bool Contains(int column) { return column >= Start && column <= End; }
    }

    public class AssemblyDiagnostic
    {
        public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Error;
        public int Line { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string Message { get; set; }
        public string Text { get; set; }
        public override string ToString() { return $"Line {Line}: {Message}: '{Text}'"; }
    }

    public class ParsedLine
    {
        public int Number { get; set; }
        public string Text { get; set; }
        public SourceToken Label { get; set; }
        public SourceToken Mnemonic { get; set; }
        public List<SourceToken> Operands { get; } = new List<SourceToken>();
        public SourceToken Comment { get; set; }
        public List<SourceToken> Tokens { get; } = new List<SourceToken>();
        // Problems found while reading the line itself, before assembling.
        public List<AssemblyDiagnostic> SyntaxErrors { get; } = new List<AssemblyDiagnostic>();
        public bool IsDirective { get { return Mnemonic?.Kind == TokenKind.Directive; } }
        // The token under a 1 based column, or null.
        public SourceToken TokenAt(int column)
        {
            return Tokens.FirstOrDefault(t => t.Contains(column) && t.Kind != TokenKind.Comma);
        }
    }

    public static class AssemblyParser
    {
        public static List<ParsedLine> Parse(string source)
        {
            return SourceText.SplitLines(source ?? "").Select((text, index) => ParseLine(text, index + 1)).ToList();
        }

        public static ParsedLine ParseLine(string text, int lineNumber)
        {
            var line = new ParsedLine { Number = lineNumber, Text = text };
            void Error(SourceToken token, string message)
            {
                line.SyntaxErrors.Add(new AssemblyDiagnostic { Line = lineNumber, StartColumn = token.Start, EndColumn = Math.Max(token.End, token.Start + 1), Message = message, Text = text });
            }

            foreach (var token in Tokenize(text))
            {
                line.Tokens.Add(token);
                if (token.Kind == TokenKind.Error) Error(token, token.Name);
            }

            // Structure: label, mnemonic, then operands separated by commas.
            bool expectOperand = true;
            SourceToken lastComma = null;
            foreach (var token in line.Tokens.Where(t => t.Kind != TokenKind.Error))
            {
                switch (token.Kind)
                {
                    case TokenKind.Comment:
                        line.Comment = token;
                        break;
                    case TokenKind.Label:
                        if (line.Label != null || line.Mnemonic != null) Error(token, "a label must come first on the line");
                        else line.Label = token;
                        break;
                    case TokenKind.Comma:
                        if (line.Mnemonic == null || line.Operands.Count == 0 || expectOperand) Error(token, "unexpected ','");
                        expectOperand = true;
                        lastComma = token;
                        break;
                    default:
                        if (line.Mnemonic == null)
                        {
                            if (token.Kind == TokenKind.LabelReference)
                            {
                                token.Kind = token.Text.StartsWith(".") ? TokenKind.Directive : TokenKind.Mnemonic;
                                line.Mnemonic = token;
                            }
                            else
                            {
                                Error(token, $"'{token.Text}' needs a mnemonic before it");
                            }
                            break;
                        }
                        if (!expectOperand) Error(token, $"missing ',' before '{token.Text}'");
                        line.Operands.Add(token);
                        expectOperand = false;
                        lastComma = null;
                        break;
                }
            }
            if (lastComma != null && expectOperand) Error(lastComma, "missing operand after ','");
            return line;
        }

        private static IEnumerable<SourceToken> Tokenize(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == ' ' || c == '\t') { i++; continue; }
                int start = i;
                if (c == ';')
                {
                    yield return Token(TokenKind.Comment, text, start, text.Length);
                    yield break;
                }
                if (c == ',')
                {
                    i++;
                    yield return Token(TokenKind.Comma, text, start, i);
                    continue;
                }
                if (c == '"')
                {
                    var (values, end, error) = ReadQuoted(text, i + 1, '"');
                    i = end;
                    yield return error == null
                        ? Token(TokenKind.String, text, start, i, values)
                        : ErrorToken(text, start, i, error);
                    continue;
                }
                // Literals: 15, 0x2A or 'A'. A leading # is allowed and means the same.
                if (c == '#' || c == '\'' || char.IsDigit(c))
                {
                    if (c == '#') i++;
                    int literalStart = i;
                    if (i < text.Length && text[i] == '\'')
                    {
                        var (values, end, error) = ReadQuoted(text, i + 1, '\'');
                        i = end;
                        if (error == null && values.Length != 1) error = "a character literal holds exactly one character";
                        yield return error == null ? Token(TokenKind.Character, text, start, i, values) : ErrorToken(text, start, i, error);
                        continue;
                    }
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    var literal = text.Substring(literalStart, i - literalStart);
                    yield return TryParseNumber(literal, out var number)
                        ? Token(TokenKind.Number, text, start, i, new[] { number })
                        : ErrorToken(text, start, i, $"'{text.Substring(start, i - start)}' is not a number, write 123, 0x7B or 'A'");
                    continue;
                }
                if (IsIdentifierStart(c))
                {
                    i++;
                    while (i < text.Length && IsIdentifierPart(text[i])) i++;
                    if (i < text.Length && text[i] == ':')
                    {
                        i++;
                        var label = Token(TokenKind.Label, text, start, i);
                        label.Name = text.Substring(start, i - start - 1);
                        yield return label;
                        continue;
                    }
                    var word = Token(TokenKind.LabelReference, text, start, i);
                    word.Name = word.Text;
                    yield return word;
                    continue;
                }
                while (i < text.Length && text[i] != ' ' && text[i] != '\t' && text[i] != ',' && text[i] != ';') i++;
                yield return ErrorToken(text, start, i, $"unexpected '{text.Substring(start, i - start)}'");
            }
        }

        // Reads up to the closing quote. Escapes: \n \t \0 \\ \" \'.
        private static (int[] Values, int End, string Error) ReadQuoted(string text, int i, char quote)
        {
            var values = new List<int>();
            while (i < text.Length)
            {
                char c = text[i];
                if (c == quote) return (values.ToArray(), i + 1, null);
                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    int? escaped = next switch { 'n' => 10, 't' => 9, '0' => 0, '\\' => '\\', '"' => '"', '\'' => '\'', _ => null };
                    if (escaped == null) return (null, i + 2, $"unknown escape '\\{next}'");
                    values.Add(escaped.Value);
                    i += 2;
                    continue;
                }
                if (c > 0xFF) return (null, i + 1, $"'{c}' is not a Latin-1 character");
                values.Add(c);
                i++;
            }
            return (null, text.Length, $"missing closing {quote}");
        }

        public static bool TryParseNumber(string text, out int value)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value) && value >= 0;
            }
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        public static bool IsIdentifierStart(char c) { return char.IsLetter(c) || c == '_' || c == '.'; }
        public static bool IsIdentifierPart(char c) { return char.IsLetterOrDigit(c) || c == '_'; }

        private static SourceToken Token(TokenKind kind, string text, int start, int end, int[] values = null)
        {
            return new SourceToken { Kind = kind, Text = text.Substring(start, end - start), Start = start + 1, End = end + 1, Values = values ?? Array.Empty<int>() };
        }
        private static SourceToken ErrorToken(string text, int start, int end, string message)
        {
            var token = Token(TokenKind.Error, text, start, Math.Min(Math.Max(end, start + 1), text.Length));
            token.Name = message;
            return token;
        }
    }
}
