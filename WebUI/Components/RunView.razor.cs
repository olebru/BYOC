using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BYOCCore;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace WebUI.Components
{
    public partial class RunView : IDisposable
    {
        private const double CardWidth = 160;
        private const double CardHeight = 90;
        private const int TraceRows = 120;
        private static readonly string[] BusColors = { "#2f80ed", "#00a3bf", "#7b61ff", "#e8a33d", "#3fb68b", "#d6336c" };
        private static readonly Dictionary<string, string> CategoryColors = new Dictionary<string, string>
        {
            ["Registers"] = "#2f80ed",
            ["Control"] = "#9b51e0",
            ["Arithmetic"] = "#f2994a",
            ["Memory"] = "#27ae60",
        };

        [Inject] private IJSRuntime JS { get; set; }

        [Parameter] public Machine Machine { get; set; }
        [Parameter] public DeviceRegistry Registry { get; set; } = DeviceRegistry.CreateDefault();
        [Parameter] public EventCallback OnRestart { get; set; }

        private Machine shown;
        private bool running;
        private string runtimeError;
        private int speedSlider = 40;
        private readonly HashSet<int> breakpoints = new HashSet<int>();
        private string bottomTab = "Memory";
        private string memoryDevice;
        private int memoryBank;
        private int? lastScrolledAddress;
        private double zoom = 1;
        private bool fitPending = true;
        private ElementReference schematicElement;
        private bool stopRequested;
        private bool wholeRom;

        protected override void OnParametersSet()
        {
            if (!ReferenceEquals(shown, Machine))
            {
                shown = Machine;
                stopRequested = true;
                running = false;
                runtimeError = null;
                lastScrolledAddress = null;
                fitPending = true;
                if (Machine != null && (memoryDevice == null || Machine.Device(memoryDevice) == null))
                {
                    memoryDevice = Machine.Definition.ProgramMemory ?? Machine.Devices.FirstOrDefault(d => d is RomModule || d is MMU)?.ID();
                }
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (fitPending && Machine != null)
            {
                fitPending = false;
                await Fit();
            }
            var address = Machine?.CurrentInstructionAddress;
            if (address != null && address != lastScrolledAddress)
            {
                lastScrolledAddress = address;
                await JS.InvokeVoidAsync("byocEditor.scrollToId", $"listing-{address}");
            }
        }

        public void Dispose()
        {
            stopRequested = true;
        }

        // Scales the schematic so the whole machine fits the panel width.
        private async Task Fit()
        {
            var rect = await JS.InvokeAsync<ElementRect>("byocEditor.rect", schematicElement);
            if (rect.Width <= 0) return;
            zoom = Math.Clamp(Math.Floor((rect.Width - 24) / CanvasWidth * 100) / 100, 0.4, 1.2);
            StateHasChanged();
        }
        private void Zoom(double factor)
        {
            zoom = Math.Clamp(Math.Round(zoom * factor, 2), 0.4, 1.6);
        }
        private class ElementRect
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        // ---- Controls ----

        private int Hz { get { return (int)Math.Round(Math.Pow(10, speedSlider / 100.0 * 3)); } }
        private string State
        {
            get
            {
                if (runtimeError != null) return "error";
                if (Machine.IsHalted) return "halted";
                return running ? "running" : "paused";
            }
        }

        private bool Tick()
        {
            try
            {
                Machine.SingleStep();
                if (Machine.LastTick?.FetchedFromAddress is int address && breakpoints.Contains(address)) return false;
                return !Machine.IsHalted;
            }
            catch (Exception e)
            {
                runtimeError = $"Cycle {Machine.Cycles + 1}: {e.Message}";
                return false;
            }
        }
        private void TickOnce()
        {
            if (running || Machine.IsHalted || runtimeError != null) return;
            Tick();
        }
        private void StepInstruction()
        {
            if (running || Machine.IsHalted || runtimeError != null) return;
            for (int i = 0; i < 10000; i++)
            {
                if (!Tick() || Machine.LastTick.FetchedFromAddress != null) break;
            }
        }
        private void RunToHalt()
        {
            if (running || Machine.IsHalted || runtimeError != null) return;
            for (int i = 0; i < 100000 && Tick(); i++) { }
        }
        private async Task ToggleRun()
        {
            if (running) { stopRequested = true; return; }
            if (Machine.IsHalted || runtimeError != null) return;
            running = true;
            stopRequested = false;
            var machine = Machine;
            var frame = Stopwatch.StartNew();
            while (!stopRequested && ReferenceEquals(machine, Machine))
            {
                frame.Restart();
                int ticks = Hz <= 60 ? 1 : Hz / 60;
                bool keepGoing = true;
                for (int i = 0; i < ticks && keepGoing; i++) keepGoing = Tick();
                StateHasChanged();
                if (!keepGoing) break;
                int delay = Hz <= 60 ? 1000 / Hz : 16;
                await Task.Delay(Math.Max(1, delay - (int)frame.ElapsedMilliseconds));
            }
            if (ReferenceEquals(machine, Machine)) running = false;
            StateHasChanged();
        }
        private async Task Restart()
        {
            stopRequested = true;
            running = false;
            await OnRestart.InvokeAsync();
        }
        private async Task OnKeyDown(KeyboardEventArgs e)
        {
            switch (e.Key)
            {
                case " ": await ToggleRun(); break;
                case "ArrowRight" when e.ShiftKey: StepInstruction(); break;
                case "ArrowRight": TickOnce(); break;
                case "r": case "R": await Restart(); break;
            }
        }
        private void ToggleBreakpoint(int address)
        {
            if (!breakpoints.Remove(address)) breakpoints.Add(address);
        }

        // ---- Tick interpretation ----

        private TickRecord Last { get { return Machine.LastTick; } }
        private BusTransfer TransferOn(string busId)
        {
            return Last?.Transfers.FirstOrDefault(t => t.Bus == busId && t.Driver != null);
        }
        private bool Drives(string deviceId, string busId)
        {
            return TransferOn(busId)?.Driver == deviceId;
        }
        private bool Reads(string deviceId, string busId)
        {
            return TransferOn(busId)?.Readers.Contains(deviceId) == true;
        }
        private bool IsActive(string deviceId)
        {
            return Last?.Signals.Any(s => s.StartsWith(deviceId + ".")) == true;
        }
        private bool Changed(string deviceId)
        {
            return Last?.Changes.Any(c => c.Device == deviceId || c.Device.StartsWith(deviceId + ".")) == true
                || Last?.Writes.Any(w => w.Device == deviceId) == true;
        }
        private IEnumerable<string> SignalsOf(string deviceId)
        {
            return Last?.Signals.Where(s => s.StartsWith(deviceId + ".")).Select(s => s.Substring(deviceId.Length + 1)) ?? Enumerable.Empty<string>();
        }
        private static string Hex(int value) => value.ToString("X2");

        private ListingLine ListingAt(int? address)
        {
            return address == null ? null : Machine.Assembler.Listing.FirstOrDefault(l => l.IsInstruction && l.Address == address);
        }
        private static string SourceText(ListingLine line)
        {
            if (line == null) return "";
            var operands = line.Operands.Length == 0 ? "" : " " + string.Join(", ", line.Operands);
            return $"{line.Mnemonic}{operands}";
        }
        private InstructionDefinition InstructionNamed(string mnemonic)
        {
            return mnemonic == null ? null : Machine.DecoderRom.Microcode.FindInstruction(mnemonic);
        }

        // The instruction whose steps the timeline shows: the one that ran last, or the next one before the first tick.
        private (InstructionDefinition Instruction, int Status, int? RanIndex) TimelineInstruction
        {
            get
            {
                if (Last?.Instruction != null) return (InstructionNamed(Last.Instruction), Last.Status, Last.StepIndex);
                var next = Machine.NextStep;
                return (next?.Instruction, Machine.Status, null);
            }
        }

        // ---- Schematic geometry ----

        private DeviceTypeInfo Info(DeviceDefinition device)
        {
            return Registry.Info(device.Type) ?? new DeviceTypeInfo { Type = device.Type, Ports = device.Ports().Select(p => p.Key).ToList() };
        }
        private Position LayoutOf(DeviceDefinition device, int index)
        {
            return device.Layout ?? new Position { X = 30 + (index % 6) * 190, Y = 40 + (index / 6) * 130 };
        }
        private double BusY(BusDefinition bus, int index)
        {
            return bus.Layout?.Y ?? 190 + index * 320;
        }
        private double BusY(string busId)
        {
            var index = Machine.Definition.Buses.FindIndex(b => b.Id == busId);
            return index < 0 ? 0 : BusY(Machine.Definition.Buses[index], index);
        }
        private string BusColor(string busId)
        {
            var index = Machine.Definition.Buses.FindIndex(b => b.Id == busId);
            return index < 0 ? "#999" : BusColors[index % BusColors.Length];
        }
        private static string CategoryColor(DeviceTypeInfo info)
        {
            return CategoryColors.TryGetValue(info.Category, out var color) ? color : "#828282";
        }
        private (double X, double Y) PortAnchor(DeviceDefinition device, Position position, string port)
        {
            var ports = Info(device).Ports;
            var index = Math.Max(0, ports.IndexOf(port));
            var x = position.X + CardWidth * (index + 1) / (ports.Count + 1);
            var busId = device.GetPortBus(port);
            bool bottom = busId == null || BusY(busId) >= position.Y + CardHeight / 2;
            return (x, bottom ? position.Y + CardHeight : position.Y);
        }
        private double CanvasWidth
        {
            get
            {
                var right = Machine.Definition.Devices.Select((d, i) => LayoutOf(d, i).X).DefaultIfEmpty(0).Max() + CardWidth + 60;
                return Math.Max(1100, right);
            }
        }
        private double CanvasHeight
        {
            get
            {
                var bottom = Machine.Definition.Devices.Select((d, i) => LayoutOf(d, i).Y).DefaultIfEmpty(0).Max() + CardHeight;
                var buses = Machine.Definition.Buses.Select((b, i) => BusY(b, i)).DefaultIfEmpty(0).Max();
                return Math.Max(bottom, buses) + 60;
            }
        }
        private static string N(double value) => FormattableString.Invariant($"{value:0.#}");
        private static string Px(double value) => FormattableString.Invariant($"{value:0.#}px");

        // ---- Memory view ----

        private IEnumerable<IBusDevice> MemoryDevices { get { return Machine.Devices.Where(d => d is RomModule || d is MMU); } }
        private RomModule MemoryModule
        {
            get
            {
                var device = Machine.Device(memoryDevice);
                return device switch
                {
                    MMU mmu => mmu.RamBanks[Math.Clamp(memoryBank, 0, mmu.RamBanks.Length - 1)],
                    RomModule rom => rom,
                    _ => null,
                };
            }
        }
        private int Bank { get { return Machine.Device(memoryDevice) is MMU ? memoryBank : -1; } }
        private IEnumerable<int> ProgramCounterValues
        {
            get { return Machine.Devices.OfType<ProgramCounter>().Select(p => (int)p.Data); }
        }
        private string CellClass(int address, RomModule module)
        {
            var classes = new List<string>();
            if (module.memoryAddress == address) classes.Add("mar");
            bool isProgram = memoryDevice == Machine.Definition.ProgramMemory;
            if (isProgram && ProgramCounterValues.Contains(address)) classes.Add("pc");
            var current = ListingAt(Machine.CurrentInstructionAddress);
            if (isProgram && current != null && address >= current.Address && address < current.Address + current.Bytes.Length) classes.Add("current");
            if (Last?.Writes.Any(w => w.Device == memoryDevice && w.Bank == Bank && w.Address == address) == true) classes.Add("written");
            else if (Machine.History.Skip(Math.Max(0, Machine.History.Count - 30)).Any(t => t.Writes.Any(w => w.Device == memoryDevice && w.Bank == Bank && w.Address == address))) classes.Add("recent");
            if (module.memory[address] == 0) classes.Add("zero");
            return string.Join(" ", classes);
        }

        // ---- Control lines and decoder ROM ----

        // Every control line of the machine, grouped by device, in definition order.
        private List<(DeviceDefinition Device, string Color, List<string> Lines)> ControlLines
        {
            get
            {
                var groups = new List<(DeviceDefinition, string, List<string>)>();
                foreach (var device in Machine.Definition.Devices)
                {
                    var info = Info(device);
                    var lines = info.ControlLines.Count > 0 ? info.ControlLines.Select(l => l.Name).ToList() : Machine.Device(device.Id)?.SignalLines() ?? new List<string>();
                    if (lines.Count > 0) groups.Add((device, CategoryColor(info), lines));
                }
                return groups;
            }
        }
        private bool LineActive(string deviceId, string line)
        {
            return Last?.Signals.Contains($"{deviceId}.{line}") == true;
        }
        private int DecoderStatus { get { return (Last?.Status ?? Machine.Status) & 0x0F; } }
        private int DecoderStep { get { return Last?.MicroStep ?? Machine.MicroStepRegister; } }
        private int NextAddress { get { return ((Machine.Status & 0x0F) << 8) | Machine.MicroStepRegister; } }

        // ROM rows to show: the fetch block and the block the decoder just used, or the whole ROM.
        private List<(int Address, string Label, bool BlockStart, HashSet<string> Signals)> RomRows
        {
            get
            {
                var rom = Machine.DecoderRom;
                var blocks = rom.Blocks.Where((b, i) => wholeRom || i == 0 || (DecoderStep >= b.Base && DecoderStep < b.Base + b.Count)).ToList();
                var rows = new List<(int, string, bool, HashSet<string>)>();
                foreach (var block in blocks)
                {
                    for (int offset = 0; offset < block.Count; offset++)
                    {
                        var address = block.Base + offset;
                        var signals = new HashSet<string>(rom.FetchInstruction((byte)DecoderStatus, (byte)address).Select(m => $"{m.DeviceID}.{m.Function}"));
                        rows.Add((address, $"{block.Instruction.Mnemonic}.{offset + 1}", offset == 0, signals));
                    }
                }
                return rows;
            }
        }
        private static string Bits(int value, int count)
        {
            return Convert.ToString(value, 2).PadLeft(count, '0');
        }

        // ---- Trace ----

        private string DescribeTransfers(TickRecord tick)
        {
            var parts = tick.Transfers.Where(t => t.Driver != null)
                .Select(t => $"{t.Driver} → {t.Bus} {Hex(t.Value)}" + (t.Readers.Count > 0 ? $" → {string.Join(", ", t.Readers)}" : ""));
            return string.Join(" · ", parts);
        }
        private string DescribeChanges(TickRecord tick)
        {
            var parts = tick.Changes.Select(c => $"{c.Device} {Hex(c.Before)}→{Hex(c.After)}")
                .Concat(tick.Writes.Select(w => $"{w.Device}{(w.Bank >= 0 ? $"[{w.Bank}]" : "")}[{Hex(w.Address)}]={Hex(w.Value)}"));
            return string.Join(" · ", parts);
        }
    }
}
