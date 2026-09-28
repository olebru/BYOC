using System;

namespace BYOCCore 
{
    public class ExampleData
    {
        // The default machine definition, BYOC-16: 16 bit buses, registers and memory.
        public static string MACHINE { get { return ReadResource("BYOCCore.Machines.byoc16.json"); } }
        // The BYOC-16 microcode as JSON. ROMDATA below is the same microcode in the legacy tab separated format.
        public static string MICROCODE { get { return ReadResource("BYOCCore.Machines.byoc16.microcode.json"); } }
        // Prints "HELLO, WORLD!" by looping over a string in memory (B is the index), then a line feed and
        // Norwegian letters from the Latin-1 range.
        public const string HELLO =
            "\tDCL\n" +
            "\tLBI\t#0\n" +
            "loop:\tLNA\tmsg\n" +
            "\tDWA\n" +
            "\tINB\n" +
            "\tLAI\t#13\n" +
            "\tCMP\n" +
            "\tJNE\tloop\n" +
            "\tDWI\t#10\n" +
            "\tDWI\t#198\n" +
            "\tDWI\t#216\n" +
            "\tDWI\t#197\n" +
            "\tDWI\t#32\n" +
            "\tDWI\t#230\n" +
            "\tDWI\t#248\n" +
            "\tDWI\t#229\n" +
            "\tHLT\n" +
            "msg:\t.BYTE\t#72,#69,#76,#76,#79,#44,#32,#87,#79,#82,#76,#68,#33";

        // Prints the Fibonacci numbers that fit in 16 bits in decimal on the display: 1 1 2 3 5 8 ... 46368.
        public static readonly string FIBONACCI = FibonacciProgram(new[] { 10000, 1000, 100, 10 });

        // Builds a Fibonacci program that prints in decimal until ADD overflows. Variables live in the selected
        // MMU bank: a at 0, b at 1, next at 2, the value being printed at 3, the digit being counted at 4 and
        // "a digit was printed" at 5. There is no divide, so each digit is found by subtracting its place value
        // until SUB borrows, which sets carry for JC. Leading zeros are not printed.
        private static string FibonacciProgram(int[] placeValues)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "\tDCL",
                "\tLAI\t#0",
                "\tSTA\t#0",
                "\tLAI\t#1",
                "\tSTA\t#1",
                "next:\tLDA\t#1",
                "\tSTA\t#3",
                "\tLAI\t#0",
                "\tSTA\t#5",
            };
            for (int k = 0; k < placeValues.Length; k++)
            {
                lines.AddRange(new[]
                {
                    "\tLAI\t#48",
                    "\tSTA\t#4",
                    $"count{k}:\tLDA\t#3",
                    $"\tLBI\t#{placeValues[k]}",
                    "\tSUB",
                    $"\tJC\tdigit{k}",
                    "\tSTA\t#3",
                    "\tLDA\t#4",
                    "\tINA",
                    "\tSTA\t#4",
                    $"\tJMP\tcount{k}",
                    $"digit{k}:\tLDA\t#4",
                    "\tLBI\t#48",
                    "\tCMP",
                    $"\tJNE\tprint{k}",
                    "\tLDA\t#5",
                    "\tLBI\t#1",
                    "\tCMP",
                    $"\tJNE\tskip{k}",
                    $"print{k}:\tLDA\t#4",
                    "\tDWA",
                    "\tLAI\t#1",
                    "\tSTA\t#5",
                    $"skip{k}:",
                });
            }
            lines.AddRange(new[]
            {
                "\tLDA\t#3",
                "\tLBI\t#48",
                "\tADD",
                "\tDWA",
                "\tDWI\t#32",
                "\tLDA\t#0",
                "\tLDB\t#1",
                "\tADD",
                "\tJC\tdone",
                "\tSTA\t#2",
                "\tLDA\t#1",
                "\tSTA\t#0",
                "\tLDA\t#2",
                "\tSTA\t#1",
                "\tJMP\tnext",
                "done:\tHLT",
            });
            return string.Join("\n", lines);
        }

        // Example programs for the default machine, by name. The first is loaded by default.
        public static readonly (string Name, string Source)[] Programs =
        {
            ("Stack and memory", SRC),
            ("Hello, world on the LCD", HELLO),
            ("Fibonacci on the LCD", FIBONACCI),
        };

        private static string ReadResource(string name)
        {
            using var stream = typeof(ExampleData).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
            using var reader = new System.IO.StreamReader(stream);
            return reader.ReadToEnd();
        }
        public const string ROMDATA = @"p	pc	output	FTC	x	x	x	x
s	mem	loadmar	FTC	x	x	x	x
p	mem	output	FTC	x	x	x	x
s	regi	load	FTC	x	x	x	x
p	rega	output	TAB	x	x	x	x
s	regb	load	TAB	x	x	x	x
s	regi	reset	TAB	x	x	x	x
s	pc	inc	TAB	x	x	x	x
p	regb	output	TBA	x	x	x	x
s	rega	load	TBA	x	x	x	x
s	regi	reset	TBA	x	x	x	x
s	pc	inc	TBA	x	x	x	x
p	alu	add	ADD	x	x	x	x
s	rega	load	ADD	x	x	x	x
s	regi	reset	ADD	x	x	x	x
s	pc	inc	ADD	x	x	x	x
p	alu	sub	SUB	x	x	x	x
s	rega	load	SUB	x	x	x	x
s	regi	reset	SUB	x	x	x	x
s	pc	inc	SUB	x	x	x	x
p	pc	inc	STA	x	x	x	x
p	pc	output	STA	x	x	x	x
s	mem	loadmar	STA	x	x	x	x
p	mem	output	STA	x	x	x	x
s	mmu	loadmar	STA	x	x	x	x
p	rega	output	STA	x	x	x	x
s	mmu	load	STA	x	x	x	x
s	regi	reset	STA	x	x	x	x
s	pc	inc	STA	x	x	x	x
p	pc	inc	STB	x	x	x	x
p	pc	output	STB	x	x	x	x
s	mem	loadmar	STB	x	x	x	x
p	mem	output	STB	x	x	x	x
s	mmu	loadmar	STB	x	x	x	x
p	regb	output	STB	x	x	x	x
s	mmu	load	STB	x	x	x	x
s	regi	reset	STB	x	x	x	x
s	pc	inc	STB	x	x	x	x
p	pc	inc	LRA	x	x	x	x
p	pc	output	LRA	x	x	x	x
s	mem	loadmar	LRA	x	x	x	x
p	mem	output	LRA	x	x	x	x
s	mem	loadmar	LRA	x	x	x	x
p	rega	load	LRA	x	x	x	x
s	mem	output	LRA	x	x	x	x
s	regi	reset	LRA	x	x	x	x
s	pc	inc	LRA	x	x	x	x
p	pc	inc	LRB	x	x	x	x
p	pc	output	LRB	x	x	x	x
s	mem	loadmar	LRB	x	x	x	x
p	mem	output	LRB	x	x	x	x
s	mem	loadmar	LRB	x	x	x	x
p	regb	load	LRB	x	x	x	x
s	mem	output	LRB	x	x	x	x
s	regi	reset	LRB	x	x	x	x
s	pc	inc	LRB	x	x	x	x
p	pc	inc	LDA	x	x	x	x
p	pc	output	LDA	x	x	x	x
s	mem	loadmar	LDA	x	x	x	x
p	mem	output	LDA	x	x	x	x
s	mmu	loadmar	LDA	x	x	x	x
p	rega	load	LDA	x	x	x	x
s	mmu	output	LDA	x	x	x	x
s	regi	reset	LDA	x	x	x	x
s	pc	inc	LDA	x	x	x	x
p	pc	inc	LDB	x	x	x	x
p	pc	output	LDB	x	x	x	x
s	mem	loadmar	LDB	x	x	x	x
p	mem	output	LDB	x	x	x	x
s	mmu	loadmar	LDB	x	x	x	x
p	regb	load	LDB	x	x	x	x
s	mmu	output	LDB	x	x	x	x
s	regi	reset	LDB	x	x	x	x
s	pc	inc	LDB	x	x	x	x
p	regsp	dec	PSA	x	x	x	x
p	regs	load	PSA	x	x	x	x
s	mmu	outputcs	PSA	x	x	x	x
p	mmu	select0stack	PSA	x	x	x	x
s	mmu	loadmar	PSA	x	x	x	x
s	regsp	output	PSA	x	x	x	x
p	rega	output	PSA	x	x	x	x
s	mmu	load	PSA	x	x	x	x
p	regs	output	PSA	x	x	x	x
s	mmu	loadcs	PSA	x	x	x	x
p	regi	reset	PSA	x	x	x	x
s	pc	inc	PSA	x	x	x	x
p	regs	load	POA	x	x	x	x
s	mmu	outputcs	POA	x	x	x	x
p	mmu	select0stack	POA	x	x	x	x
p	mmu	loadmar	POA	x	x	x	x
s	regsp	output	POA	x	x	x	x
p	rega	load	POA	x	x	x	x
s	mmu	output	POA	x	x	x	x
s	regsp	inc	POA	x	x	x	x
p	regs	output	POA	x	x	x	x
s	mmu	loadcs	POA	x	x	x	x
s	regi	reset	POA	x	x	x	x
s	pc	inc	POA	x	x	x	x
p	regsp	dec	PSB	x	x	x	x
p	regs	load	PSB	x	x	x	x
s	mmu	outputcs	PSB	x	x	x	x
p	mmu	select0stack	PSB	x	x	x	x
s	mmu	loadmar	PSB	x	x	x	x
s	regsp	output	PSB	x	x	x	x
p	regb	output	PSB	x	x	x	x
s	mmu	load	PSB	x	x	x	x
p	regs	output	PSB	x	x	x	x
s	mmu	loadcs	PSB	x	x	x	x
p	regi	reset	PSB	x	x	x	x
s	pc	inc	PSB	x	x	x	x
p	regs	load	POB	x	x	x	x
s	mmu	outputcs	POB	x	x	x	x
p	mmu	select0stack	POB	x	x	x	x
p	mmu	loadmar	POB	x	x	x	x
s	regsp	output	POB	x	x	x	x
p	regb	load	POB	x	x	x	x
s	mmu	output	POB	x	x	x	x
s	regsp	inc	POB	x	x	x	x
p	regs	output	POB	x	x	x	x
s	mmu	loadcs	POB	x	x	x	x
s	regi	reset	POB	x	x	x	x
s	pc	inc	POB	x	x	x	x
p	regi	reset	NOP	x	x	x	x
s	pc	inc	NOP	x	x	x	x
p	pc	inc	LNA	x	x	x	x
p	pc	output	LNA	x	x	x	x
s	mem	loadmar	LNA	x	x	x	x
p	mem	output	LNA	x	x	x	x
s	rega	load	LNA	x	x	x	x
p	alu	add	LNA	x	x	x	x
s	mem	loadmar	LNA	x	x	x	x
p	mem	output	LNA	x	x	x	x
s	rega	load	LNA	x	x	x	x
s	regi	reset	LNA	x	x	x	x
s	pc	inc	LNA	x	x	x	x
p	alu	cmp	CMP	x	x	x	x
s	regi	reset	CMP	x	x	x	x
s	pc	inc	CMP	x	x	x	x
p	pc	inc	JMP	x	x	x	x
p	pc	output	JMP	x	x	x	x
s	mem	loadmar	JMP	x	x	x	x
p	pc	load	JMP	x	x	x	x
s	mem	output	JMP	x	x	x	x
s	regi	reset	JMP	x	x	x	x
p	pc	inc	JEQ	x	x	x	x
p	pc	output	JEQ	x	x	x	1
s	mem	loadmar	JEQ	x	x	x	1
p	pc	load	JEQ	x	x	x	1
s	mem	output	JEQ	x	x	x	1
s	regi	reset	JEQ	x	x	x	1
p	regi	reset	JEQ	x	x	x	0
s	pc	inc	JEQ	x	x	x	0
p	pc	inc	JNE	x	x	x	x
p	pc	output	JNE	x	x	x	0
s	mem	loadmar	JNE	x	x	x	0
p	pc	load	JNE	x	x	x	0
s	mem	output	JNE	x	x	x	0
s	regi	reset	JNE	x	x	x	0
p	regi	reset	JNE	x	x	x	1
s	pc	inc	JNE	x	x	x	1
p	alu	cmp	CMP	x	x	x	x
s	regi	reset	CMP	x	x	x	x
s	pc	inc	CMP	x	x	x	x
p	regsta	reset	RST	x	x	x	x
s	regi	reset	RST	x	x	x	x
s	pc	inc	RST	x	x	x	x
p	pc	inc	JC	x	x	x	x
p	pc	output	JC	x	x	1	x
s	mem	loadmar	JC	x	x	1	x
p	pc	load	JC	x	x	1	x
s	mem	output	JC	x	x	1	x
s	regi	reset	JC	x	x	1	x
p	regi	reset	JC	x	x	0	x
s	pc	inc	JC	x	x	0	x
p	pc	inc	LPA	x	x	x	x
p	pc	output	LPA	x	x	x	x
s	mem	loadmar	LPA	x	x	x	x
p	mem	output	LPA	x	x	x	x
s	regs	load	LPA	x	x	x	x
p	regs	output	LPA	x	x	x	x
s	mem	loadmar	LPA	x	x	x	x
p	rega	load	LPA	x	x	x	x
s	mem	output	LPA	x	x	x	x
s	regi	reset	LPA	x	x	x	x
s	pc	inc	LPA	x	x	x	x
p	pc	inc	LPB	x	x	x	x
p	pc	output	LPB	x	x	x	x
s	mem	loadmar	LPB	x	x	x	x
p	mem	output	LPB	x	x	x	x
s	regs	load	LPB	x	x	x	x
p	regs	output	LPB	x	x	x	x
s	mem	loadmar	LPB	x	x	x	x
p	regb	load	LPB	x	x	x	x
s	mem	output	LPB	x	x	x	x
s	regi	reset	LPB	x	x	x	x
s	pc	inc	LPB	x	x	x	x
p	clk	disable	HLT	x	x	x	x
p	pc	inc	LAI	x	x	x	x
p	pc	output	LAI	x	x	x	x
s	mem	loadmar	LAI	x	x	x	x
p	mem	output	LAI	x	x	x	x
s	rega	load	LAI	x	x	x	x
s	regi	reset	LAI	x	x	x	x
s	pc	inc	LAI	x	x	x	x
p	pc	inc	LBI	x	x	x	x
p	pc	output	LBI	x	x	x	x
s	mem	loadmar	LBI	x	x	x	x
p	mem	output	LBI	x	x	x	x
s	regb	load	LBI	x	x	x	x
s	regi	reset	LBI	x	x	x	x
s	pc	inc	LBI	x	x	x	x
p	regsp	output	PEA	x	x	x	x
s	mmu	loadmar	PEA	x	x	x	x
p	mmu	output	PEA	x	x	x	x
s	rega	load	PEA	x	x	x	x
s	regi	reset	PEA	x	x	x	x
s	pc	inc	PEA	x	x	x	x
p	regsp	output	PEB	x	x	x	x
s	mmu	loadmar	PEB	x	x	x	x
p	mmu	output	PEB	x	x	x	x
s	regb	load	PEB	x	x	x	x
s	regi	reset	PEB	x	x	x	x
s	pc	inc	PEB	x	x	x	x
p	pc	inc	SWB	x	x	x	x
p	pc	output	SWB	x	x	x	x
s	mem	loadmar	SWB	x	x	x	x
p	mem	output	SWB	x	x	x	x
s	mmu	loadcs	SWB	x	x	x	x
s	regi	reset	SWB	x	x	x	x
s	pc	inc	SWB	x	x	x	x";
        public const string SRC = @"	LAI	#15
	PSA
	PSA
	LRA	letter
	LRB	one
	PSA
loop:	ADD
	PSA
letter:	.BYTE	#65
one:	.BYTE	#1";
    }
}
