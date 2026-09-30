using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Exuarch.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Exuarch.Web.Components
{
    public partial class MachineEditor
    {
        public const double CardWidth = 150;
        private const double CardBaseHeight = 58;
        private const double ConnectionRowHeight = 18;
        private const double Grid = 10;
        private const double BusSpacing = 320;
        private const double FirstBusY = 190;
        private const double BusSnapDistance = 22;
        private const double MinCanvasWidth = 1200;
        private const double MinCanvasHeight = 560;


        [Inject] private IJSRuntime JS { get; set; }

        [Parameter] public MachineDefinition Definition { get; set; }
        [Parameter] public EventCallback<MachineDefinition> DefinitionChanged { get; set; }
        [Parameter] public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
        [Parameter] public Machine Preview { get; set; }
        [Parameter] public DeviceRegistry Registry { get; set; } = DeviceRegistry.CreateDefault();
        // Undo history shared with the microcode editor.
        [Parameter] public EditHistory History { get; set; }
        // Asks the page to show an instruction step in the microcode editor.
        [Parameter] public EventCallback<(string Mnemonic, int Step)> OnOpenMicrocode { get; set; }
        // Selects this device when FocusVersion changes, so other views can point at a device.
        [Parameter] public string FocusDevice { get; set; }
        [Parameter] public int FocusVersion { get; set; }

        private ElementReference canvasElement;
        private ElementReference paletteElement;
        // The device type the pointer is over in the palette, and where its card goes (fixed, beside the palette).
        private (DeviceTypeInfo Info, double Left, double Top)? tip;
        private ElementReference canvasScroller;
        private (string Id, List<(InstructionDefinition Instruction, int Step, string Signal)> Usages)? pendingDelete;
        private int focusVersionSeen;
        private MachineDefinition laidOut;
        private string selectedDevice;
        private string selectedBus;
        private bool selectedDecoder;
        private string renameError;
        private bool showProblems;
        private double zoom = 0.8;
        private Drag drag;

        private enum DragKind { MoveDevice, MoveBus, NewDevice, WirePort, WireConnection, MoveDecoder, WireDecoder }

        private class Drag
        {
            public DragKind Kind;
            public string DeviceId;
            public string BusId;
            public string Port;
            public string Connection;
            public string NewType;
            public double StartClientX, StartClientY;
            public double OriginX, OriginY;
            public double CanvasLeft, CanvasTop;
            public double X, Y;
            public bool HasRect;
            public bool Moved;
            public string UndoSnapshot;
        }

        private class Rect
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        protected override void OnParametersSet()
        {
            if (!ReferenceEquals(laidOut, Definition))
            {
                laidOut = Definition;
                if (Definition != null) EnsureLayout();
                if (selectedDevice != null && Definition?.FindDevice(selectedDevice) == null) selectedDevice = null;
                if (selectedBus != null && Definition?.FindBus(selectedBus) == null) selectedBus = null;
            }
            if (FocusVersion != focusVersionSeen)
            {
                focusVersionSeen = FocusVersion;
                if (FocusDevice != null && Definition?.FindDevice(FocusDevice) != null) Select(FocusDevice);
            }
        }

        // ---- Geometry ----

        private DeviceTypeInfo Info(DeviceDefinition device)
        {
            return Registry.Info(device.Type) ?? new DeviceTypeInfo { Type = device.Type, Ports = device.Ports().Select(p => p.Key).ToList() };
        }
        private double CardHeight(DeviceDefinition device)
        {
            var connections = Info(device).Connections.Count;
            return CardBaseHeight + (connections > 0 ? 12 + connections * ConnectionRowHeight : 0);
        }
        private double CanvasWidth
        {
            get
            {
                var right = Definition.Devices.Select(d => d.Layout?.X ?? 0).DefaultIfEmpty(0).Max() + CardWidth + 60;
                var decoderRight = (Definition.Decoder?.Layout?.X ?? 0) + CardWidth + 60;
                return Math.Max(MinCanvasWidth, Math.Max(right, decoderRight));
            }
        }
        private double CanvasHeight
        {
            get
            {
                var devicesBottom = Definition.Devices.Select(d => (d.Layout?.Y ?? 0) + CardHeight(d)).DefaultIfEmpty(0).Max();
                var decoderBottom = (Definition.Decoder?.Layout?.Y ?? 0) + DecoderHeight;
                var busBottom = Definition.Buses.Select(b => b.Layout?.Y ?? 0).DefaultIfEmpty(0).Max();
                return Math.Max(MinCanvasHeight, Math.Max(Math.Max(devicesBottom, decoderBottom), busBottom) + 100);
            }
        }

        // ---- The decoder, drawn as a card: its sockets name the devices it works with ----

        private static readonly (string Name, string Label, string Description)[] DecoderSockets =
        {
            ("status", "status", "Status register: its flags pick which steps of an instruction run"),
            ("instructionRegister", "steps", "Instruction register: the micro step counter that addresses the decoder ROM"),
            ("interrupts", "interrupts", "Interrupt controller for the I condition, optional"),
        };
        private static double DecoderHeight { get { return CardBaseHeight + 12 + DecoderSockets.Length * ConnectionRowHeight; } }
        private string DecoderTarget(string socket)
        {
            var decoder = Definition.Decoder;
            return socket switch
            {
                "status" => decoder?.Status,
                "instructionRegister" => decoder?.InstructionRegister,
                _ => decoder?.Interrupts,
            };
        }
        private Task SetDecoderTarget(string socket, string deviceId)
        {
            return SetDecoder(d =>
            {
                if (socket == "status") d.Status = deviceId;
                else if (socket == "instructionRegister") d.InstructionRegister = deviceId;
                else d.Interrupts = deviceId;
            });
        }
        // To a device on its right the wire curves across like any connection. To one on its left it runs like a trace:
        // out of the socket, up above both cards, across, and down onto the target's top edge. Each socket gets its own
        // lane so the wires do not lie on top of each other.
        private string DecoderPath(int index, DeviceDefinition to)
        {
            var layout = Definition.Decoder.Layout;
            var sx = layout.X + CardWidth;
            var sy = layout.Y + ConnectionRowY(index);
            if (to.Layout.X >= sx) return CurveTo(sx, sy, to.Layout.X, to.Layout.Y + CardHeight(to) / 2);
            var out_ = sx + 12 + index * 8;
            var above = Math.Max(6, Math.Min(layout.Y, to.Layout.Y) - 14 - index * 8);
            var tx = to.Layout.X + CardWidth / 2 + (index - 1) * 12;
            return FormattableString.Invariant($"M {sx:0.#} {sy:0.#} H {out_:0.#} V {above:0.#} H {tx:0.#} V {to.Layout.Y:0.#}");
        }
        // Problems with the decoder name it: "decoder" or "decoder.status" and so on.
        private bool DecoderHasProblem
        {
            get { return Errors.Any(e => e.Contains("\"decoder")); }
        }
        private int? DecoderStep
        {
            get
            {
                var id = Definition.Decoder?.InstructionRegister;
                return id != null && Preview?.Device(id) is InstructionRegister register ? register.Data : null;
            }
        }
        private async Task AddDecoder()
        {
            if (Definition.Decoder?.Layout != null) { SelectDecoder(); return; }
            await Mutate(() =>
            {
                Definition.Decoder ??= new DecoderDefinition();
                Definition.EnsureLayout();
            });
            SelectDecoder();
        }
        private string BusColor(string busId)
        {
            var index = Definition.Buses.FindIndex(b => b.Id == busId);
            return Palette.Bus(index);
        }
        private static string CategoryColor(DeviceTypeInfo info)
        {
            return Palette.Category(info);
        }
        private double BusY(string busId)
        {
            return Definition.FindBus(busId)?.Layout?.Y ?? 0;
        }

        // Port anchors sit on the card edge that faces the port's bus.
        private (double X, double Y, bool Bottom) PortAnchor(DeviceDefinition device, string port)
        {
            var ports = Info(device).Ports;
            var index = Math.Max(0, ports.IndexOf(port));
            var x = device.Layout.X + CardWidth * (index + 1) / (ports.Count + 1);
            var busId = device.GetPortBus(port);
            var height = CardHeight(device);
            bool bottom = busId == null || BusY(busId) >= device.Layout.Y + height / 2;
            return (x, bottom ? device.Layout.Y + height : device.Layout.Y, bottom);
        }
        private double ConnectionRowY(int index)
        {
            return CardBaseHeight + 4 + index * ConnectionRowHeight + ConnectionRowHeight / 2;
        }
        private string ConnectionPath(DeviceDefinition from, int index, DeviceDefinition to)
        {
            var sx = from.Layout.X + CardWidth;
            var sy = from.Layout.Y + ConnectionRowY(index);
            var targetRight = to.Layout.X + CardWidth / 2 < sx;
            var tx = targetRight ? to.Layout.X + CardWidth : to.Layout.X;
            var ty = to.Layout.Y + CardHeight(to) / 2;
            return CurveTo(sx, sy, tx, ty, targetRight);
        }
        private static string CurveTo(double sx, double sy, double tx, double ty, bool targetRight = false)
        {
            var bend = Math.Max(50, Math.Abs(tx - sx) / 2);
            var c2 = targetRight ? tx + bend : tx - bend;
            return FormattableString.Invariant($"M {sx:0.#} {sy:0.#} C {sx + bend:0.#} {sy:0.#}, {c2:0.#} {ty:0.#}, {tx:0.#} {ty:0.#}");
        }
        private static string Px(double value) => FormattableString.Invariant($"{value:0.#}px");
        private static string N(double value) => FormattableString.Invariant($"{value:0.#}");
        private static double Snap(double value) => Math.Round(value / Grid) * Grid;

        private DeviceDefinition DeviceAt(double x, double y)
        {
            return Definition.Devices.LastOrDefault(d => d.Layout != null
                && x >= d.Layout.X && x <= d.Layout.X + CardWidth
                && y >= d.Layout.Y && y <= d.Layout.Y + CardHeight(d));
        }
        private BusDefinition BusNear(double y)
        {
            return Definition.Buses.Where(b => b.Layout != null && Math.Abs(b.Layout.Y - y) <= BusSnapDistance)
                                   .OrderBy(b => Math.Abs(b.Layout.Y - y)).FirstOrDefault();
        }
        private BusDefinition NearestBus(double y)
        {
            return Definition.Buses.Where(b => b.Layout != null).OrderBy(b => Math.Abs(b.Layout.Y - y)).FirstOrDefault();
        }

        private void EnsureLayout(bool force = false)
        {
            Definition.EnsureLayout(force);
        }

        // ---- Validation display ----

        private bool HasProblem(string id)
        {
            return Errors.Any(e => e.Contains($"'{id}'"));
        }
        private string ProblemTarget(string error)
        {
            return Definition.Devices.Select(d => d.Id).FirstOrDefault(id => error.Contains($"'{id}'"));
        }

        // ---- Editing ----

        private async Task Mutate(Action change)
        {
            History?.Record();
            change();
            await DefinitionChanged.InvokeAsync(Definition);
        }
        private void PushUndo(string snapshot)
        {
            History?.Push(snapshot);
        }
        private Task Undo() { return History?.Undo() ?? Task.CompletedTask; }
        private Task Redo() { return History?.Redo() ?? Task.CompletedTask; }
        private async Task Replace(MachineDefinition definition)
        {
            laidOut = definition;
            Definition = definition;
            if (selectedDevice != null && definition.FindDevice(selectedDevice) == null) selectedDevice = null;
            if (selectedBus != null && definition.FindBus(selectedBus) == null) selectedBus = null;
            await DefinitionChanged.InvokeAsync(definition);
        }

        private string IdPrefix(string type)
        {
            return type switch
            {
                "register" => "reg",
                "statusRegister" => "status",
                "instructionRegister" => "ir",
                "dualPortRegister" => "bridge",
                "clock" => "clk",
                _ => type.ToLowerInvariant(),
            };
        }
        private async Task AddDevice(string type, double? x = null, double? y = null)
        {
            var info = Registry.Info(type);
            var id = Definition.NextFreeId(IdPrefix(type));
            var position = x.HasValue ? new Position { X = Snap(x.Value), Y = Snap(y.Value) } : FreeSpot();
            var device = new DeviceDefinition { Id = id, Type = type, Layout = position };
            var bus = NearestBus(position.Y + CardBaseHeight / 2);
            if (bus != null && info.Ports.Count > 0) device.SetPortBus(info.Ports[0], bus.Id);
            await Mutate(() => Definition.Devices.Add(device));
            Select(id);
        }
        private Position FreeSpot()
        {
            var y = Definition.Devices.Select(d => d.Layout.Y + CardHeight(d)).DefaultIfEmpty(0).Max() + 40;
            return new Position { X = 30, Y = Snap(y) };
        }
        private async Task AddBus()
        {
            var id = Definition.NextFreeId("bus");
            var y = Definition.Buses.Select(b => b.Layout?.Y ?? 0).DefaultIfEmpty(FirstBusY - BusSpacing).Max() + BusSpacing;
            await Mutate(() => Definition.Buses.Add(new BusDefinition { Id = id, Layout = new Position { Y = Snap(y) } }));
            SelectBus(id);
        }
        // A device the microcode uses is only deleted after asking what to do with its signals.
        private async Task DeleteSelected()
        {
            if (selectedDevice != null)
            {
                var usages = Definition.SignalUsages(selectedDevice);
                if (usages.Count > 0)
                {
                    pendingDelete = (selectedDevice, usages);
                    return;
                }
                var id = selectedDevice;
                selectedDevice = null;
                await Mutate(() => Definition.RemoveDevice(id));
            }
            else if (selectedBus != null)
            {
                var id = selectedBus;
                selectedBus = null;
                await Mutate(() => Definition.RemoveBus(id));
            }
        }
        private async Task ConfirmDelete(bool removeSignals)
        {
            if (pendingDelete == null) return;
            var id = pendingDelete.Value.Id;
            pendingDelete = null;
            selectedDevice = null;
            await Mutate(() =>
            {
                Definition.RemoveDevice(id);
                if (removeSignals) Definition.RemoveSignalsOf(id);
            });
        }
        private void CancelDelete()
        {
            pendingDelete = null;
        }
        private List<(string Mnemonic, List<(int Step, string Line)> Steps)> UsageSummary(string deviceId)
        {
            return Definition.SignalUsages(deviceId)
                .GroupBy(u => u.Instruction.Mnemonic)
                .Select(g => (g.Key, g.Select(u => (u.Step, u.Signal.Substring(deviceId.Length + 1))).ToList()))
                .ToList();
        }

        private async Task DuplicateSelected()
        {
            var source = Definition.FindDevice(selectedDevice);
            if (source == null) return;
            var copy = Definition.Clone().FindDevice(source.Id);
            copy.Id = Definition.NextFreeId(IdPrefix(source.Type));
            copy.Name = null;
            copy.Layout = new Position { X = source.Layout.X + 30, Y = source.Layout.Y + 30 };
            await Mutate(() => Definition.Devices.Add(copy));
            Select(copy.Id);
        }
        private async Task AutoLayout()
        {
            await Mutate(() => EnsureLayout(force: true));
        }
        private async Task Download()
        {
            var name = string.IsNullOrWhiteSpace(Definition.Name) ? "machine" : Definition.Name;
            await JS.InvokeVoidAsync("exuarchEditor.download", $"{name}.json", Definition.ToJson());
        }

        private void Select(string deviceId)
        {
            selectedDevice = deviceId;
            selectedBus = null;
            selectedDecoder = false;
            renameError = null;
        }
        private void SelectBus(string busId)
        {
            selectedBus = busId;
            selectedDevice = null;
            selectedDecoder = false;
            renameError = null;
        }
        private void SelectDecoder()
        {
            selectedDecoder = true;
            selectedDevice = null;
            selectedBus = null;
            renameError = null;
        }
        private void ClearSelection()
        {
            selectedDevice = null;
            selectedBus = null;
            selectedDecoder = false;
            renameError = null;
        }

        private async Task RenameSelectedDevice(string newId)
        {
            var oldId = selectedDevice;
            try
            {
                History?.Record();
                Definition.RenameDevice(oldId, newId?.Trim());
                selectedDevice = newId.Trim();
                renameError = null;
                await DefinitionChanged.InvokeAsync(Definition);
            }
            catch (ArgumentException e)
            {
                History?.Discard();
                renameError = e.Message;
            }
        }
        private async Task RenameSelectedBus(string newId)
        {
            var oldId = selectedBus;
            try
            {
                History?.Record();
                Definition.RenameBus(oldId, newId?.Trim());
                selectedBus = newId.Trim();
                renameError = null;
                await DefinitionChanged.InvokeAsync(Definition);
            }
            catch (ArgumentException e)
            {
                History?.Discard();
                renameError = e.Message;
            }
        }
        private Task SetParameter(DeviceDefinition device, ParameterInfo parameter, string text)
        {
            return Mutate(() =>
            {
                if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text, out var value))
                {
                    device.Parameters.Remove(parameter.Name);
                    return;
                }
                value = Math.Clamp(value, parameter.Min, parameter.Max);
                using var document = JsonDocument.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                device.Parameters[parameter.Name] = document.RootElement.Clone();
            });
        }
        private static string ParameterText(DeviceDefinition device, ParameterInfo parameter)
        {
            return device.Parameters.TryGetValue(parameter.Name, out var value) ? value.ToString() : "";
        }
        private Task SetConnection(DeviceDefinition device, string connection, string targetId)
        {
            return Mutate(() =>
            {
                if (string.IsNullOrEmpty(targetId)) device.Connections.Remove(connection);
                else device.Connections[connection] = targetId;
            });
        }
        private Task SetPort(DeviceDefinition device, string port, string busId)
        {
            return Mutate(() => device.SetPortBus(port, string.IsNullOrEmpty(busId) ? null : busId));
        }
        private Task SetDecoder(Action<DecoderDefinition> change)
        {
            return Mutate(() =>
            {
                Definition.Decoder ??= new DecoderDefinition();
                change(Definition.Decoder);
            });
        }
        private static string Blank(string value) => string.IsNullOrEmpty(value) ? null : value;

        // ---- Pointer handling ----

        private async Task BeginDrag(PointerEventArgs e, Drag newDrag)
        {
            if (e.Button != 0) return;
            newDrag.StartClientX = e.ClientX;
            newDrag.StartClientY = e.ClientY;
            newDrag.UndoSnapshot = Definition.ToJson();
            drag = newDrag;
            var rect = await JS.InvokeAsync<Rect>("exuarchEditor.rect", canvasElement);
            newDrag.CanvasLeft = rect.Left;
            newDrag.CanvasTop = rect.Top;
            newDrag.HasRect = true;
            UpdatePointer(newDrag, e);
            await JS.InvokeVoidAsync("exuarchEditor.focus", canvasScroller);
        }
        private void UpdatePointer(Drag d, PointerEventArgs e)
        {
            d.X = (e.ClientX - d.CanvasLeft) / zoom;
            d.Y = (e.ClientY - d.CanvasTop) / zoom;
            if (Math.Abs(e.ClientX - d.StartClientX) + Math.Abs(e.ClientY - d.StartClientY) > 4) d.Moved = true;
        }

        private Task StartMoveDevice(PointerEventArgs e, DeviceDefinition device)
        {
            Select(device.Id);
            return BeginDrag(e, new Drag { Kind = DragKind.MoveDevice, DeviceId = device.Id, OriginX = device.Layout.X, OriginY = device.Layout.Y });
        }
        private Task StartMoveDecoder(PointerEventArgs e)
        {
            SelectDecoder();
            var layout = Definition.Decoder.Layout;
            return BeginDrag(e, new Drag { Kind = DragKind.MoveDecoder, OriginX = layout.X, OriginY = layout.Y });
        }
        private Task StartWireDecoder(PointerEventArgs e, string socket)
        {
            SelectDecoder();
            return BeginDrag(e, new Drag { Kind = DragKind.WireDecoder, Connection = socket });
        }
        private Task StartMoveBus(PointerEventArgs e, BusDefinition bus)
        {
            SelectBus(bus.Id);
            return BeginDrag(e, new Drag { Kind = DragKind.MoveBus, BusId = bus.Id, OriginY = bus.Layout.Y });
        }
        private async Task ShowTip(DeviceTypeInfo info, PointerEventArgs e)
        {
            var palette = await JS.InvokeAsync<ElementRect>("exuarchEditor.rect", paletteElement);
            // OffsetY is measured from the palette item itself: its children do not take pointer events.
            var top = Math.Max(8, e.ClientY - e.OffsetY - 8);
            tip = (info, palette.Left + palette.Width + 8, top);
        }
        private void HideTip()
        {
            tip = null;
        }
        private Task StartNewDevice(PointerEventArgs e, string type)
        {
            return BeginDrag(e, new Drag { Kind = DragKind.NewDevice, NewType = type });
        }
        private Task StartWirePort(PointerEventArgs e, DeviceDefinition device, string port)
        {
            Select(device.Id);
            return BeginDrag(e, new Drag { Kind = DragKind.WirePort, DeviceId = device.Id, Port = port });
        }
        private Task StartWireConnection(PointerEventArgs e, DeviceDefinition device, string connection)
        {
            Select(device.Id);
            return BeginDrag(e, new Drag { Kind = DragKind.WireConnection, DeviceId = device.Id, Connection = connection });
        }

        private void OnPointerMove(PointerEventArgs e)
        {
            if (drag == null || !drag.HasRect) return;
            UpdatePointer(drag, e);
            if (!drag.Moved) return;
            var dx = (e.ClientX - drag.StartClientX) / zoom;
            var dy = (e.ClientY - drag.StartClientY) / zoom;
            if (drag.Kind == DragKind.MoveDevice)
            {
                var device = Definition.FindDevice(drag.DeviceId);
                device.Layout.X = Math.Max(0, Snap(drag.OriginX + dx));
                device.Layout.Y = Math.Max(0, Snap(drag.OriginY + dy));
            }
            else if (drag.Kind == DragKind.MoveBus)
            {
                Definition.FindBus(drag.BusId).Layout.Y = Math.Max(20, Snap(drag.OriginY + dy));
            }
            else if (drag.Kind == DragKind.MoveDecoder)
            {
                Definition.Decoder.Layout.X = Math.Max(0, Snap(drag.OriginX + dx));
                Definition.Decoder.Layout.Y = Math.Max(0, Snap(drag.OriginY + dy));
            }
        }

        private async Task OnPointerUp(PointerEventArgs e)
        {
            var d = drag;
            drag = null;
            if (d == null || !d.HasRect) return;
            UpdatePointer(d, e);
            bool onCanvas = d.X >= 0 && d.Y >= 0 && d.X <= CanvasWidth && d.Y <= CanvasHeight;
            switch (d.Kind)
            {
                case DragKind.MoveDevice:
                case DragKind.MoveBus:
                case DragKind.MoveDecoder:
                    if (d.Moved)
                    {
                        PushUndo(d.UndoSnapshot);
                        await DefinitionChanged.InvokeAsync(Definition);
                    }
                    break;
                case DragKind.NewDevice:
                    if (d.Moved && onCanvas) await AddDevice(d.NewType, d.X - CardWidth / 2, d.Y - 20);
                    else if (!d.Moved) await AddDevice(d.NewType);
                    break;
                case DragKind.WirePort:
                    var bus = BusNear(d.Y);
                    var portDevice = Definition.FindDevice(d.DeviceId);
                    if (bus != null && portDevice.GetPortBus(d.Port) != bus.Id) await SetPort(portDevice, d.Port, bus.Id);
                    break;
                case DragKind.WireConnection:
                    var target = DeviceAt(d.X, d.Y);
                    var source = Definition.FindDevice(d.DeviceId);
                    if (target != null && target != source) await SetConnection(source, d.Connection, target.Id);
                    break;
                case DragKind.WireDecoder:
                    var decoderTarget = DeviceAt(d.X, d.Y);
                    if (decoderTarget != null && DecoderTarget(d.Connection) != decoderTarget.Id) await SetDecoderTarget(d.Connection, decoderTarget.Id);
                    break;
            }
        }

        // Leaving the editor cancels a drag; a moved element goes back where it was.
        private async Task OnPointerLeave(PointerEventArgs e)
        {
            var d = drag;
            drag = null;
            if (d != null && d.Moved && (d.Kind == DragKind.MoveDevice || d.Kind == DragKind.MoveBus || d.Kind == DragKind.MoveDecoder))
            {
                await Replace(MachineDefinition.FromJson(d.UndoSnapshot));
            }
        }

        private async Task OnKeyDown(KeyboardEventArgs e)
        {
            bool command = e.CtrlKey || e.MetaKey;
            if (command && e.Key.ToLowerInvariant() == "z" && !e.ShiftKey) await Undo();
            else if (command && (e.Key.ToLowerInvariant() == "y" || (e.Key.ToLowerInvariant() == "z" && e.ShiftKey))) await Redo();
            else if (command && e.Key.ToLowerInvariant() == "d") await DuplicateSelected();
            else if (e.Key == "Delete" || e.Key == "Backspace") await DeleteSelected();
            else if (e.Key == "Escape") { drag = null; pendingDelete = null; ClearSelection(); }
        }

        private void Zoom(double factor)
        {
            zoom = Math.Clamp(Math.Round(zoom * factor, 2), 0.4, 1.6);
        }
    }
}
