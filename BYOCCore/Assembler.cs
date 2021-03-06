using System;
using System.Collections.Generic;
using System.Linq;
namespace BYOCCore
{
    public class Assembler
    {
        public Dictionary<String, int> labelLUT;
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
            //First pass
    
            int address = 0;
           foreach(var line in source.Split(Environment.NewLine)) 
           {
                var tokens = line.Split('\t');
                if (tokens[0].Length > 0 && tokens[0].Last() == ':')
                {
                    labelLUT.Add(tokens[0].Replace(":", string.Empty), address);
                }
                if (tokens[1].First() != '.') address++;
                int numberOfOperands = 0;
                if (tokens.Length > 2) numberOfOperands = 1;
                if (tokens.Length > 2 && tokens[2].Contains(','))
                {
                    var operandTokens = tokens[2].Split(',');
                    numberOfOperands = operandTokens.Count();
                }
                address += numberOfOperands;
                
          
           }
            //Second pass
             foreach(var line in source.Split(Environment.NewLine)) 
             {
                    var tokens = line.Split('\t');
                    string mnemonic = tokens[1];
                    if (mnemonic.First() != '.')
                    {
                        var opcode = completeDecoderRom.FetchByteCodeFromMnemonic(mnemonic);
                        bytecode.Add(opcode);
                    }
                    if (tokens.Length == 3) //Operand
                    {
                        var operandTokens = tokens[2].Split(',');
                        foreach (var operandToken in operandTokens)
                        {
                            switch (operandToken.First())
                            {
                                case '#':
                                    bytecode.Add(byte.Parse(operandToken.Replace("#", string.Empty)));
                                    break;
                                default: //Label
                                    bytecode.Add(
                                        (byte)labelLUT.Single(l => l.Key == operandToken).Value
                                        );
                                    break;
                            }
                        }
                    }
                
             }
            return bytecode.ToArray();
        }
    }
}