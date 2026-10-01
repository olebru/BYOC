using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Exuarch.Core
{
    // Routes wires between cards as right angled paths that go around the cards instead of over them, the way a
    // schematic editor draws them. Each wire leaves its socket to the right and enters its target card from whichever
    // side is cheapest. The search runs on a sparse grid made of the cards' edges, so it stays small however large the
    // canvas is: A* over (point, direction), where a bend costs extra and running along an earlier wire costs more.
    // Wires that still share a stretch are then moved apart into lanes of their own.
    public class WireRouter
    {
        public readonly struct Rect
        {
            public readonly double X, Y, Width, Height;
            public Rect(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
            public double Right => X + Width;
            public double Bottom => Y + Height;
            public Rect Inflate(double by) => new Rect(X - by, Y - by, Width + 2 * by, Height + 2 * by);
        }

        public class Wire
        {
            public double StartX, StartY;
            public int Target;
            // Spreads the wires that end on the same card along its side, so their arrows do not land on top of each other.
            public double Spread;
            public List<(double X, double Y)> Points;
        }

        // Space kept free around every card, and between wires that run side by side.
        public const double Margin = 12;
        public const double Lane = 6;
        private const double BendCost = 40;
        private const double SharedCost = 3;

        private readonly IReadOnlyList<Rect> cards;
        private readonly Rect[] blocked;
        // Lines a wire may cross but never run along: the buses, and the wires from ports down or up to them.
        private readonly double[] busYs;
        private readonly (double X, double Top, double Bottom)[] taps;
        private const double Beside = 7;

        public WireRouter(IReadOnlyList<Rect> cards, IEnumerable<double> buses = null, IEnumerable<(double X, double Top, double Bottom)> taps = null)
        {
            this.cards = cards;
            blocked = cards.Select(c => c.Inflate(Margin - 0.5)).ToArray();
            busYs = buses?.ToArray() ?? Array.Empty<double>();
            this.taps = taps?.ToArray() ?? Array.Empty<(double, double, double)>();
        }

        // Whether a stretch would lie along a bus or a port's wire.
        private bool AlongLine(double x1, double y1, double x2, double y2)
        {
            if (y1 == y2)
            {
                foreach (var y in busYs) if (Math.Abs(y1 - y) < Beside) return true;
                return false;
            }
            double top = Math.Min(y1, y2), bottom = Math.Max(y1, y2);
            foreach (var t in taps) if (Math.Abs(x1 - t.X) < Beside && bottom > t.Top && top < t.Bottom) return true;
            return false;
        }

        // Routes every wire, shortest first, then separates the stretches they share. A wire that can not be routed
        // keeps Points null.
        public void Route(IList<Wire> wires)
        {
            var used = new Dictionary<(double, double, double, double), int>();
            foreach (var wire in wires.OrderBy(w => Math.Abs(w.StartX - Centre(cards[w.Target]).X) + Math.Abs(w.StartY - Centre(cards[w.Target]).Y)))
            {
                wire.Points = RouteOne(wire, used);
                if (wire.Points == null) continue;
                for (int i = 1; i + 2 < wire.Points.Count; i++)
                {
                    var key = Key(wire.Points[i], wire.Points[i + 1]);
                    used[key] = used.GetValueOrDefault(key) + 1;
                }
            }
            Separate(wires.Where(w => w.Points != null).ToList());
        }

        private static (double X, double Y) Centre(Rect r) => (r.X + r.Width / 2, r.Y + r.Height / 2);
        private static (double, double, double, double) Key((double X, double Y) a, (double X, double Y) b)
        {
            return a.X < b.X || (a.X == b.X && a.Y < b.Y) ? (a.X, a.Y, b.X, b.Y) : (b.X, b.Y, a.X, a.Y);
        }

        private bool Inside(double x, double y)
        {
            foreach (var r in blocked) if (x > r.X && x < r.Right && y > r.Y && y < r.Bottom) return true;
            return false;
        }
        // Whether a horizontal or vertical stretch passes through a card's keep-out area.
        private bool Crosses(double x1, double y1, double x2, double y2)
        {
            double left = Math.Min(x1, x2), right = Math.Max(x1, x2), top = Math.Min(y1, y2), bottom = Math.Max(y1, y2);
            foreach (var r in blocked)
            {
                if (left == right ? (left > r.X && left < r.Right && bottom > r.Y && top < r.Bottom)
                                  : (top > r.Y && top < r.Bottom && right > r.X && left < r.Right)) return true;
            }
            return false;
        }

        // Directions: 0 right, 1 down, 2 left, 3 up.
        private static readonly int[] DX = { 1, 0, -1, 0 }, DY = { 0, 1, 0, -1 };

        private List<(double X, double Y)> RouteOne(Wire wire, Dictionary<(double, double, double, double), int> used)
        {
            var target = cards[wire.Target];
            var start = (X: wire.StartX + Margin, Y: wire.StartY);
            if (Inside(start.X, start.Y)) return null;
            // The point just outside each side of the target, and the direction the wire has to travel to go in there.
            double cx = target.X + target.Width / 2 + Math.Clamp(wire.Spread, -target.Width / 2 + 8, target.Width / 2 - 8);
            double cy = target.Y + target.Height / 2 + Math.Clamp(wire.Spread, -target.Height / 2 + 8, target.Height / 2 - 8);
            var goals = new List<(double X, double Y, int Dir, double EdgeX, double EdgeY)>
            {
                (target.X - Margin, cy, 0, target.X, cy),
                (target.Right + Margin, cy, 2, target.Right, cy),
                (cx, target.Y - Margin, 1, cx, target.Y),
                (cx, target.Bottom + Margin, 3, cx, target.Bottom),
            };
            goals.RemoveAll(g => Inside(g.X, g.Y) || AlongLine(g.X, g.Y, g.EdgeX, g.EdgeY));
            if (goals.Count == 0) return null;

            var xs = new SortedSet<double>(Lanes(blocked.SelectMany(r => new[] { r.X - 0.5, r.Right + 0.5 }))) { start.X };
            var ys = new SortedSet<double>(Lanes(blocked.SelectMany(r => new[] { r.Y - 0.5, r.Bottom + 0.5 }))) { start.Y };
            foreach (var g in goals) { xs.Add(g.X); ys.Add(g.Y); }
            foreach (var y in busYs) { ys.Add(y - 2 * Beside); ys.Add(y + 2 * Beside); }
            foreach (var t in taps) { xs.Add(t.X - 2 * Beside); xs.Add(t.X + 2 * Beside); }
            var X = xs.ToArray();
            var Y = ys.ToArray();
            int nx = X.Length, ny = Y.Length;
            int Index(int ix, int iy) => iy * nx + ix;
            var free = new bool[nx * ny];
            for (int iy = 0; iy < ny; iy++)
                for (int ix = 0; ix < nx; ix++) free[Index(ix, iy)] = !Inside(X[ix], Y[iy]);
            // Whether the step from a grid point to its neighbour in a direction is clear.
            bool Open(int ix, int iy, int dir)
            {
                int jx = ix + DX[dir], jy = iy + DY[dir];
                if (jx < 0 || jy < 0 || jx >= nx || jy >= ny || !free[Index(jx, jy)]) return false;
                return !Crosses(X[ix], Y[iy], X[jx], Y[jy]) && !AlongLine(X[ix], Y[iy], X[jx], Y[jy]);
            }

            int sx = Array.IndexOf(X, start.X), sy = Array.IndexOf(Y, start.Y);
            var goalAt = new Dictionary<int, int>();
            for (int i = 0; i < goals.Count; i++) goalAt[Index(Array.IndexOf(X, goals[i].X), Array.IndexOf(Y, goals[i].Y))] = i;
            double Heuristic(int ix, int iy)
            {
                double best = double.MaxValue;
                foreach (var g in goals) best = Math.Min(best, Math.Abs(X[ix] - g.X) + Math.Abs(Y[iy] - g.Y));
                return best;
            }

            int states = nx * ny * 4;
            var cost = new double[states];
            var from = new int[states];
            Array.Fill(cost, double.MaxValue);
            Array.Fill(from, -1);
            var queue = new PriorityQueue<int, double>();
            int first = Index(sx, sy) * 4 + 0;
            cost[first] = 0;
            queue.Enqueue(first, Heuristic(sx, sy));
            int found = -1;
            double foundCost = double.MaxValue;
            while (queue.TryDequeue(out var state, out var priority))
            {
                if (priority >= foundCost) break;
                int node = state / 4, dir = state % 4, ix = node % nx, iy = node / nx;
                if (goalAt.TryGetValue(node, out var gi))
                {
                    double total = cost[state] + (dir == goals[gi].Dir ? 0 : BendCost * (dir == (goals[gi].Dir + 2) % 4 ? 2 : 1));
                    if (total < foundCost) { foundCost = total; found = state; }
                }
                for (int d = 0; d < 4; d++)
                {
                    if (d == (dir + 2) % 4 || !Open(ix, iy, d)) continue;
                    int jx = ix + DX[d], jy = iy + DY[d];
                    double length = Math.Abs(X[jx] - X[ix]) + Math.Abs(Y[jy] - Y[iy]);
                    double step = length * (1 + SharedCost * used.GetValueOrDefault(Key((X[ix], Y[iy]), (X[jx], Y[jy])))) + (d == dir ? 0 : BendCost);
                    int next = Index(jx, jy) * 4 + d;
                    if (cost[state] + step >= cost[next]) continue;
                    cost[next] = cost[state] + step;
                    from[next] = state;
                    queue.Enqueue(next, cost[next] + Heuristic(jx, jy));
                }
            }
            if (found < 0) return null;

            var points = new List<(double X, double Y)>();
            for (int s = found; s >= 0; s = from[s]) points.Add((X[s / 4 % nx], Y[s / 4 / nx]));
            points.Reverse();
            var goal = goals[goalAt[found / 4]];
            points.Insert(0, (wire.StartX, wire.StartY));
            points.Add((goal.EdgeX, goal.EdgeY));
            return Simplify(points);
        }

        // The lines wires can run along, one just outside each card edge. Two that are closer than two lanes, on either
        // side of a narrow gap between cards, become one down the middle of it.
        private static IEnumerable<double> Lanes(IEnumerable<double> edges)
        {
            var sorted = edges.Distinct().OrderBy(v => v).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i + 1 < sorted.Count && sorted[i + 1] - sorted[i] < 2 * Lane)
                {
                    yield return (sorted[i] + sorted[i + 1]) / 2;
                    i++;
                }
                else yield return sorted[i];
            }
        }

        // Drops points in the middle of a straight stretch.
        private static List<(double X, double Y)> Simplify(List<(double X, double Y)> points)
        {
            var result = new List<(double X, double Y)>();
            foreach (var p in points)
            {
                if (result.Count > 0 && result[^1] == p) continue;
                if (result.Count >= 2)
                {
                    var a = result[^2];
                    var b = result[^1];
                    if ((a.X == b.X && b.X == p.X) || (a.Y == b.Y && b.Y == p.Y)) result[^1] = p;
                    else result.Add(p);
                }
                else result.Add(p);
            }
            return result;
        }

        // Wires that run along the same line over the same stretch are moved apart, one lane each. The first and the
        // last stretch, which leave the socket and enter the card, stay put.
        private void Separate(List<Wire> wires)
        {
            var segments = new List<(Wire Wire, int Index, bool Horizontal, double At, double From, double To)>();
            foreach (var w in wires)
            {
                for (int i = 1; i + 2 < w.Points.Count; i++)
                {
                    var (a, b) = (w.Points[i], w.Points[i + 1]);
                    bool horizontal = a.Y == b.Y;
                    segments.Add((w, i, horizontal, horizontal ? a.Y : a.X, Math.Min(horizontal ? a.X : a.Y, horizontal ? b.X : b.Y), Math.Max(horizontal ? a.X : a.Y, horizontal ? b.X : b.Y)));
                }
            }
            foreach (var line in segments.GroupBy(s => (s.Horizontal, s.At)))
            {
                var group = line.OrderBy(s => s.From).ToList();
                // Clusters of stretches that overlap one another.
                var clusters = new List<List<(Wire Wire, int Index, bool Horizontal, double At, double From, double To)>>();
                double reach = double.MinValue;
                foreach (var s in group)
                {
                    if (clusters.Count == 0 || s.From >= reach) clusters.Add(new());
                    clusters[^1].Add(s);
                    reach = Math.Max(reach, s.To);
                }
                foreach (var cluster in clusters.Where(c => c.Select(s => s.Wire).Distinct().Count() > 1))
                {
                    var owners = cluster.Select(s => s.Wire).Distinct().ToList();
                    for (int k = 0; k < owners.Count; k++)
                    {
                        double offset = (k - (owners.Count - 1) / 2.0) * Lane;
                        foreach (var s in cluster.Where(s => s.Wire == owners[k])) Shift(s.Wire, s.Index, s.Horizontal, offset);
                    }
                }
            }
        }

        private void Shift(Wire wire, int index, bool horizontal, double offset)
        {
            var (a, b) = (wire.Points[index], wire.Points[index + 1]);
            (double X, double Y) na = horizontal ? (a.X, a.Y + offset) : (a.X + offset, a.Y);
            (double X, double Y) nb = horizontal ? (b.X, b.Y + offset) : (b.X + offset, b.Y);
            // Only if the moved stretch, and the two it joins, stay clear of the cards themselves.
            var before = wire.Points[index - 1];
            var after = wire.Points[index + 2];
            bool Clear((double X, double Y) p, (double X, double Y) q)
            {
                double left = Math.Min(p.X, q.X), right = Math.Max(p.X, q.X), top = Math.Min(p.Y, q.Y), bottom = Math.Max(p.Y, q.Y);
                return !cards.Any(r => right > r.X && left < r.Right && bottom > r.Y && top < r.Bottom);
            }
            if (!Clear(na, nb) || !Clear(before, na) || !Clear(nb, after) || AlongLine(na.X, na.Y, nb.X, nb.Y)) return;
            wire.Points[index] = na;
            wire.Points[index + 1] = nb;
        }

        // An SVG path through the points, with the corners rounded a little.
        public static string Path(IReadOnlyList<(double X, double Y)> points, double radius = 6)
        {
            string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
            var path = new System.Text.StringBuilder($"M {N(points[0].X)} {N(points[0].Y)}");
            for (int i = 1; i < points.Count; i++)
            {
                var p = points[i];
                if (i == points.Count - 1) { path.Append($" L {N(p.X)} {N(p.Y)}"); break; }
                var (prev, next) = (points[i - 1], points[i + 1]);
                double r = Math.Min(radius, Math.Min(Distance(prev, p), Distance(p, next)) / 2);
                var a = Toward(p, prev, r);
                var b = Toward(p, next, r);
                path.Append($" L {N(a.X)} {N(a.Y)} Q {N(p.X)} {N(p.Y)} {N(b.X)} {N(b.Y)}");
            }
            return path.ToString();
        }
        private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        private static (double X, double Y) Toward((double X, double Y) from, (double X, double Y) to, double by)
        {
            double length = Distance(from, to);
            return length == 0 ? from : (from.X + (to.X - from.X) / length * by, from.Y + (to.Y - from.Y) / length * by);
        }
    }
}
