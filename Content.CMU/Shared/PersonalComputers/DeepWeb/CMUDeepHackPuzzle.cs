using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers.DeepWeb;

/// <summary>The two stages of a hack: lock onto the terminal's carrier, then route it to the core.</summary>
[Serializable, NetSerializable]
public enum CMUDeepHackStage : byte
{
    Tune,
    Route,
}

/// <summary>A circuit board tile. Its connections are <see cref="CMUDeepHackPuzzle.Mask"/> turned by its rotation.</summary>
[Serializable, NetSerializable]
public enum CMUDeepHackTile : byte
{
    Empty,
    Straight,
    Corner,
    Tee,
    Cross,
}

/// <summary>One tile of the board. Locked tiles are burnt in and do not turn.</summary>
[Serializable, NetSerializable]
public record struct CMUDeepHackCell(CMUDeepHackTile Tile, byte Rotation, bool Locked);

/// <summary>
/// Rules of the access tuner hack, shared so the window draws exactly what the server checks. The carrier is a
/// wave of frequency, amplitude and phase steps; the board is a grid of trace tiles from a source on the left
/// edge to the core on the right edge.
/// </summary>
public static class CMUDeepHackPuzzle
{
    public const int North = 1;
    public const int East = 2;
    public const int South = 4;
    public const int West = 8;

    public const int MaxFrequency = 8;
    public const int MaxAmplitude = 5;
    public const int PhaseSteps = 8;

    /// <summary>Connections of a tile at rotation 0.</summary>
    public static int Mask(CMUDeepHackTile tile)
    {
        return tile switch
        {
            CMUDeepHackTile.Straight => North | South,
            CMUDeepHackTile.Corner => North | East,
            CMUDeepHackTile.Tee => North | East | South,
            CMUDeepHackTile.Cross => North | East | South | West,
            _ => 0,
        };
    }

    /// <summary>Connections of a cell, each quarter turn moving them clockwise.</summary>
    public static int Connections(CMUDeepHackCell cell)
    {
        var mask = Mask(cell.Tile);
        for (var i = 0; i < cell.Rotation % 4; i++)
            mask = ((mask << 1) | (mask >> 3)) & 0xF;

        return mask;
    }

    /// <summary>The carrier's height at <paramref name="x"/> (0 to 1 across the scope), from -amplitude to amplitude.</summary>
    public static float Wave(int frequency, int amplitude, int phase, float x)
    {
        return amplitude * MathF.Sin(MathF.Tau * (frequency * x + phase / (float) PhaseSteps));
    }

    /// <summary>
    /// Which cells carry the signal from the source, walking through matching connections. The board is solved
    /// when the core's cell is powered and opens east.
    /// </summary>
    public static bool[] Powered(IReadOnlyList<CMUDeepHackCell> cells, int width, int height, int sourceRow)
    {
        var powered = new bool[cells.Count];
        var start = sourceRow * width;
        if ((Connections(cells[start]) & West) == 0)
            return powered;

        var queue = new Queue<int>();
        queue.Enqueue(start);
        powered[start] = true;
        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;
            var mask = Connections(cells[index]);
            foreach (var (dir, back, dx, dy) in new[] { (North, South, 0, -1), (East, West, 1, 0), (South, North, 0, 1), (West, East, -1, 0) })
            {
                if ((mask & dir) == 0)
                    continue;

                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;

                var next = ny * width + nx;
                if (powered[next] || (Connections(cells[next]) & back) == 0)
                    continue;

                powered[next] = true;
                queue.Enqueue(next);
            }
        }

        return powered;
    }

    public static bool Solved(IReadOnlyList<CMUDeepHackCell> cells, int width, int height, int sourceRow, int coreRow)
    {
        var powered = Powered(cells, width, height, sourceRow);
        var core = coreRow * width + width - 1;
        return powered[core] && (Connections(cells[core]) & East) != 0;
    }

    /// <summary>
    /// A board with one guaranteed route from source to core, every tile turned at random and a few route tiles
    /// burnt in at their right rotation as anchors.
    /// </summary>
    public static List<CMUDeepHackCell> NewBoard(
        System.Random rng,
        int width,
        int height,
        int sourceRow,
        int coreRow,
        int anchors,
        out List<byte> solution)
    {
        var route = Route(rng, width, height, sourceRow, coreRow);
        var cells = new CMUDeepHackCell[width * height];
        for (var i = 0; i < cells.Length; i++)
        {
            var tile = rng.NextDouble() < 0.15 ? CMUDeepHackTile.Empty : (CMUDeepHackTile) rng.Next(1, 5);
            cells[i] = new CMUDeepHackCell(tile, (byte) rng.Next(4), false);
        }

        for (var i = 0; i < route.Count; i++)
        {
            var from = i == 0 ? West : Toward(route[i], route[i - 1], width);
            var to = i == route.Count - 1 ? East : Toward(route[i], route[i + 1], width);
            var need = from | to;
            var tile = need is (North | South) or (East | West) ? CMUDeepHackTile.Straight : CMUDeepHackTile.Corner;
            if (rng.NextDouble() < 0.15)
                tile = rng.Next(2) == 0 ? CMUDeepHackTile.Tee : CMUDeepHackTile.Cross;

            // The rotation that covers every connection the route needs.
            byte right = 0;
            for (byte r = 0; r < 4; r++)
            {
                if ((Connections(new CMUDeepHackCell(tile, r, false)) & need) == need)
                {
                    right = r;
                    break;
                }
            }

            cells[route[i]] = new CMUDeepHackCell(tile, right, false);
        }

        solution = new List<byte>();
        foreach (var cell in cells)
            solution.Add(cell.Rotation);

        // Anchors stay right; everything else is scrambled, and the scramble must not already be solved.
        var anchored = new HashSet<int>();
        for (var i = 0; i < anchors && route.Count > 2; i++)
            anchored.Add(route[rng.Next(1, route.Count - 1)]);

        do
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (anchored.Contains(i))
                    cells[i] = cells[i] with { Locked = true };
                else if (cells[i].Tile != CMUDeepHackTile.Empty)
                    cells[i] = cells[i] with { Rotation = (byte) rng.Next(4) };
            }
        } while (Solved(cells, width, height, sourceRow, coreRow));

        return new List<CMUDeepHackCell>(cells);
    }

    /// <summary>A path of cell indices from the source to the core that never visits a cell twice.</summary>
    private static List<int> Route(System.Random rng, int width, int height, int sourceRow, int coreRow)
    {
        while (true)
        {
            var path = new List<int> { sourceRow * width };
            var seen = new HashSet<int>(path);
            var x = 0;
            var y = sourceRow;
            for (var steps = 0; steps < width * height * 2; steps++)
            {
                if (x == width - 1 && y == coreRow)
                    return path;

                var options = new List<(int X, int Y)>();
                if (x < width - 1)
                {
                    options.Add((x + 1, y));
                    options.Add((x + 1, y));
                }

                if (y > 0)
                    options.Add((x, y - 1));
                if (y < height - 1)
                    options.Add((x, y + 1));

                // On the last column only move toward the core.
                if (x == width - 1)
                {
                    options.Clear();
                    options.Add((x, y + Math.Sign(coreRow - y)));
                }

                options.RemoveAll(o => seen.Contains(o.Y * width + o.X));
                if (options.Count == 0)
                    break;

                (x, y) = options[rng.Next(options.Count)];
                path.Add(y * width + x);
                seen.Add(y * width + x);
            }
        }
    }

    private static int Toward(int from, int to, int width)
    {
        var dx = to % width - from % width;
        var dy = to / width - from / width;
        return (dx, dy) switch
        {
            (1, 0) => East,
            (-1, 0) => West,
            (0, 1) => South,
            _ => North,
        };
    }
}
