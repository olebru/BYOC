using System;

namespace BYOCCore 
{
    public class ExampleData
    {
        // The default machine definition, BYOC-16: 16 bit buses, registers and memory.
        public static string MACHINE { get { return ReadResource("BYOCCore.Machines.byoc16.json"); } }
        // The default machine's microcode (its decoder.microcode) on its own. ROMDATA below is the same
        // microcode in the legacy tab separated format.
        public static string MICROCODE { get { return MachineDefinition.FromJson(MACHINE).Decoder.Microcode.ToJson(); } }
        // Pushes values on the stack, loads two bytes from memory, adds them and pushes the result.
        public static readonly string SRC = AssemblyLanguage.Format(string.Join("\n", new[]
        {
            "; Stack and memory",
            "; Pushes values on the stack in MMU bank 0 and adds two bytes stored after the code.",
            "LAI #15 ; A = 15",
            "PSA ; push A",
            "PSA ; and again",
            "LRA letter ; A = the byte at 'letter'",
            "LRB one ; B = the byte at 'one'",
            "PSA",
            "loop: ADD ; A = A + B",
            "PSA ; push the sum",
            "letter: .BYTE #65 ; 'A'",
            "one: .BYTE #1",
        }));

        // Prints "HELLO, WORLD!" by looping over a string in memory (B is the index), then a line feed and
        // Norwegian letters from the Latin-1 range.
        public static readonly string HELLO = AssemblyLanguage.Format(string.Join("\n", new[]
        {
            "; Hello, world on the LCD",
            "; B indexes the string, A holds each character.",
            "DCL ; clear the display",
            "LBI #0 ; B = 0",
            "loop: LNA msg ; A = msg[B]",
            "DWA ; print A",
            "INB ; next character",
            "LAI #13 ; length of the string",
            "CMP ; Z is set when B = 13",
            "JNE loop",
            "DWI #'\\n' ; new line",
            "DWI #'Æ'",
            "DWI #'Ø'",
            "DWI #'Å'",
            "DWI #' '",
            "DWI #'æ'",
            "DWI #'ø'",
            "DWI #'å'",
            "HLT",
            "msg: .BYTE \"HELLO, WORLD!\"",
        }));

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
                "; Fibonacci on the LCD",
                "; Prints each Fibonacci number in decimal until the next one does not fit in 16 bits.",
                "; Variables in the MMU bank: 0 a, 1 b, 2 next, 3 value to print, 4 digit, 5 digit printed.",
                "DCL",
                "LAI #0",
                "STA #0 ; a = 0",
                "LAI #1",
                "STA #1 ; b = 1",
                "next: LDA #1 ; print b",
                "STA #3",
                "LAI #0",
                "STA #5 ; nothing printed yet",
            };
            for (int k = 0; k < placeValues.Length; k++)
            {
                lines.AddRange(new[]
                {
                    $"; digit for {placeValues[k]}s: count how often it can be subtracted",
                    "LAI #'0'",
                    "STA #4",
                    $"count{k}: LDA #3",
                    $"LBI #{placeValues[k]}",
                    "SUB",
                    $"JC digit{k} ; borrow: value < place value",
                    "STA #3",
                    "LDA #4",
                    "INA",
                    "STA #4",
                    $"JMP count{k}",
                    $"digit{k}: LDA #4",
                    "LBI #'0'",
                    "CMP",
                    $"JNE print{k} ; not a zero: print it",
                    "LDA #5",
                    "LBI #1",
                    "CMP",
                    $"JNE skip{k} ; leading zero: skip it",
                    $"print{k}: LDA #4",
                    "DWA",
                    "LAI #1",
                    "STA #5",
                    $"skip{k}:",
                });
            }
            lines.AddRange(new[]
            {
                "; units digit, then a space",
                "LDA #3",
                "LBI #'0'",
                "ADD",
                "DWA",
                "DWI #' '",
                "; next = a + b, stop when it does not fit",
                "LDA #0",
                "LDB #1",
                "ADD",
                "JC done",
                "STA #2",
                "LDA #1",
                "STA #0 ; a = b",
                "LDA #2",
                "STA #1 ; b = next",
                "JMP next",
                "done: HLT",
            });
            return AssemblyLanguage.Format(string.Join("\n", lines));
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
    }
}
