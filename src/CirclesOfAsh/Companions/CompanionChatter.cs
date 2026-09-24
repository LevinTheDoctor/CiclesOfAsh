using CirclesOfAsh.Assets;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Pets;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Companions;

/// <summary>
/// Sprechende Begleitseelen. Ein Zwischenruf hält das Spiel bewusst NICHT an – im Kampf wäre ein
/// Dialogfenster unerträglich. Stattdessen erscheint eine Sprechblase über der Seele, die von
/// selbst wieder verschwindet.
///
/// Die Auslöser stehen als String-Id in <c>chatter.json</c>; der Code ruft sie an den passenden
/// Stellen auf. Fehlt ein Auslöser in der Datei, passiert schlicht nichts – deshalb darf man dort
/// jederzeit Zeilen ergänzen oder streichen, ohne den Code anzufassen.
/// </summary>
public sealed class CompanionChatter
{
    /// <summary>Auslöser, die der Code kennt. Als Konstanten, damit ein Tippfehler auffällt.</summary>
    public const string RunStart = "run_start";
    public const string RoomCleared = "room_cleared";
    public const string LowHealth = "low_health";
    public const string BossStart = "boss_start";
    public const string BossDefeated = "boss_defeated";
    public const string ArmorShattered = "armor_shattered";
    public const string CollectibleFound = "collectible_found";
    public const string FirstCrouch = "first_crouch";
    public const string FirstBlock = "first_block";
    public const string HubIdle = "hub_idle";

    /// <summary>Kürzester Abstand zwischen zwei Sprechblasen – sonst reden sie sich tot.</summary>
    private const float GlobalCooldown = 7f;
    private const float BubbleSeconds = 4.5f;
    private const int BubbleMaxWidth = 128;

    private readonly GameContext _context;
    private readonly Random _random;
    /// <summary>Spielzeit, ab der ein Auslöser wieder sprechen darf. Fehlt der Schlüssel: sofort.</summary>
    private readonly Dictionary<string, float> _nextAllowed = new(StringComparer.OrdinalIgnoreCase);
    private float _time;
    private float _globalNextAllowed;

    private Companion? _speaker;
    private string _text = "";
    private float _bubbleLeft;

    public CompanionChatter(GameContext context, Random random)
    {
        _context = context;
        _random = random;
    }

    public void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        if (_bubbleLeft > 0f) _bubbleLeft -= deltaSeconds;
    }

    /// <summary>
    /// Versucht, einen Zwischenruf auszulösen. Gibt true zurück, wenn wirklich jemand spricht –
    /// der Aufrufer muss nichts prüfen, alle Sperren liegen hier.
    /// </summary>
    public bool Trigger(string triggerId, IReadOnlyList<Companion> companions)
    {
        if (companions.Count == 0) return false;
        if (_time < _globalNextAllowed) return false;
        if (_nextAllowed.TryGetValue(triggerId, out float allowedAt) && _time < allowedAt) return false;
        if (!_context.Definitions.Chatter.TryGet(triggerId, out ChatterDefinition? chatter)) return false;

        // Sprecher zuerst: die Zeilen dürfen nach Begleiter und Verhalten gefiltert sein.
        Companion speaker = companions[_random.Next(companions.Count)];
        List<ChatterLineDefinition> matching = chatter.Lines.Where(line => Matches(line, speaker)).ToList();
        if (matching.Count == 0)
        {
            // Diese Seele hat zu dem Anlass nichts zu sagen – eine andere vielleicht schon.
            speaker = companions.FirstOrDefault(c => chatter.Lines.Any(line => Matches(line, c))) ?? speaker;
            matching = chatter.Lines.Where(line => Matches(line, speaker)).ToList();
            if (matching.Count == 0) return false;
        }

        string name = PetService.GetPet(_context, speaker.Definition.Id)?.Name ?? speaker.Definition.Name;
        _speaker = speaker;
        _text = matching[_random.Next(matching.Count)].Text.Replace("{name}", name, StringComparison.Ordinal);
        _bubbleLeft = BubbleSeconds;
        _globalNextAllowed = _time + GlobalCooldown;
        // Repeat 0 heißt "nur einmal": eine Sperre, die in diesem Lauf nicht mehr abläuft.
        _nextAllowed[triggerId] = chatter.Repeat > 0f ? _time + chatter.Repeat : float.MaxValue;
        return true;
    }

    private static bool Matches(ChatterLineDefinition line, Companion speaker) =>
        (line.Companion.Length == 0 || line.Companion.Equals(speaker.Definition.Id, StringComparison.OrdinalIgnoreCase)) &&
        (line.Behavior.Length == 0 || line.Behavior.Equals(speaker.Definition.Behavior, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Sprechblase über der Seele, in Weltkoordinaten – sie wandert also mit, während die Seele
    /// dem Spieler folgt. Wird innerhalb desselben SpriteBatch wie die Welt gezeichnet.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, BitmapFont font)
    {
        if (_bubbleLeft <= 0f || _speaker is null) return;

        string wrapped = font.Wrap(_text, BubbleMaxWidth);
        string[] lines = wrapped.Split('\n');
        int width = lines.Max(line => font.MeasureWidth(line)) + 8;
        int height = lines.Length * font.LineHeight + 6;
        var area = new Rectangle(
            (int)MathF.Round(_speaker.Center.X - width / 2f),
            (int)MathF.Round(_speaker.Position.Y - height - 6f),
            width, height);

        // Die letzte halbe Sekunde blendet aus, damit die Blase nicht wegspringt.
        float alpha = Math.Clamp(_bubbleLeft / 0.5f, 0f, 1f);
        UiDraw.Rect(spriteBatch, pixel, area, new Color(12, 10, 18) * (0.85f * alpha));
        UiDraw.Border(spriteBatch, pixel, area, new Color(210, 180, 120) * alpha);
        for (int i = 0; i < lines.Length; i++)
            font.Draw(spriteBatch, lines[i], new Vector2(area.X + 4, area.Y + 3 + i * font.LineHeight), Color.White * alpha);
    }
}
