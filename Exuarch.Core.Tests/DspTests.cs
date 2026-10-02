using System;
using System.Linq;
using Exuarch.Core;

namespace Exuarch.Core.Tests;

// DSP-16: RISC-16 with a multiplier, and the Mandelbrot set drawn with it.
public class DspTests
{
    private static readonly MachinePackage Dsp = BuiltInPackages.Get("DSP-16");

    private static Machine Start(string source) => new Machine(Dsp.Machine, source) { RecordHistory = false };
    private static int R(Machine c, int n) => c.Device<RegisterFile>("rf").Values[n];

    // MUL works in 8.8 fixed point: the 32 bit product of two signed words, shifted right by 8 and clamped.
    [Theory]
    [InlineData(512, 384, 768)]        // 2.0 x 1.5 = 3.0
    [InlineData(-512, 384, -768)]      // -2.0 x 1.5 = -3.0
    [InlineData(64, 64, 16)]           // 0.25 x 0.25 = 0.0625
    [InlineData(12, 3072, 144)]        // a whole number times 256 x a step: 12 x 12
    [InlineData(32767, 32767, 32767)]  // too big for 16 bits: clamped
    public void MulMultipliesFixedPoint(int a, int b, int product)
    {
        var c = Start($"MOVI R1, {a}\nMOVI R2, {b}\nMUL R0, R1, R2\nHLT");
        while (!c.IsHalted) c.SingleStep();
        Assert.Equal(product, (short)R(c, 0));
        Assert.Equal(a & 0xFFFF, R(c, 1));
    }

    // The same sums the program does, in C#, word for word: what every point's colour has to be.
    private static int Mul(int a, int b) => (int)Math.Clamp(((long)(short)a * (short)b) >> 8, short.MinValue, short.MaxValue) & 0xFFFF;
    private static int Colour(int column, int row, int step, int steps, Func<int, int> palette)
    {
        int cx = (Mul(column, step) - 640) & 0xFFFF, cy = (Mul(row, step) - 360) & 0xFFFF;
        int x = cx, y = cy;
        for (int left = steps; left > 0; left--)
        {
            int x2 = Mul(x, x), y2 = Mul(y, y);
            if ((((x2 + y2) - 1025) & 0x8000) == 0) return palette(left);
            int xy = Mul(x, y);
            y = (xy + xy + cy) & 0xFFFF;
            x = (x2 - y2 + cx) & 0xFFFF;
        }
        return 0;
    }

    private static void AssertDrawn(Machine c, int width, int block, int rows)
    {
        var mem = c.Device<RamModule>("mem");
        int palette = c.Assembler.labelLUT["palette"];
        var fb = c.Device<Framebuffer>("fb");
        int step = 960 * 256 / width;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int expected = Colour(column, row, step, 32, left => mem.ValueAt(palette + left));
                for (int dy = 0; dy < block; dy++)
                {
                    for (int dx = 0; dx < block; dx++)
                    {
                        Assert.True(expected == fb.Pixels[(row * block + dy) * Framebuffer.Width + column * block + dx], $"point {column}, {row}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TheCoarsePictureIsTheMandelbrotSet()
    {
        var c = Start(Dsp.Program("Mandelbrot, 80 x 60").Source);
        long ticks = 0;
        while (!c.IsHalted) { c.SingleStep(); ticks++; }
        Assert.Empty(c.MicrocodeWarnings);
        AssertDrawn(c, 80, 8, 60);
        Assert.Equal(8_141_020, ticks);
        Assert.Contains("8,141,020 ticks", Dsp.Readme);
        // On the real axis from -2 to 0.25 every point is in the set: black.
        var fb = c.Device<Framebuffer>("fb");
        Assert.Equal(0, fb.Pixels[240 * Framebuffer.Width + 100]);
        Assert.Equal(0, fb.Pixels[240 * Framebuffer.Width + 400]);
        Assert.NotEqual(0, fb.Pixels[0]);
    }

    // The finer pictures take far longer; their first rows are checked the same way.
    [Theory]
    [InlineData("Mandelbrot, 160 x 120", 160, 4, 2)]
    [InlineData("Mandelbrot, 640 x 480", 640, 1, 3)]
    public void TheFinerPicturesDrawTheSamePoints(string name, int width, int block, int rows)
    {
        var c = Start(Dsp.Program(name).Source);
        var fb = c.Device<Framebuffer>("fb");
        while (fb.Y < rows * block) c.SingleStep();
        Assert.Empty(c.MicrocodeWarnings);
        AssertDrawn(c, width, block, rows);
    }

    [Fact]
    public void TheReadmeGivesTheTicksOfEveryPicture()
    {
        foreach (var text in new[] { "8,141,020 ticks", "25,140,404 ticks", "345,850,782 ticks" }) Assert.Contains(text, Dsp.Readme);
    }

    // The three programs are the same but for their sizes.
    [Fact]
    public void TheThreeProgramsDifferOnlyInTheirSizes()
    {
        string Shape(string source) => System.Text.RegularExpressions.Regex.Replace(
            string.Join("\n", source.Split('\n').Select(l => l.Split(';')[0].TrimEnd()).Where(l => l.Length > 0 && !l.Contains("PLOT"))), @"\b\d+\b", "#");
        var shapes = Dsp.Programs.Select(p => Shape(p.Source)).Distinct().ToList();
        Assert.Single(shapes);
    }
}
