using System;
using System.Collections.Generic;
using System.Linq;

namespace Exuarch.Core
{
    // The geometry of a machine's drawing, shared by the hardware design canvas and the run view so both draw the
    // same cards in the same places: card sizes, where ports and connection sockets sit, how wires run, and how big
    // the canvas is. Positions come from the machine definition's layout.
    public class SchematicLayout
    {
        public const double CardWidth = 160;
        // Every card is at least this tall; a card with connection sockets grows to fit them below its body.
        public const double CardMinHeight = 96;
        // Where the first connection socket row starts: below the card's head and two lines of body.
        public const double SocketTop = 62;
        public const double SocketRowHeight = 18;
        public const double FirstBusY = 190;
        public const double BusSpacing = 320;
        public const double ColumnSpacing = 186;
        public const double MinCanvasWidth = 1200;
        public const double MinCanvasHeight = 560;

        // The decoder's sockets: the devices it works with.
        public static readonly (string Name, string Label, string Description)[] DecoderSockets =
        {
            ("status", "status", "Status register: its flags pick which steps of an instruction run"),
            ("instructionRegister", "steps", "Instruction register: the micro step counter that addresses the decoder ROM"),
            ("interrupts", "interrupts", "Interrupt controller for the I condition, optional"),
        };

        private readonly MachineDefinition definition;
        private readonly DeviceRegistry registry;

        public SchematicLayout(MachineDefinition definition, DeviceRegistry registry)
        {
            this.definition = definition;
            this.registry = registry;
        }

        public DeviceTypeInfo Info(DeviceDefinition device)
        {
            return registry.Info(device.Type) ?? new DeviceTypeInfo { Type = device.Type, Ports = device.Ports().Select(p => p.Key).ToList() };
        }

        public static double HeightFor(int sockets)
        {
            return sockets == 0 ? CardMinHeight : Math.Max(CardMinHeight, SocketTop + sockets * SocketRowHeight + 10);
        }
        public double CardHeight(DeviceDefinition device) => HeightFor(Info(device).Connections.Count);
        public static double DecoderHeight => HeightFor(DecoderSockets.Length);

        // The middle of a socket row, from the top of the card.
        public static double SocketRowY(int index) => SocketTop + index * SocketRowHeight + SocketRowHeight / 2;

        public double BusY(string busId) => definition.FindBus(busId)?.Layout?.Y ?? 0;

        // Port anchors sit on the card edge that faces the port's bus, spread evenly along it.
        public (double X, double Y, bool Bottom) PortAnchor(DeviceDefinition device, string port)
        {
            var ports = Info(device).Ports;
            var index = Math.Max(0, ports.IndexOf(port));
            var x = device.Layout.X + CardWidth * (index + 1) / (ports.Count + 1);
            var busId = device.GetPortBus(port);
            var height = CardHeight(device);
            bool bottom = busId == null || BusY(busId) >= device.Layout.Y + height / 2;
            return (x, bottom ? device.Layout.Y + height : device.Layout.Y, bottom);
        }

        // A connection curves from its socket on the right edge to the side of the target that faces it.
        public string ConnectionPath(DeviceDefinition from, int index, DeviceDefinition to)
        {
            var sx = from.Layout.X + CardWidth;
            var sy = from.Layout.Y + SocketRowY(index);
            var targetRight = to.Layout.X + CardWidth / 2 < sx;
            var tx = targetRight ? to.Layout.X + CardWidth : to.Layout.X;
            var ty = to.Layout.Y + CardHeight(to) / 2;
            return CurveTo(sx, sy, tx, ty, targetRight);
        }

        // To a device on its right the wire curves across like any connection. To one on its left it runs like a trace:
        // out of the socket, up above both cards, across, and down onto the target's top edge. Each socket gets its own
        // lane so the wires do not lie on top of each other.
        public string DecoderPath(int index, DeviceDefinition to)
        {
            var layout = definition.Decoder.Layout;
            var sx = layout.X + CardWidth;
            var sy = layout.Y + SocketRowY(index);
            if (to.Layout.X >= sx) return CurveTo(sx, sy, to.Layout.X, to.Layout.Y + CardHeight(to) / 2);
            var out_ = sx + 12 + index * 8;
            var above = Math.Max(6, Math.Min(layout.Y, to.Layout.Y) - 14 - index * 8);
            var tx = to.Layout.X + CardWidth / 2 + (index - 1) * 12;
            return FormattableString.Invariant($"M {sx:0.#} {sy:0.#} H {out_:0.#} V {above:0.#} H {tx:0.#} V {to.Layout.Y:0.#}");
        }

        public static string CurveTo(double sx, double sy, double tx, double ty, bool targetRight = false)
        {
            var bend = Math.Max(50, Math.Abs(tx - sx) / 2);
            var c2 = targetRight ? tx + bend : tx - bend;
            return FormattableString.Invariant($"M {sx:0.#} {sy:0.#} C {sx + bend:0.#} {sy:0.#}, {c2:0.#} {ty:0.#}, {tx:0.#} {ty:0.#}");
        }

        // The device a socket of the decoder names.
        public string DecoderTarget(string socket)
        {
            var decoder = definition.Decoder;
            return socket switch
            {
                "status" => decoder?.Status,
                "instructionRegister" => decoder?.InstructionRegister,
                _ => decoder?.Interrupts,
            };
        }

        public double CanvasWidth
        {
            get
            {
                var right = definition.Devices.Select(d => d.Layout?.X ?? 0).DefaultIfEmpty(0).Max() + CardWidth + 60;
                var decoderRight = (definition.Decoder?.Layout?.X ?? 0) + CardWidth + 60;
                return Math.Max(MinCanvasWidth, Math.Max(right, decoderRight));
            }
        }
        public double CanvasHeight
        {
            get
            {
                var devicesBottom = definition.Devices.Select(d => (d.Layout?.Y ?? 0) + CardHeight(d)).DefaultIfEmpty(0).Max();
                var decoderBottom = (definition.Decoder?.Layout?.Y ?? 0) + DecoderHeight;
                var busBottom = definition.Buses.Select(b => b.Layout?.Y ?? 0).DefaultIfEmpty(0).Max();
                return Math.Max(MinCanvasHeight, Math.Max(Math.Max(devicesBottom, decoderBottom), busBottom) + 100);
            }
        }

        // The rectangle a card covers, the decoder's as "decoder".
        public IEnumerable<(string Id, double X, double Y, double Width, double Height)> Cards()
        {
            foreach (var device in definition.Devices.Where(d => d.Layout != null))
            {
                yield return (device.Id, device.Layout.X, device.Layout.Y, CardWidth, CardHeight(device));
            }
            if (definition.Decoder?.Layout != null)
            {
                yield return ("decoder", definition.Decoder.Layout.X, definition.Decoder.Layout.Y, CardWidth, DecoderHeight);
            }
        }
    }
}
