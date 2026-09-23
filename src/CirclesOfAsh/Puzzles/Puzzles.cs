using CirclesOfAsh.Core;
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
        // Falsche Rune: alle rot aufleuchten lassen, dann zurücksetzen
        foreach (Prop pillar in world.PropsWithTag("rune")) pillar.Behavior.OnSignal(pillar, world, "error");
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

    public void Initialize(DungeonWorld world)
    {
        _total = world.PropsWithTag("brazier").Count();
        _timeLimit = world.Context.Definitions.Balance.BrazierTimeLimit;
    }

    public void OnPropActivated(Prop prop, DungeonWorld world)
    {
        if (prop.Tag != "brazier" || IsSolved) return;
        if (_lit == 0) _timeLeft = _timeLimit;   // Zeit läuft ab dem ersten Feuer
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
        if (_timeLeft > 0f) return;
        foreach (Prop brazier in world.PropsWithTag("brazier")) brazier.Behavior.OnSignal(brazier, world, "extinguish");
        _lit = 0;
        world.Announce("Die Flammen erlöschen …");
        world.Context.Audio.Play("error", 0.5f);
    }
}
