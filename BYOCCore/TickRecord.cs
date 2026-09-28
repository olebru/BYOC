using System.Collections.Generic;
namespace BYOCCore
{
    // What happened during one clock tick, for visualisation and tracing.
    public class TickRecord
    {
        // Cycle number of this tick, starting at 1.
        public int Cycle { get; set; }
        // Instruction whose micro step ran, and the index of that step in the instruction's Steps list.
        public string Instruction { get; set; }
        public int? StepIndex { get; set; }
        // Status register value the decoder used for this tick.
        public int Status { get; set; }
        // Micro step register value, and the full decoder ROM address: (status & 0x0F) << step bits | micro step.
        public int MicroStep { get; set; }
        public int RomAddress { get; set; }
        public List<string> Signals { get; set; } = new List<string>();
        public List<BusTransfer> Transfers { get; set; } = new List<BusTransfer>();
        public List<ValueChange> Changes { get; set; } = new List<ValueChange>();
        public List<MemoryWrite> Writes { get; set; } = new List<MemoryWrite>();
        // Set when this tick loaded a new opcode into the micro step register (the end of fetch).
        public int? FetchedFromAddress { get; set; }
    }

    public class BusTransfer
    {
        public string Bus { get; set; }
        // Device that drove the bus, or null when it floated.
        public string Driver { get; set; }
        public int Value { get; set; }
        public List<string> Readers { get; set; } = new List<string>();
    }

    public class ValueChange
    {
        // Device ID, or "device.mar" / "device.cs" for memory address and chip select registers.
        public string Device { get; set; }
        public int Before { get; set; }
        public int After { get; set; }
    }

    public class MemoryWrite
    {
        public string Device { get; set; }
        // Bank number for an MMU, otherwise -1.
        public int Bank { get; set; } = -1;
        public int Address { get; set; }
        public int Value { get; set; }
    }
}
