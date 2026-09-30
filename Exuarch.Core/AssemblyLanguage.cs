using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Exuarch.Core
{
    // Editor support for assembly source, driven by a machine's microcode: diagnostics, completion, hover and
    // formatting. Lines and columns are 1 based.
    public class AssemblyLanguage
    {
        private readonly MicrocodeDefinition microcode;
        private readonly DecoderRom rom;
        private readonly Assembler assembler;

        public AssemblyLanguage(MicrocodeDefinition microcode, int memorySize = MemoryModule.DefaultSize)
        {
            this.microcode = microcode ?? new MicrocodeDefinition();
            try { rom = new DecoderRom(this.microcode); }
            catch (Exception) { rom = null; }
            assembler = rom != null
                ? new Assembler(rom, memorySize)
                : new Assembler(m => Instruction(m) != null ? 0 : null, m => Instruction(m)?.OperandCount, memorySize, (m, i) => Instruction(m)?.OperandTypeAt(i));
        }

        // Mnemonics a program can use: every instruction except the fetch routine.
        public IEnumerable<InstructionDefinition> Instructions
        {
            get { return microcode.Instructions.Where(i => !string.IsNullOrWhiteSpace(i.Mnemonic)); }
        }
        private InstructionDefinition Instruction(string mnemonic)
        {
            var canonical = Assembler.Canonical(microcode, mnemonic);
            return Instructions.FirstOrDefault(i => i.Mnemonic == canonical);
        }

        public AssemblyResult Analyze(string source)
        {
            return assembler.Analyze(source);
        }

        // ---- Completion ----

        public List<CompletionItem> Complete(string source, int lineNumber, int column)
        {
            var result = Analyze(source);
            var line = result.Lines.ElementAtOrDefault(lineNumber - 1);
            if (line == null) return new List<CompletionItem>();
            if (line.Comment != null && column > line.Comment.Start) return new List<CompletionItem>();

            // Before or on the mnemonic: offer instructions and directives.
            if (line.Mnemonic == null || column <= line.Mnemonic.End)
            {
                var items = Instructions.OrderBy(i => i.Mnemonic).Select(i => new CompletionItem
                {
                    Label = i.Mnemonic,
                    Kind = CompletionKind.Instruction,
                    Detail = OperandSummary(i),
                    Documentation = i.Description ?? "",
                    InsertText = i.Mnemonic + ((i.OperandCount ?? 0) > 0 ? " " : ""),
                }).ToList();
                items.Add(new CompletionItem { Label = ".DATA", Kind = CompletionKind.Directive, Detail = "data", Documentation = "Store values, labels or \"strings\" in memory, one 16 bit cell each", InsertText = ".DATA " });
                items.Add(new CompletionItem { Label = ".STRING", Kind = CompletionKind.Directive, Detail = "text", Documentation = "Store \"text\" and values like .DATA, followed by a 0 cell that ends the string", InsertText = ".STRING \"" });
                return items;
            }

            // After the mnemonic: labels, unless the instruction takes no operands or a literal is being typed.
            var instruction = Instruction(line.Mnemonic.Text);
            if (instruction != null && instruction.OperandCount == 0) return new List<CompletionItem>();
            int operandIndex = line.Operands.Count(o => o.End < column);
            var expected = instruction?.OperandTypeAt(operandIndex);
            var current = line.TokenAt(column - 1);
            if (current != null && (current.Kind == TokenKind.Number || current.Kind == TokenKind.Character || current.Kind == TokenKind.String)) return new List<CompletionItem>();
            return result.Labels.OrderBy(l => l.Value).Select(l => new CompletionItem
            {
                Label = l.Key,
                Kind = CompletionKind.Label,
                Detail = $"label at {Hex(l.Value)}",
                Documentation = expected == OperandType.Value ? $"{instruction.Mnemonic} takes a value here; a label gives its address." : "",
                InsertText = l.Key,
            }).ToList();
        }

        // ---- Hover ----

        // Markdown describing the token under the position, or null.
        // Ends a hover with a link to the handbook page on assembly, opened in the app by the editor.
        public const string ReadMore = "\n\n[Read more: Assembly](exuarch:guide/assembly)";

        public string Hover(string source, int lineNumber, int column)
        {
            var result = Analyze(source);
            var line = result.Lines.ElementAtOrDefault(lineNumber - 1);
            var token = line?.TokenAt(column);
            if (token == null) return null;
            switch (token.Kind)
            {
                case TokenKind.Mnemonic:
                    var instruction = Instruction(token.Text);
                    if (instruction == null) return $"**{token.Text}** is not an instruction of this machine.";
                    return InstructionMarkdown(instruction);
                case TokenKind.Directive:
                    return (token.Text switch
                    {
                        ".DATA" => "**.DATA** values, labels, \"strings\"\n\nStores each value, label address or character of a string in its own 16 bit memory cell.",
                        ".STRING" => "**.STRING** \"text\", values\n\nLike .DATA, then a 0 cell, so a program can find where the string ends.",
                        ".BYTE" or ".WORD" => $"**{token.Text}** is an old name for **.DATA**: every value takes one 16 bit cell either way.",
                        _ => $"**{token.Text}** is not a directive. Use .DATA or .STRING.",
                    }) + ReadMore;
                case TokenKind.Label:
                case TokenKind.LabelReference:
                    var labelText = result.Labels.TryGetValue(token.Name, out var address)
                        ? $"label **{token.Name}** at `{Hex(address)}` ({address})"
                        : $"label **{token.Name}** is not defined";
                    return labelText + OperandRole(line, token);
                case TokenKind.Number:
                case TokenKind.Character:
                    return ValueMarkdown(token.Values[0]) + OperandRole(line, token);
                case TokenKind.String:
                    return $"string of {token.Values.Length} character{(token.Values.Length == 1 ? "" : "s")}, one cell each";
                default:
                    return null;
            }
        }

        // What an operand means for its instruction, from the instruction's operand types.
        private string OperandRole(ParsedLine line, SourceToken token)
        {
            int index = line.Operands.IndexOf(token);
            if (index < 0 || line.Mnemonic == null || line.IsDirective) return "";
            var instruction = Instruction(line.Mnemonic.Text);
            var type = instruction?.OperandTypeAt(index);
            if (type == null) return "";
            return type == OperandType.Address
                ? $"\n\n**address** for {instruction.Mnemonic}: the memory location it uses"
                : $"\n\n**value** for {instruction.Mnemonic}: used as it is";
        }

        private string InstructionMarkdown(InstructionDefinition instruction)
        {
            var text = new StringBuilder();
            var operands = SignatureOperands(instruction);
            text.Append(operands.Length > 0 ? $"**{instruction.Mnemonic}**{operands}" : $"**{instruction.Mnemonic}** · {OperandSummary(instruction)}");
            if (rom != null) text.Append($" · opcode `{Hex(rom.FetchByteCodeFromMnemonic(instruction.Mnemonic))}`");
            text.Append("\n\n");
            if (!string.IsNullOrWhiteSpace(instruction.Description)) text.Append(instruction.Description).Append("\n\n");
            text.Append("```\n");
            for (int i = 0; i < instruction.Steps.Count; i++)
            {
                var step = instruction.Steps[i];
                var when = step.When == null ? "" : $"  (when {step.When})";
                text.Append($"{i + 1}: {string.Join(", ", step.Signals)}{when}\n");
            }
            text.Append("```");
            return text.ToString();
        }

        private static string ValueMarkdown(int value)
        {
            var character = value >= 0x20 && value <= 0xFF && CharacterDisplay.ToChar((byte)value) != ' ' || value == 0x20
                ? $" · '{(char)value}'" : "";
            return $"`{value}` · `{Hex(value)}`{character}";
        }

        // " address" or " value, address" after the mnemonic, when the operand types are known.
        private static string SignatureOperands(InstructionDefinition instruction)
        {
            var signature = instruction.Signature;
            return signature.Length > instruction.Mnemonic.Length ? " " + signature.Substring(instruction.Mnemonic.Length + 1) : "";
        }

        private static string OperandSummary(InstructionDefinition instruction)
        {
            if (instruction.OperandTypes != null && instruction.OperandTypes.Count > 0)
            {
                return string.Join(", ", instruction.OperandTypes.Select(t => t == OperandType.Address ? "address" : "value"));
            }
            return instruction.OperandCount switch
            {
                null => "operands not declared",
                0 => "no operands",
                1 => "1 operand",
                var n => $"{n} operands",
            };
        }

        private static string Hex(int value) { return "0x" + value.ToString("X4"); }

        // ---- Formatting ----

        // Lines up labels, mnemonics, operands and comments in columns. Lines with syntax errors are left as they are.
        public static string Format(string source)
        {
            var lines = AssemblyParser.Parse(source);
            var layout = Layout.For(lines);
            return string.Join("\n", lines.Select(l => FormatLine(l, layout)));
        }

        // One line formatted with the column widths of the whole document, for format on type.
        public static string FormatLine(string source, int lineNumber)
        {
            var lines = AssemblyParser.Parse(source);
            var line = lines.ElementAtOrDefault(lineNumber - 1);
            return line == null ? "" : FormatLine(line, Layout.For(lines));
        }

        // As Format, and also writes each mnemonic the way the instruction set spells it (lai becomes LAI).
        public string FormatDocument(string source)
        {
            var lines = WithCanonicalMnemonics(source);
            var layout = Layout.For(lines);
            return string.Join("\n", lines.Select(l => FormatLine(l, layout)));
        }
        public string FormatDocumentLine(string source, int lineNumber)
        {
            var lines = WithCanonicalMnemonics(source);
            var line = lines.ElementAtOrDefault(lineNumber - 1);
            return line == null ? "" : FormatLine(line, Layout.For(lines));
        }
        private List<ParsedLine> WithCanonicalMnemonics(string source)
        {
            var lines = AssemblyParser.Parse(source);
            foreach (var line in lines.Where(l => l.Mnemonic?.Kind == TokenKind.Mnemonic))
            {
                line.Mnemonic.Text = Assembler.Canonical(microcode, line.Mnemonic.Text);
            }
            foreach (var line in lines.Where(l => l.Mnemonic?.Kind == TokenKind.Directive))
            {
                line.Mnemonic.Text = line.Mnemonic.Text.ToUpperInvariant();
            }
            return lines;
        }

        private class Layout
        {
            public int MnemonicColumn;
            public int OperandColumn;
            public int CommentColumn;

            public static Layout For(List<ParsedLine> lines)
            {
                var clean = lines.Where(l => l.SyntaxErrors.Count == 0).ToList();
                int labelWidth = clean.Where(l => l.Label != null).Select(l => l.Label.Text.Length + 1).DefaultIfEmpty(0).Max();
                int mnemonicColumn = Math.Max(8, labelWidth);
                int mnemonicWidth = Math.Max(4, clean.Where(l => l.Mnemonic != null).Select(l => l.Mnemonic.Text.Length).DefaultIfEmpty(0).Max()) + 2;
                var layout = new Layout { MnemonicColumn = mnemonicColumn, OperandColumn = mnemonicColumn + mnemonicWidth };
                int codeWidth = clean.Where(l => l.Comment != null && (l.Mnemonic != null || l.Label != null))
                                     .Select(l => Code(l, layout).Length).DefaultIfEmpty(0).Max();
                layout.CommentColumn = Math.Min(48, Math.Max(codeWidth + 2, layout.OperandColumn + 12));
                return layout;
            }
        }

        private static string Code(ParsedLine line, Layout layout)
        {
            var text = new StringBuilder();
            if (line.Label != null) text.Append(line.Label.Text);
            if (line.Mnemonic != null)
            {
                text.Append(' ', Math.Max(1, layout.MnemonicColumn - text.Length));
                text.Append(line.Mnemonic.Text);
                if (line.Operands.Count > 0)
                {
                    text.Append(' ', Math.Max(1, layout.OperandColumn - text.Length));
                    text.Append(string.Join(", ", line.Operands.Select(OperandText)));
                }
            }
            return text.ToString();
        }

        // Literals are written without the older # prefix.
        private static string OperandText(SourceToken operand)
        {
            return operand.Kind == TokenKind.Number || operand.Kind == TokenKind.Character ? operand.Text.TrimStart('#') : operand.Text;
        }

        private static string FormatLine(ParsedLine line, Layout layout)
        {
            if (line.SyntaxErrors.Count > 0) return line.Text;
            var code = Code(line, layout);
            if (line.Comment == null) return code;
            var comment = "; " + line.Comment.Text.Substring(1).Trim();
            if (code.Length == 0)
            {
                // A comment on its own line keeps to the left edge, or lines up with the code if it was indented.
                bool indented = line.Text.Length > 0 && char.IsWhiteSpace(line.Text[0]);
                return (indented ? new string(' ', layout.MnemonicColumn) : "") + comment;
            }
            return code + new string(' ', Math.Max(1, layout.CommentColumn - code.Length)) + comment;
        }
    }

    public enum CompletionKind { Instruction, Directive, Label }

    public class CompletionItem
    {
        public string Label { get; set; }
        public CompletionKind Kind { get; set; }
        public string Detail { get; set; }
        public string Documentation { get; set; }
        public string InsertText { get; set; }
    }
}
