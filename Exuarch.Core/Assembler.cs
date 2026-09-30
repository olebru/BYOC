using System;
using System.Collections.Generic;
using System.Linq;
namespace Exuarch.Core
{
    // Two pass assembler. Each opcode and operand takes one 16 bit memory cell; a string in .DATA or .STRING takes
    // one cell per character, and .STRING ends with a 0 cell. See AssemblyParser for the syntax.
    public class Assembler
    {
        public Dictionary<String, int> labelLUT;
        // One entry per source line that has a label or emits cells, in address order.
        public List<ListingLine> Listing { get; private set; } = new List<ListingLine>();
        public static readonly string[] Directives = { ".DATA", ".STRING" };
        // Older names for .DATA: both always stored one value per cell. They still assemble, with a warning.
        public static readonly string[] OldDirectives = { ".BYTE", ".WORD" };
        public const string StringDirective = ".STRING";
        private const int cellMask = Bus.Mask;
        private readonly int memorySize;
        private readonly Func<string, int?> opcodeOf;
        private readonly Func<string, int?> operandCountOf;
        private readonly Func<string, int, OperandType?> operandTypeOf;

        // Mnemonics are matched without regard to case.
        public Assembler(DecoderRom completeDecoderRom, int memorySize = MemoryModule.DefaultSize)
            : this(mnemonic => TryOpcode(completeDecoderRom, mnemonic),
                   mnemonic => completeDecoderRom.OperandCount(Canonical(completeDecoderRom.Microcode, mnemonic)), memorySize,
                   (mnemonic, index) => completeDecoderRom.Microcode.FindInstruction(Canonical(completeDecoderRom.Microcode, mnemonic))?.OperandTypeAt(index))
        {
        }

        // The instruction set's own spelling of a mnemonic, matched without regard to case.
        public static string Canonical(MicrocodeDefinition microcode, string mnemonic)
        {
            return microcode.Instructions.FirstOrDefault(i => i.Mnemonic == mnemonic)?.Mnemonic
                ?? microcode.Instructions.FirstOrDefault(i => string.Equals(i.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase))?.Mnemonic
                ?? mnemonic;
        }

        // opcodeOf returns null for an unknown mnemonic; operandCountOf and operandTypeOf return null when an
        // instruction does not say.
        public Assembler(Func<string, int?> opcodeOf, Func<string, int?> operandCountOf, int memorySize = MemoryModule.DefaultSize,
                         Func<string, int, OperandType?> operandTypeOf = null)
        {
            this.opcodeOf = opcodeOf;
            this.operandCountOf = operandCountOf;
            this.operandTypeOf = operandTypeOf ?? ((mnemonic, index) => null);
            this.memorySize = memorySize;
            labelLUT = new Dictionary<String, int>();
        }

        private static int? TryOpcode(DecoderRom rom, string mnemonic)
        {
            try { return rom.FetchByteCodeFromMnemonic(Canonical(rom.Microcode, mnemonic)); }
            catch (ArgumentException) { return null; }
        }

        // Assembles, throwing a FormatException for the first error. Warnings do not stop it.
        public int[] Assemble(string source)
        {
            var result = Analyze(source);
            var error = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
            if (error != null) throw new FormatException(error.ToString());
            return result.Cells;
        }

        // Assembles as far as possible and reports every problem with its position; never throws.
        public AssemblyResult Analyze(string source)
        {
            var result = new AssemblyResult { Lines = AssemblyParser.Parse(source) };
            var diagnostics = result.Diagnostics;
            void Warning(ParsedLine line, SourceToken token, string message)
            {
                diagnostics.Add(new AssemblyDiagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Line = line.Number,
                    StartColumn = token.Start,
                    EndColumn = token.End,
                    Message = message,
                    Text = line.Text,
                });
            }
            void Error(ParsedLine line, SourceToken token, string message)
            {
                diagnostics.Add(new AssemblyDiagnostic
                {
                    Line = line.Number,
                    StartColumn = token?.Start ?? 1,
                    EndColumn = token?.End ?? Math.Max(2, line.Text.Length + 1),
                    Message = message,
                    Text = line.Text,
                });
            }

            //First pass: label addresses.
            labelLUT = new Dictionary<String, int>();
            int address = 0;
            foreach (var line in result.Lines)
            {
                diagnostics.AddRange(line.SyntaxErrors);
                if (line.Label != null)
                {
                    if (labelLUT.ContainsKey(line.Label.Name)) Error(line, line.Label, $"label '{line.Label.Name}' is defined more than once");
                    else labelLUT[line.Label.Name] = address;
                }
                address += CellCount(line);
            }

            //Second pass: cells.
            var cells = new List<int>();
            Listing = new List<ListingLine>();
            foreach (var line in result.Lines)
            {
                int start = cells.Count;
                if (line.Mnemonic == null)
                {
                    if (line.Label != null) Listing.Add(ToListing(line, start, new int[0]));
                    continue;
                }
                if (line.IsDirective)
                {
                    var directive = line.Mnemonic.Text.ToUpperInvariant();
                    if (OldDirectives.Contains(directive)) Warning(line, line.Mnemonic, $"{directive} is an old name for .DATA: every value takes one 16 bit cell either way. Use .DATA, or .STRING for text that ends with 0.");
                    else if (!Directives.Contains(directive)) Error(line, line.Mnemonic, $"unknown directive '{line.Mnemonic.Text}', use .DATA or .STRING");
                }
                else
                {
                    var opcode = opcodeOf(line.Mnemonic.Text);
                    if (opcode == null) Error(line, line.Mnemonic, $"unknown mnemonic '{line.Mnemonic.Text}'");
                    cells.Add(opcode ?? 0);
                    var expected = operandCountOf(line.Mnemonic.Text);
                    if (opcode != null && expected.HasValue && expected.Value != line.Operands.Count)
                    {
                        Error(line, line.Mnemonic, $"{line.Mnemonic.Text} takes {expected} operand{(expected == 1 ? "" : "s")}, found {line.Operands.Count}");
                    }
                }
                for (int index = 0; index < line.Operands.Count; index++)
                {
                    var operand = line.Operands[index];
                    switch (operand.Kind)
                    {
                        case TokenKind.Number:
                        case TokenKind.Character:
                            if (operand.Values[0] > cellMask) Error(line, operand, $"'{operand.Text}' is not a number between 0 and {cellMask}");
                            cells.Add(operand.Values[0] & cellMask);
                            break;
                        case TokenKind.String:
                            if (!line.IsDirective) Error(line, operand, "a string is only allowed in .DATA and .STRING");
                            cells.AddRange(operand.Values);
                            break;
                        case TokenKind.LabelReference:
                            if (labelLUT.TryGetValue(operand.Name, out var labelAddress))
                            {
                                cells.Add(labelAddress);
                                if (!line.IsDirective && operandTypeOf(line.Mnemonic.Text, index) == OperandType.Value)
                                {
                                    Warning(line, operand, $"{line.Mnemonic.Text} takes a value here, and '{operand.Name}' is the address of a label (0x{labelAddress:X4}). Use a number, or an instruction that takes an address.");
                                }
                            }
                            else
                            {
                                Error(line, operand, $"unknown label '{operand.Name}'");
                                cells.Add(0);
                            }
                            break;
                    }
                }
                if (IsString(line)) cells.Add(0);
                if (cells.Count > memorySize && start <= memorySize)
                {
                    Error(line, line.Mnemonic, $"the program is {address} cells, but program memory only holds {memorySize}");
                }
                Listing.Add(ToListing(line, start, cells.Skip(start).ToArray()));
            }
            result.Cells = cells.ToArray();
            result.Listing = Listing;
            result.Labels = new Dictionary<string, int>(labelLUT);
            diagnostics.Sort((a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : a.StartColumn.CompareTo(b.StartColumn));
            return result;
        }

        // Cells a line takes: opcode plus one per operand, one per character for strings, and the 0 after .STRING.
        private static int CellCount(ParsedLine line)
        {
            if (line.Mnemonic == null) return 0;
            int operands = line.Operands.Sum(o => o.Kind == TokenKind.String ? o.Values.Length : 1);
            return (line.IsDirective ? 0 : 1) + operands + (IsString(line) ? 1 : 0);
        }

        private static bool IsString(ParsedLine line)
        {
            return line.IsDirective && line.Mnemonic.Text.Equals(StringDirective, StringComparison.OrdinalIgnoreCase);
        }

        private static ListingLine ToListing(ParsedLine line, int address, int[] cells)
        {
            return new ListingLine
            {
                LineNumber = line.Number,
                Text = line.Text,
                Label = line.Label?.Name,
                Mnemonic = line.Mnemonic?.Text,
                Operands = line.Operands.Select(o => o.Text).ToArray(),
                Address = address,
                Cells = cells,
                IsInstruction = line.Mnemonic != null && !line.IsDirective,
            };
        }
    }

    public class AssemblyResult
    {
        public List<ParsedLine> Lines { get; set; } = new List<ParsedLine>();
        public int[] Cells { get; set; } = Array.Empty<int>();
        public List<ListingLine> Listing { get; set; } = new List<ListingLine>();
        public Dictionary<string, int> Labels { get; set; } = new Dictionary<string, int>();
        public List<AssemblyDiagnostic> Diagnostics { get; } = new List<AssemblyDiagnostic>();
        // True when there are no errors; warnings are allowed.
        public bool Success { get { return !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error); } }
        public IEnumerable<AssemblyDiagnostic> Errors { get { return Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error); } }
        public IEnumerable<AssemblyDiagnostic> Warnings { get { return Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning); } }
    }

    public class ListingLine
    {
        public int LineNumber { get; set; }
        public string Text { get; set; }
        public string Label { get; set; }
        public string Mnemonic { get; set; }
        public string[] Operands { get; set; }
        public int Address { get; set; }
        public int[] Cells { get; set; }
        // False for .DATA / .STRING data and label only lines.
        public bool IsInstruction { get; set; }
    }
}
