using System;
using System.Collections.Generic;
using System.Linq;

namespace Exuarch.Core
{
    // Where the cards go when a machine is laid out automatically. Every bus gets at most one row of cards above it
    // and one below, so a card's port wire runs straight to its bus without passing another card. A device on two or
    // more buses sits in the gap between them, and the column it is in is kept free in the rows its wires pass. Within
    // the rows, devices are ordered to sit near the ones they are connected to, and the gaps between buses are as tall
    // as the rows in them need.
    public static class SchematicPlacement
    {
        public const double Left = 30;
        public const double Top = 40;
        public const double ColumnSpacing = 210;
        // From a card's edge to the bus it faces, and between the two rows that share the gap between two buses.
        public const double ToBus = 56;
        public const double BetweenRows = 70;

        private static readonly Lazy<DeviceRegistry> registry = new Lazy<DeviceRegistry>(() => DeviceRegistry.CreateDefault());

        private static double Height(DeviceDefinition device)
        {
            return SchematicLayout.HeightFor(registry.Value.Info(device.Type)?.Connections.Count ?? 0);
        }

        // The rows: 2 * bus is the row above that bus, 2 * bus + 1 the row below it.
        private class Card
        {
            public DeviceDefinition Device;
            public bool IsDecoder;
            public int Row;
            public int Column;
            public double Height;
            // Other rows this card's wires pass through, where its column has to stay free.
            public List<int> Through = new List<int>();
            public List<Card> Partners = new List<Card>();
            public int Order;
        }

        public static void LayOut(MachineDefinition definition)
        {
            var buses = definition.Buses;
            if (buses.Count == 0)
            {
                // Nothing to arrange around: a grid.
                int i = 0;
                foreach (var device in definition.Devices) device.Layout = new Position { X = Left + (i % 6) * ColumnSpacing, Y = Top + (i++ / 6) * 140 };
                if (definition.Decoder != null) definition.Decoder.Layout = new Position { X = Left + 6 * ColumnSpacing, Y = Top };
                return;
            }
            var busIndex = buses.Select((b, i) => (b.Id, i)).ToDictionary(b => b.Id, b => b.i);
            var cards = definition.Devices.Select((d, i) => new Card { Device = d, Height = Height(d), Order = i }).ToList();
            var byId = cards.ToDictionary(c => c.Device.Id);
            Card decoder = null;
            if (definition.Decoder != null)
            {
                decoder = new Card { IsDecoder = true, Height = SchematicLayout.DecoderHeight, Order = cards.Count, Row = 0 };
                cards.Add(decoder);
            }

            // Who is wired to whom, either way round.
            void Link(Card a, Card b)
            {
                if (a == null || b == null || a == b) return;
                if (!a.Partners.Contains(b)) a.Partners.Add(b);
                if (!b.Partners.Contains(a)) b.Partners.Add(a);
            }
            foreach (var card in cards.Where(c => !c.IsDecoder))
            {
                foreach (var target in card.Device.Connections.Values) Link(card, byId.GetValueOrDefault(target));
            }
            if (decoder != null)
            {
                foreach (var id in new[] { definition.Decoder.Status, definition.Decoder.InstructionRegister, definition.Decoder.Interrupts })
                {
                    if (id != null) Link(decoder, byId.GetValueOrDefault(id));
                }
            }

            List<int> BusesOf(Card c) => c.IsDecoder ? new List<int>() :
                c.Device.Ports().Select(p => p.Value).Where(busIndex.ContainsKey).Select(b => busIndex[b]).Distinct().OrderBy(b => b).ToList();

            // Rows first. Devices on one bus go above or below it, towards the devices they are wired to; the rest
            // are shared out so the two rows stay even.
            var perSide = new Dictionary<int, int>();
            foreach (var card in cards.Where(c => !c.IsDecoder).OrderBy(c => BusesOf(c).Count > 1 ? 0 : 1).ThenBy(c => c.Order))
            {
                var on = BusesOf(card);
                if (on.Count == 0)
                {
                    card.Row = 0;
                }
                else if (on.Count == 1)
                {
                    int bus = on[0];
                    var partnerBuses = card.Partners.Where(p => !p.IsDecoder).SelectMany(BusesOf).ToList();
                    double lean = partnerBuses.Count == 0 ? 0 : partnerBuses.Average() - bus;
                    bool below;
                    if (lean > 0.25) below = true;
                    else if (lean < -0.25) below = false;
                    else below = perSide.GetValueOrDefault(2 * bus + 1) < perSide.GetValueOrDefault(2 * bus);
                    // The first bus's top row also holds the decoder and devices on no bus.
                    if (bus == 0 && !below && lean >= 0 && perSide.GetValueOrDefault(0) > perSide.GetValueOrDefault(1)) below = true;
                    card.Row = 2 * bus + (below ? 1 : 0);
                }
                else
                {
                    // In the gap between two of its buses: the one nearest the middle of where its ports and partners are.
                    var weights = on.Concat(card.Partners.Where(p => !p.IsDecoder).SelectMany(BusesOf)).OrderBy(b => b).ToList();
                    double middle = (weights[(weights.Count - 1) / 2] + weights[weights.Count / 2]) / 2.0;
                    int gap = Enumerable.Range(on.First(), on.Last() - on.First()).OrderBy(g => Math.Abs(g + 0.5 - middle)).First();
                    card.Row = 2 * gap + 1;
                    // The rows its wires pass on the way: up to the buses above, down to the ones below.
                    foreach (var bus in on)
                    {
                        if (bus <= gap) for (int row = 2 * bus + 1; row < card.Row; row++) card.Through.Add(row);
                        else for (int row = card.Row + 1; row <= 2 * bus; row++) card.Through.Add(row);
                    }
                    card.Through = card.Through.Distinct().ToList();
                }
                perSide[card.Row] = perSide.GetValueOrDefault(card.Row) + 1;
            }

            // Columns, then a few rounds of moving each card towards the average column of its partners.
            var rowCount = 2 * buses.Count;
            // Keys are columns: each card starts where it comes in its row.
            var keys = new Dictionary<Card, double>();
            foreach (var row in cards.GroupBy(c => c.Row))
            {
                int index = 0;
                foreach (var card in row.OrderBy(c => c.Order)) keys[card] = index++;
            }
            for (int round = 0; round < 4; round++)
            {
                Assign(cards, rowCount, keys);
                foreach (var card in cards)
                {
                    keys[card] = card.Partners.Count == 0 ? card.Column : (card.Column + card.Partners.Average(p => (double)p.Column)) / 2;
                }
                // Devices on no bus, and the decoder, stay at the end of the top row.
                foreach (var card in cards.Where(c => c.IsDecoder || BusesOf(c).Count == 0)) keys[card] = 1e9 + (card.IsDecoder ? 1 : 0);
            }
            Assign(cards, rowCount, keys);

            // Heights: each gap between two buses is as tall as its two rows.
            var rowHeight = new double[rowCount];
            foreach (var card in cards) rowHeight[card.Row] = Math.Max(rowHeight[card.Row], card.Height);
            var busY = new double[buses.Count];
            var rowTop = new double[rowCount];
            double y = Top;
            for (int b = 0; b < buses.Count; b++)
            {
                rowTop[2 * b] = y;
                busY[b] = rowHeight[2 * b] > 0 ? y + rowHeight[2 * b] + ToBus : y + (b == 0 ? 60 : 0);
                rowTop[2 * b + 1] = busY[b] + ToBus;
                y = rowHeight[2 * b + 1] > 0 ? rowTop[2 * b + 1] + rowHeight[2 * b + 1] + BetweenRows : busY[b] + ToBus + BetweenRows / 2;
            }
            for (int b = 0; b < buses.Count; b++) buses[b].Layout = new Position { X = 0, Y = busY[b] };
            foreach (var card in cards)
            {
                var position = new Position { X = Left + card.Column * ColumnSpacing, Y = rowTop[card.Row] };
                if (card.IsDecoder) definition.Decoder.Layout = position;
                else card.Device.Layout = position;
            }
        }

        // Columns in key order: cards that reach through other rows first, each in the leftmost column that is free in
        // every row it needs, then each row's other cards left to right.
        private static void Assign(List<Card> cards, int rowCount, Dictionary<Card, double> keys)
        {
            var taken = new HashSet<(int Row, int Column)>();
            bool Free(Card c, int column) => !taken.Contains((c.Row, column)) && c.Through.All(r => !taken.Contains((r, column)));
            void Take(Card c, int column)
            {
                c.Column = column;
                taken.Add((c.Row, column));
                foreach (var r in c.Through) taken.Add((r, column));
            }
            foreach (var card in cards.Where(c => c.Through.Count > 0).OrderBy(c => keys[c]).ThenBy(c => c.Order))
            {
                int column = Math.Max(0, (int)Math.Round(Math.Min(keys[card], 64)));
                while (!Free(card, column)) column++;
                Take(card, column);
            }
            foreach (var row in cards.Where(c => c.Through.Count == 0).GroupBy(c => c.Row))
            {
                int column = 0;
                foreach (var card in row.OrderBy(c => keys[c]).ThenBy(c => c.Order))
                {
                    while (!Free(card, column)) column++;
                    Take(card, column);
                    column++;
                }
            }
        }

        // A spot for one new card next to its bus, clear of every card already placed: the first free column in the row
        // above the bus or the row below it.
        public static Position FreeSpot(MachineDefinition definition, DeviceDefinition device)
        {
            var placed = definition.Devices.Where(d => d.Layout != null && d != device)
                .Select(d => (d.Layout.X, d.Layout.Y, W: SchematicLayout.CardWidth, H: Height(d))).ToList();
            if (definition.Decoder?.Layout != null) placed.Add((definition.Decoder.Layout.X, definition.Decoder.Layout.Y, SchematicLayout.CardWidth, SchematicLayout.DecoderHeight));
            double height = Height(device);
            var busId = device.Ports().Select(p => p.Value).FirstOrDefault(b => definition.FindBus(b)?.Layout != null);
            var busYs = definition.Buses.Where(b => b.Layout != null).Select(b => b.Layout.Y).ToList();
            bool Clear(double x, double y)
            {
                if (busYs.Any(b => b > y - 20 && b < y + height + 20)) return false;
                return !placed.Any(p => x < p.X + p.W + 20 && p.X < x + SchematicLayout.CardWidth + 20 && y < p.Y + p.H + 20 && p.Y < y + height + 20);
            }
            var rows = new List<double>();
            if (busId != null)
            {
                var bus = definition.FindBus(busId).Layout.Y;
                rows.Add(bus - ToBus - height);
                rows.Add(bus + ToBus);
            }
            else rows.Add(placed.Count == 0 ? Top : placed.Min(p => p.Y));
            for (int column = 0; column < 64; column++)
            {
                foreach (var y in rows.Where(r => r >= 0))
                {
                    var x = Left + column * ColumnSpacing;
                    if (Clear(x, y)) return new Position { X = x, Y = y };
                }
            }
            var bottom = placed.Select(p => p.Y + p.H).Concat(busYs).DefaultIfEmpty(Top).Max() + ToBus;
            return new Position { X = Left, Y = bottom };
        }
    }
}
