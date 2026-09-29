using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Puzzles;

/// <summary>
/// Rätsel, das das Siegeltor vor dem Ausgang verschließt. Props melden Aktionen über
/// <see cref="DungeonWorld.NotifyPuzzle"/>; das Rätsel entscheidet und schickt Signale zurück.
/// </summary>
public interface IPuzzle
{
    bool IsSolved { get; }
    /// <summary>Kurzer Hinweis für das HUD, z. B. "Hebel 1/3".</summary>
    string Hint { get; }
    void Initialize(DungeonWorld world);
    void OnPropActivated(Prop prop, DungeonWorld world);
    void Update(DungeonWorld world, float deltaSeconds) { }
    /// <summary>Eigene Darstellung in der Welt (Lichtstrahl). Läuft im selbstleuchtenden Durchgang.</summary>
    void Draw(SpriteBatch spriteBatch, DungeonWorld world) { }
}

/// <summary>"levers": Alle im Verlies verteilten Hebel umlegen (Erkundungsrätsel).</summary>
public sealed class LeverPuzzle : IPuzzle
{
    private int _total;
    private int _pulled;

    public bool IsSolved => _total > 0 && _pulled >= _total;
    public string Hint => $"Siegeltor: Hebel {_pulled}/{_total}";

    public void Initialize(DungeonWorld world) => _total = world.PropsWithTag("lever").Count();

    public void OnPropActivated(Prop prop, DungeonWorld world)
    {
        if (prop.Tag != "lever" || IsSolved) return;
        _pulled++;
        world.Announce(IsSolved ? "Ein fernes Tor erbebt …" : $"Ein Mechanismus rastet ein ({_pulled}/{_total}).");
        if (IsSolved) world.OpenGate();
    }
}

/// <summary>"rune_order": Runensäulen in der Reihenfolge der Wandtafel berühren. Fehler setzt zurück.</summary>
public sealed class RuneOrderPuzzle : IPuzzle
{
    private IReadOnlyList<int> _order = Array.Empty<int>();
    private int _step;
    private float _errorTimer;

    public bool IsSolved { get; private set; }
    public string Hint => IsSolved ? "" : $"Siegeltor: Runenfolge {_step}/{_order.Count} – suche die Inschrift";

    public void Initialize(DungeonWorld world)
    {
        _order = world.Layout.Puzzle?.Order ?? Array.Empty<int>();
        // Die Inschrift (Wandtafel) bekommt die Lösung als Payload und zeichnet sie
        foreach (Prop mural in world.PropsWithTag("mural")) mural.Payload = _order;
    }

    public void OnPropActivated(Prop prop, DungeonWorld world)
    {
        if (prop.Tag != "rune" || IsSolved || _errorTimer > 0f) return;
        if (_step < _order.Count && prop.Index == _order[_step])
        {
            prop.Behavior.OnSignal(prop, world, "activate");
            _step++;
            if (_step < _order.Count) return;
            IsSolved = true;
            world.Announce("Die Runen leuchten im Einklang.");
            world.OpenGate();
            return;
        }
        // Falsche Rune: alle rot aufleuchten lassen, dann zurücksetzen.
        // Bewusst OHNE die Lösung im Text: Säulen und Inschrift zeigen Symbole, keine Ziffern –
        // eine Zahlenfolge im HUD wäre etwas, das der Spieler nirgends wiedererkennt.
        foreach (Prop pillar in world.PropsWithTag("rune")) pillar.Behavior.OnSignal(pillar, world, "error");
        world.Announce(_step > 0 ? $"Falsche Rune – {_step} Schritte verloren." : "Falsche Rune.");
        world.Context.Audio.Play("error", 0.6f);
        world.ShakeCamera(2f);
        _errorTimer = 0.8f;
        _step = 0;
    }

    public void Update(DungeonWorld world, float deltaSeconds)
    {
        if (_errorTimer <= 0f) return;
        _errorTimer -= deltaSeconds;
        if (_errorTimer > 0f) return;
        foreach (Prop pillar in world.PropsWithTag("rune")) pillar.Behavior.OnSignal(pillar, world, "reset");
    }
}

/// <summary>"braziers": Alle Kohlenbecken innerhalb des Zeitlimits entzünden, sonst erlöschen sie.</summary>
public sealed class BrazierPuzzle : IPuzzle
{
    private int _total;
    private int _lit;
    private float _timeLeft;
    private float _timeLimit;

    public bool IsSolved { get; private set; }
    public string Hint => IsSolved ? "" : _lit == 0
        ? $"Siegeltor: Entzünde {_total} Feuerbecken rasch hintereinander"
        : $"Siegeltor: Feuer {_lit}/{_total} – noch {MathF.Ceiling(_timeLeft)} s";

    /// <summary>Ab wann die Vorwarnung läuft: die letzten Sekunden zählen sichtbar herunter.</summary>
    private const float WarningSeconds = 3f;
    private bool _warned;

    public void Initialize(DungeonWorld world)
    {
        _total = world.PropsWithTag("brazier").Count();
        BalanceDefinition balance = world.Context.Definitions.Balance;
        // Je tiefer der Kreis, desto knapper die Zeit (balance.json, "puzzleScaling").
        _timeLimit = balance.PuzzleScaling.BrazierTimeLimitAt(balance.BrazierTimeLimit, world.Plan.CircleIndex);
    }

    public void OnPropActivated(Prop prop, DungeonWorld world)
    {
        if (prop.Tag != "brazier" || IsSolved) return;
        if (_lit == 0) { _timeLeft = _timeLimit; _warned = false; }   // Zeit läuft ab dem ersten Feuer
        _lit++;
        if (_lit < _total) return;
        IsSolved = true;
        world.Announce("Die Flammen brennen vereint.");
        world.OpenGate();
    }

    public void Update(DungeonWorld world, float deltaSeconds)
    {
        if (IsSolved || _lit == 0) return;
        _timeLeft -= deltaSeconds;
        // Vorwarnung: Vorher liefen die Becken ohne jedes Zeichen aus, und das Erlöschen kam aus
        // dem Nichts. Ein Ton und ein Zucken der brennenden Becken machen die Uhr hörbar.
        if (!_warned && _timeLeft <= WarningSeconds)
        {
            _warned = true;
            foreach (Prop brazier in world.PropsWithTag("brazier"))
                if (brazier.State == 1) brazier.Behavior.OnSignal(brazier, world, "flicker");
            world.Context.Audio.Play("error", 0.3f, 0.6f);
        }
        if (_timeLeft > 0f) return;
        foreach (Prop brazier in world.PropsWithTag("brazier")) brazier.Behavior.OnSignal(brazier, world, "extinguish");
        _lit = 0;
        _warned = false;
        world.Announce("Die Flammen erlöschen …");
        world.Context.Audio.Play("error", 0.5f);
    }
}

/// <summary>
/// "weights": Alle Druckplatten müssen GLEICHZEITIG beschwert sein. Es gibt eine Platte mehr als
/// Schiebeblöcke – auf der letzten muss der Spieler selbst stehen bleiben. Deshalb wird bei jedem
/// Zustandswechsel neu gezählt: Es reicht nicht, die Platten nacheinander zu treffen.
/// </summary>
public sealed class WeightPuzzle : IPuzzle
{
    private DungeonWorld? _world;
    private int _total;

    public bool IsSolved { get; private set; }

    /// <summary>
    /// Zählt bei jedem Blick neu. Ein gemerkter Zähler stand hinterher falsch da: Er wurde nur bei
    /// einem Plattenereignis fortgeschrieben, und wer von einer Platte heruntertrat, ohne eine
    /// andere auszulösen, sah weiter die alte Zahl.
    /// </summary>
    public string Hint => IsSolved ? "" : $"Siegeltor: Platten {PressedCount()}/{_total} gleichzeitig beschwert";

    private int PressedCount() => _world?.PropsWithTag("plate").Count(plate => plate.State == 1) ?? 0;

    public void Initialize(DungeonWorld world)
    {
        _world = world;
        _total = world.PropsWithTag("plate").Count();
    }

    public void OnPropActivated(Prop prop, DungeonWorld world)
    {
        if (prop.Tag != "plate" || IsSolved) return;
        int pressed = PressedCount();
        if (pressed < _total || _total == 0) return;

        IsSolved = true;
        world.Announce("Der Stein senkt sich unter dem Gewicht.");
        world.OpenGate();
    }
}

/// <summary>
/// "mirrors": Einen Lichtstrahl vom Leuchter bis zum Standbild lenken.
///
/// Die Stellung eines Spiegels bestimmt, was mit dem Strahl passiert:
///   "/" und "\" lenken ihn um 90 Grad um,
///   "|" lässt einen senkrechten Strahl durch und blockt einen waagerechten,
///   "–" umgekehrt.
/// Manche Spiegel stehen also bewusst IM Weg und müssen flach gedreht werden, statt zu lenken.
///
/// Der Strahl läuft auf dem Kachelraster – dieselbe Auflösung, in der der Generator die Spiegel
/// setzt. Dadurch trifft er sie exakt, statt an ihnen vorbeizuschrammen.
/// </summary>
public sealed class MirrorPuzzle : IPuzzle
{
    private const int MaxSteps = 200;
    private const string MirrorTag = "mirror";
    private const string FixedTag = "mirror_fixed";
    private static readonly Point[] Directions = { new(1, 0), new(0, -1), new(-1, 0), new(0, 1) };

    private readonly List<Point> _path = new();
    /// <summary>Spiegel je Kachel. Vorher wurde je Schritt die ganze Liste durchsucht.</summary>
    private readonly Dictionary<Point, Prop> _mirrorsByTile = new();
    private Prop? _source;
    private Prop? _target;
    private bool _dirty = true;
    private int _reached;

    public bool IsSolved { get; private set; }

    /// <summary>
    /// Zeigt, wie viele Spiegel der Strahl schon erreicht hat. Vorher stand hier immer derselbe
    /// Satz – man sah nie, ob eine Drehung etwas gebracht hat.
    /// </summary>
    public string Hint => IsSolved
        ? ""
        : $"Siegeltor: Licht zum Standbild lenken – erreicht: {_reached}/{_mirrorsByTile.Count} Spiegel";

    public void Initialize(DungeonWorld world)
    {
        _source = world.PropsWithTag("beam_source").FirstOrDefault();
        _target = world.PropsWithTag("beam_target").FirstOrDefault();

        // Die festen Spiegel haengen hoch an der Wand und zeigen den gedachten Weg. Ihre Stellung
        // kommt aus dem Generator (PuzzleSpec.Order) – so steht die Geometrie an EINER Stelle.
        IReadOnlyList<int> fixedStates = world.Layout.Puzzle?.Order ?? Array.Empty<int>();
        foreach (Prop mirror in world.PropsWithTag(FixedTag))
        {
            mirror.State = mirror.Index < fixedStates.Count ? fixedStates[mirror.Index] & 3 : 1;
            mirror.CanInteract = false;   // sonst koennte der Spieler den sicheren Weg zerdrehen
            mirror.Behavior.Initialize(mirror, world);
        }
        _dirty = true;
    }

    public void OnPropActivated(Prop prop, DungeonWorld world)
    {
        if (prop.Tag != MirrorTag) return;
        _dirty = true;
    }

    /// <summary>Drehbare und feste Spiegel zusammen – der Strahl unterscheidet sie nicht.</summary>
    private static IEnumerable<Prop> AllMirrors(DungeonWorld world) =>
        world.PropsWithTag(MirrorTag).Concat(world.PropsWithTag(FixedTag));

    public void Update(DungeonWorld world, float deltaSeconds)
    {
        if (!_dirty) return;
        _dirty = false;
        Trace(world);
    }

    private void Trace(DungeonWorld world)
    {
        _path.Clear();
        _reached = 0;
        // Einmal je Durchlauf einsortieren: Die Spiegel bewegen sich nicht, nur ihre Stellung
        // ändert sich. Vorher lief je Kachel eine lineare Suche über alle Spiegel.
        _mirrorsByTile.Clear();
        foreach (Prop mirror in AllMirrors(world))
        {
            mirror.Behavior.OnSignal(mirror, world, "dark");
            _mirrorsByTile[TileOf(mirror)] = mirror;
        }
        if (_source is null || _target is null) return;

        Rectangle bounds = _source.Room.TileBounds;
        Point tile = TileOf(_source);
        Point targetTile = TileOf(_target);
        Point direction = Directions[0];   // der Leuchter strahlt nach rechts
        _path.Add(tile);

        for (int step = 0; step < MaxSteps; step++)
        {
            tile += direction;
            if (!bounds.Contains(tile)) break;
            if (TileMap.IsBlocking(world.Map[tile.X, tile.Y])) break;
            _path.Add(tile);

            if (tile == targetTile)
            {
                if (IsSolved) return;
                IsSolved = true;
                _target.LightRadius = 70f;
                world.Announce("Das Licht trifft das Standbild.");
                world.Context.Audio.Play("unseal", 0.8f);
                world.OpenGate();
                return;
            }

            if (!_mirrorsByTile.TryGetValue(tile, out Prop? mirror)) continue;

            mirror.Behavior.OnSignal(mirror, world, "lit");
            _reached++;
            bool horizontal = direction.Y == 0;
            switch (mirror.State)
            {
                case 0 when horizontal:   // "|" steht quer zum waagerechten Strahl
                case 2 when !horizontal:  // "–" steht quer zum senkrechten Strahl
                    return;
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
    }

    private static Point TileOf(Prop prop) =>
        new(TileMap.ToTile(prop.Center.X), TileMap.ToTile(prop.Center.Y));

    public void Draw(SpriteBatch spriteBatch, DungeonWorld world)
    {
        if (_path.Count < 2) return;
        Texture2D pixel = world.Context.Assets.Pixel;
        var color = new Color(255, 236, 170) * (IsSolved ? 0.95f : 0.7f);
        const int size = TileMap.TileSize;

        // Je zwei benachbarte Kachelmitten mit einem 2 px dünnen Balken verbinden.
        for (int i = 1; i < _path.Count; i++)
        {
            Point a = _path[i - 1];
            Point b = _path[i];
            int x = Math.Min(a.X, b.X) * size + size / 2 - 1;
            int y = Math.Min(a.Y, b.Y) * size + size / 2 - 1;
            int width = a.Y == b.Y ? size + 2 : 2;
            int height = a.Y == b.Y ? 2 : size + 2;
            spriteBatch.Draw(pixel, new Rectangle(x, y, width, height), color);
        }
    }
}
