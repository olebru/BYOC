using System;
using System.Collections.Generic;
using System.Linq;
namespace BYOCCore
{
    public class Assembler
    {
        public Dictionary<String, int> labelLUT;
        // One entry per source line that has a label or emits bytes, in address order.
        public List<ListingLine> Listing { get; private set; } = new List<ListingLine>();
        private List<String> assemblerDirectives;
        private List<byte> bytecode;
        private DecoderRom completeDecoderRom;

        public Assembler(DecoderRom completeDecoderRom)
        {
            this.completeDecoderRom = completeDecoderRom;
            bytecode = new List<byte>();
            assemblerDirectives = new List<string>();
            assemblerDirectives.Add(".BYTE");
            labelLUT = new Dictionary<String, int>();
        }

        public byte[] Assemble(string source)
        {
            bytecode = new List<byte>();
            labelLUT = new Dictionary<String, int>();
            Listing = new List<ListingLine>();
            var lines = SourceText.SplitLines(source)
                                  .Select((text, index) => new SourceLine(text, index + 1))
                                  .ToList();
            //First pass
            int address = 0;
            foreach (var line in lines)
            {
                if (line.Label != null)
                {
                    if (labelLUT.ContainsKey(line.Label))
                    {
                        throw line.Error($"label '{line.Label}' is defined more than once");
                    }
                    labelLUT.Add(line.Label, address);
                }
                if (line.Mnemonic == null) continue;
                if (line.IsDirective)
                {
                    if (!assemblerDirectives.Contains(line.Mnemonic))
                    {
                        throw line.Error($"unknown directive '{line.Mnemonic}'");
                    }
                }
                else
                {
                    address++;
                }
                address += line.Operands.Length;
            }
            if (address > DecoderRom.OpCodeAddressSpace)
            {
                throw new FormatException($"Program is {address} bytes, but only {DecoderRom.OpCodeAddressSpace} bytes of memory are addressable.");
            }
            //Second pass
            foreach (var line in lines)
            {
                int start = bytecode.Count;
                if (line.Mnemonic == null)
                {
                    if (line.Label != null) Listing.Add(line.ToListing(start, new byte[0]));
                    continue;
                }
                if (!line.IsDirective)
                {
                    try
                    {
                        bytecode.Add(completeDecoderRom.FetchByteCodeFromMnemonic(line.Mnemonic));
                    }
                    catch (ArgumentException e)
                    {
                        throw line.Error(e.Message);
                    }
                    var expectedOperands = completeDecoderRom.OperandCount(line.Mnemonic);
                    if (expectedOperands.HasValue && expectedOperands.Value != line.Operands.Length)
                    {
                        throw line.Error($"{line.Mnemonic} takes {expectedOperands} operand{(expectedOperands == 1 ? "" : "s")}, found {line.Operands.Length}");
                    }
                }
                foreach (var operandToken in line.Operands)
                {
                    if (operandToken.StartsWith("#"))
                    {
                        if (!byte.TryParse(operandToken.Substring(1), out var value))
                        {
                            throw line.Error($"'{operandToken}' is not a number between 0 and 255");
                        }
                        bytecode.Add(value);
                    }
                    else
                    {
                        if (!labelLUT.TryGetValue(operandToken, out var labelAddress))
                        {
                            throw line.Error($"unknown label '{operandToken}'");
                        }
                        bytecode.Add((byte)labelAddress);
                    }
                }
                Listing.Add(line.ToListing(start, bytecode.Skip(start).ToArray()));
            }
            return bytecode.ToArray();
        }

        // A source line is: [label:] TAB mnemonic [TAB operand[,operand...]]
        private class SourceLine
        {
            public readonly string Label;
            public readonly string Mnemonic;
            public readonly string[] Operands = new string[0];
            private readonly int lineNumber;
            private readonly string text;
            public SourceLine(string text, int lineNumber)
            {
                this.text = text;
                this.lineNumber = lineNumber;
                var tokens = text.Split('\t').Select(t => t.Trim()).ToList();
                while (tokens.Count > 0 && tokens.Last().Length == 0) tokens.RemoveAt(tokens.Count - 1);
                if (tokens.Count == 0) return;
                if (tokens[0].Length > 0)
                {
                    if (!tokens[0].EndsWith(":"))
                    {
                        throw Error($"'{tokens[0]}' in the label column must end with ':'");
                    }
                    Label = tokens[0].TrimEnd(':');
                }
                if (tokens.Count > 1 && tokens[1].Length > 0) Mnemonic = tokens[1];
                if (tokens.Count > 2)
                {
                    if (Mnemonic == null) throw Error("operands without a mnemonic");
                    Operands = tokens[2].Split(',').Select(o => o.Trim()).ToArray();
                    if (Operands.Any(o => o.Length == 0)) throw Error("empty operand");
                }
                if (tokens.Count > 3) throw Error("too many columns");
            }
            public bool IsDirective { get { return Mnemonic != null && Mnemonic.StartsWith("."); } }
            public ListingLine ToListing(int address, byte[] bytes)
            {
                return new ListingLine
                {
                    LineNumber = lineNumber,
                    Text = text,
                    Label = Label,
                    Mnemonic = Mnemonic,
                    Operands = Operands,
                    Address = address,
                    Bytes = bytes,
                    IsInstruction = Mnemonic != null && !IsDirective,
                };
            }
            public FormatException Error(string message)
            {
                return new FormatException($"Line {lineNumber}: {message}: '{text}'");
            }
        }
    }

    public class ListingLine
    {
        public int LineNumber { get; set; }
        public string Text { get; set; }
        public string Label { get; set; }
        public string Mnemonic { get; set; }
        public string[] Operands { get; set; }
        public int Address { get; set; }
        public byte[] Bytes { get; set; }
        // False for .BYTE data and label only lines.
        public bool IsInstruction { get; set; }
    }
}
