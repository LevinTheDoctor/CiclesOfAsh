using CirclesOfAsh.Companions;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Tutorial;

/// <summary>
/// Führt durch die ersten Minuten: ein Schritt nach dem anderen, jeder wartet auf eine Handlung
/// des Spielers. Es gibt keine Zeitbegrenzung und keinen Zwang – wer weiterläuft, statt zu üben,
/// wird nicht aufgehalten, der Schritt bleibt einfach stehen.
///
/// Die Auslöser sind dieselben Ereignisse, mit denen schon die Begleitseelen zum Sprechen gebracht
/// werden (<see cref="CompanionChatter"/>): <see cref="DungeonWorld.Say"/> meldet beide Wege. Damit
/// braucht das Tutorial keinen Blick in den Spielerzustand und keine eigenen Sonderabfragen.
///
/// Die Schritte stehen in <c>Content/Data/tutorial.json</c>. Reihenfolge, Texte und Auslöser sind
/// damit reine Daten; der Code kennt nur die Auslösernamen.
/// </summary>
public sealed class TutorialDirector
{
    /// <summary>Solange der Spieler nichts tut, wandert die Aufforderung nicht weiter.</summary>
    private const float RepeatSeconds = 14f;

    private readonly GameContext _context;
    private readonly IReadOnlyList<TutorialStepDefinition> _steps;
    private int _index;
    private float _stepAge;
    private float _holdTimer;
    private bool _triggered;
    private bool _spoken;

    public TutorialDirector(GameContext context)
    {
        _context = context;
        _steps = context.Definitions.Tutorial.All.ToList();
    }

    public bool IsFinished => _index >= _steps.Count;
    private TutorialStepDefinition? Current => IsFinished ? null : _steps[_index];

    /// <summary>Zeile für das HUD. Leer, wenn nichts mehr zu zeigen ist.</summary>
    public string Hint => Current is { } step
        ? $"{step.Text}   ({_context.Input.Prompt(GameAction.Randomize)} überspringen)"
        : "";

    /// <summary>Der Spieler bricht ab. Das Tutorial meldet sich in diesem Lauf nicht mehr.</summary>
    public void Skip(DungeonWorld world)
    {
        if (IsFinished) return;
        _index = _steps.Count;
        world.Announce("Tutorial übersprungen.");
        _context.Audio.Play("lever", 0.4f, -0.3f);
        Finish();
    }

    /// <summary>
    /// Meldung eines Ereignisses aus dem Spiel. Passt sie zum aktuellen Schritt, ist er bestanden
    /// – bei Schritten mit <c>hold</c> erst, wenn der Zustand lange genug gehalten wurde.
    /// </summary>
    public void OnEvent(string triggerId)
    {
        if (Current is not { } step) return;
        if (!step.Trigger.Equals(triggerId, StringComparison.OrdinalIgnoreCase)) return;
        _triggered = true;
    }

    public void Update(DungeonWorld world, float deltaSeconds)
    {
        if (Current is not { } step) return;
        _stepAge += deltaSeconds;

        // Den Text einmal als Sprechblase sagen, danach nur noch in der HUD-Zeile stehenlassen.
        // Wiederholt wird er erst, wenn der Spieler lange nicht weiterkommt.
        if (!_spoken || _stepAge > RepeatSeconds)
        {
            Speak(world, step.Text);
            _spoken = true;
            if (_stepAge > RepeatSeconds) _stepAge = 0f;
        }

        // Bewegung prüft der Regisseur selbst – dafür gibt es kein Ereignis, und ein einzelner
        // Tastendruck wäre zu wenig: Der Spieler soll wirklich ein Stück gelaufen sein.
        if (step.Trigger.Equals("move", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(world.Player.Velocity.X) > 20f) _triggered = true;

        bool holding = _triggered && step.Hold > 0f;
        _holdTimer = holding ? _holdTimer + deltaSeconds : 0f;
        if (holding && _holdTimer < step.Hold) { _triggered = false; return; }   // Zustand muss gehalten werden

        bool done = step.Trigger.Equals("wait", StringComparison.OrdinalIgnoreCase)
            ? _stepAge >= step.Wait
            : _triggered;
        if (!done) return;

        Advance(world);
    }

    private void Advance(DungeonWorld world)
    {
        _index++;
        _stepAge = 0f;
        _holdTimer = 0f;
        _triggered = false;
        _spoken = false;
        _context.Audio.Play("pickup", 0.35f, 0.5f);
        if (!IsFinished) return;

        world.Announce("Du kannst alles, was du brauchst.");
        Finish();
    }

    /// <summary>
    /// Merkt sich, dass der Spieler es gesehen hat – beim nächsten Lauf startet das Tutorial
    /// nicht erneut. Über die Optionen lässt es sich jederzeit wieder einschalten.
    /// </summary>
    private void Finish()
    {
        _context.Settings.Tutorial = false;
        _context.SaveSettings();
    }

    /// <summary>
    /// Spricht über eine Begleitseele, wenn eine dabei ist – sonst erscheint die Blase über dem
    /// Spieler selbst. Ohne diesen Rückfall bliebe das Tutorial stumm, wenn jemand
    /// "Allein hinabsteigen" gewählt hat.
    /// </summary>
    private static void Speak(DungeonWorld world, string text)
    {
        IReadOnlyList<Companion> companions = world.Companions;
        if (companions.Count > 0) world.Chatter.Show(text, companions[0]);
        else world.Chatter.Show(text, world.Player);
    }
}
