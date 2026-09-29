using CirclesOfAsh.World;

namespace CirclesOfAsh.Puzzles;

/// <summary>
/// Der Lichtstrahl des Spiegelrätsels – ohne jeden Bezug zur laufenden Welt.
///
/// Herausgelöst aus <see cref="MirrorPuzzle"/>, damit der Seed-Sweep prüfen kann, ob eine erzeugte
/// Spiegelstellung überhaupt lösbar ist: Er braucht dafür keine Spielfigur, keine Sprites und
/// keinen Ton, nur die Kachelkarte und die Spiegel. Vorher liess sich die Geometrie erst im Spiel
/// beurteilen, und der Kommentar im Generator erzählt, wie das ausgeht – eine Fassung löste sich
/// von selbst, weil der Strahl unten einfach durchlief.
///
/// Die Stellung eines Spiegels bestimmt, was mit dem Strahl passiert:
///   "/" (1) und "\" (3) lenken ihn um 90 Grad um,
///   "|" (0) lässt einen senkrechten Strahl durch und blockt einen waagerechten,
///   "–" (2) umgekehrt.
/// </summary>
public static class BeamTracer
{
    public const int MaxSteps = 200;

    /// <summary>Der Leuchter strahlt nach rechts.</summary>
    public static readonly Point StartDirection = new(1, 0);

    /// <param name="mirrors">Stellung je Spiegelkachel (0 = "|", 1 = "/", 2 = "–", 3 = "\").</param>
    /// <param name="path">Wird mit den durchlaufenen Kacheln gefüllt (für die Darstellung).</param>
    /// <param name="touched">Wird mit den Spiegeln gefüllt, die der Strahl berührt hat.</param>
    /// <returns>true, wenn der Strahl das Ziel erreicht.</returns>
    public static bool Trace(TileMap map, Rectangle bounds, Point source, Point target,
                             IReadOnlyDictionary<Point, int> mirrors,
                             List<Point>? path = null, List<Point>? touched = null)
    {
        path?.Clear();
        touched?.Clear();

        Point tile = source;
        Point direction = StartDirection;
        path?.Add(tile);

        for (int step = 0; step < MaxSteps; step++)
        {
            tile += direction;
            if (!bounds.Contains(tile)) return false;
            if (TileMap.IsBlocking(map[tile.X, tile.Y])) return false;
            path?.Add(tile);
            if (tile == target) return true;

            if (!mirrors.TryGetValue(tile, out int state)) continue;
            touched?.Add(tile);

            bool horizontal = direction.Y == 0;
            switch (state & 3)
            {
                case 0 when horizontal:   // "|" steht quer zum waagerechten Strahl
                case 2 when !horizontal:  // "–" steht quer zum senkrechten Strahl
                    return false;
                case 0:
                case 2:
                    continue;             // flach in Strahlrichtung -> der Strahl läuft weiter
                case 1:                   // "/": rechts<->oben, links<->unten
                    direction = new Point(-direction.Y, -direction.X);
                    break;
                default:                  // "\": rechts<->unten, links<->oben
                    direction = new Point(direction.Y, direction.X);
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// Probiert ALLE Stellungen der drehbaren Spiegel durch (4 je Spiegel) und zählt, wie viele
    /// davon das Ziel treffen. Damit lässt sich beides messen, was an einem Spiegelrätsel schiefgehen
    /// kann: keine Lösung (0 Treffer) und eine, die sich von selbst löst.
    /// </summary>
    /// <param name="turnable">Kacheln der drehbaren Spiegel, in fester Reihenfolge.</param>
    /// <param name="fixedMirrors">Kacheln und Stellungen der festen Spiegel.</param>
    /// <returns>Wie viele der 4^n Stellungen lösen, und die Stellung, in der das Rätsel beginnt.</returns>
    public static (int Solutions, int Total, bool StartsSolved) CountSolutions(
        TileMap map, Rectangle bounds, Point source, Point target,
        IReadOnlyList<Point> turnable, IReadOnlyDictionary<Point, int> fixedMirrors,
        IReadOnlyList<int> startStates)
    {
        var mirrors = new Dictionary<Point, int>(fixedMirrors);
        int total = 1;
        for (int index = 0; index < turnable.Count; index++) total *= 4;

        int solutions = 0;
        for (int combination = 0; combination < total; combination++)
        {
            int rest = combination;
            for (int index = 0; index < turnable.Count; index++)
            {
                mirrors[turnable[index]] = rest & 3;
                rest >>= 2;
            }
            if (Trace(map, bounds, source, target, mirrors)) solutions++;
        }

        for (int index = 0; index < turnable.Count; index++)
            mirrors[turnable[index]] = index < startStates.Count ? startStates[index] & 3 : 0;
        bool startsSolved = Trace(map, bounds, source, target, mirrors);
        return (solutions, total, startsSolved);
    }
}
