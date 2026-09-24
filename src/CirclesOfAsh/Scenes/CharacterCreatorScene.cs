using CirclesOfAsh.Assets;
using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;
using Microsoft.Xna.Framework.Input;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Charakter-Editor: Name, Klasse (die früheren Charaktere), Hautton, Frisur, Haar- und Akzentfarbe.
/// Danach Auswahl der Begleitseele. Einfacher Zustandsautomat mit zwei Schritten.
/// </summary>
public sealed class CharacterCreatorScene : SceneBase
{
    private enum Step { Look, Companion }
    private enum Row { Name, Class, Skin, Hair, HairColor, Accent, Continue }

    private const int MaxNameLength = 14;
    private static readonly string[] RandomNames =
    {
        "Aurel", "Beatrix", "Cassian", "Dante", "Elysia", "Fenris", "Galia", "Ilian", "Lucan", "Mira", "Orin", "Seraphine", "Vigil",
    };

    private readonly List<ClassDefinition> _classes;
    private readonly AppearanceDefinition _options;
    private readonly MenuList _companionMenu = new();
    private readonly Random _random = new();
    private readonly Row[] _rows = Enum.GetValues<Row>();   // alle Enum-Werte als Array
    private Step _step = Step.Look;
    private int _rowIndex;
    private string _name;
    private int _classIndex, _skin, _hair, _hairColor, _accent;
    private LayeredSprite _preview = null!;
    private float _time;

    public CharacterCreatorScene(GameContext context) : base(context)
    {
        _classes = context.Definitions.Classes.All.ToList();
        _options = context.Definitions.Appearance;
        _name = RandomNames[_random.Next(RandomNames.Length)];
        RebuildPreview();
    }

    private Row CurrentRow => _rows[_rowIndex];
    private ClassDefinition SelectedClass => _classes[_classIndex];
    private CharacterAppearance Look => new(_name.Trim().Length > 0 ? _name.Trim() : "Namenloser", _skin, _hair, _hairColor, _accent);

    public override void OnEnter()
    {
        foreach (CompanionDefinition companion in Context.Progression.AvailableCompanions)
        {
            string companionId = companion.Id;   // lokale Kopie für das Lambda (Closure)
            _companionMenu.Add(companion.Name, () => StartRun(new[] { companionId }), hint: companion.Description);
        }
        _companionMenu.Add("Allein hinabsteigen", () => StartRun(Array.Empty<string>()), hint: "Kein Begleiter.");
    }

    // ------------------------------------------------------------------ Eingabe
    public override void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        _preview.Play(_time % 4f < 2f ? "idle" : "run");   // abwechselnd stehen und laufen
        _preview.Update(deltaSeconds);

        InputState input = Context.Input;
        if (_step == Step.Companion)
        {
            if (input.WasPressed(GameAction.Cancel)) _step = Step.Look;
            else _companionMenu.Update(input, Context.Audio);
            return;
        }

        if (input.WasPressed(GameAction.Cancel))
        {
            Context.Scenes.Replace(new TitleScene(Context));
            return;
        }
        if (input.WasPressed(GameAction.Randomize)) Randomize();

        if (CurrentRow == Row.Name) UpdateNameRow(input);
        else UpdateOptionRow(input);
    }

    /// <summary>
    /// Im Namensfeld sind W/A/S/D Buchstaben. Deshalb hier nur Pfeiltasten und Enter zur Navigation
    /// (rohe Tastenabfrage statt GameAction).
    /// </summary>
    private void UpdateNameRow(InputState input)
    {
        foreach (char character in input.TypedText)
        {
            if (character == '\b')
            {
                if (_name.Length > 0) _name = _name[..^1];   // Range-Operator: alles außer dem letzten Zeichen
            }
            else if ((char.IsLetterOrDigit(character) || character is ' ' or '-') && _name.Length < MaxNameLength)
            {
                _name += character;
            }
        }
        if (input.WasKeyPressed(Keys.Down) || input.WasKeyPressed(Keys.Tab) || input.WasPressed(GameAction.Confirm)) MoveRow(1);
        else if (input.WasKeyPressed(Keys.Up)) MoveRow(-1);
    }

    private void UpdateOptionRow(InputState input)
    {
        if (input.WasPressed(GameAction.Down)) MoveRow(1);
        else if (input.WasPressed(GameAction.Up)) MoveRow(-1);

        int change = input.WasPressed(GameAction.Right) ? 1 : input.WasPressed(GameAction.Left) ? -1 : 0;
        if (change != 0) ChangeValue(change);

        if (input.WasPressed(GameAction.Confirm))
        {
            if (CurrentRow == Row.Continue) _step = Step.Companion;
            else MoveRow(1);
        }
    }

    private void MoveRow(int step)
    {
        _rowIndex = CharacterVisuals.Wrap(_rowIndex + step, _rows.Length);
        Context.Audio.Play("pickup", 0.2f, 0.4f);
    }

    private void ChangeValue(int step)
    {
        // switch-Anweisung: jede Zeile verändert ihren eigenen Index, Wrap hält ihn im gültigen Bereich
        switch (CurrentRow)
        {
            case Row.Class: _classIndex = CharacterVisuals.Wrap(_classIndex + step, _classes.Count); break;
            case Row.Skin: _skin = CharacterVisuals.Wrap(_skin + step, _options.SkinTones.Count); break;
            case Row.Hair: _hair = CharacterVisuals.Wrap(_hair + step, _options.HairStyles.Count); break;
            case Row.HairColor: _hairColor = CharacterVisuals.Wrap(_hairColor + step, _options.HairColors.Count); break;
            case Row.Accent: _accent = CharacterVisuals.Wrap(_accent + step, _options.AccentColors.Count); break;
            default: return;
        }
        Context.Audio.Play("pickup", 0.25f, 0.2f);
        RebuildPreview();
    }

    private void Randomize()
    {
        _name = RandomNames[_random.Next(RandomNames.Length)];
        _classIndex = _random.Next(_classes.Count);
        _skin = _random.Next(Math.Max(1, _options.SkinTones.Count));
        _hair = _random.Next(Math.Max(1, _options.HairStyles.Count));
        _hairColor = _random.Next(Math.Max(1, _options.HairColors.Count));
        _accent = _random.Next(Math.Max(1, _options.AccentColors.Count));
        Context.Audio.Play("unseal", 0.3f, 0.6f);
        RebuildPreview();
    }

    private void RebuildPreview() => _preview = CharacterVisuals.Create(Context, SelectedClass, Look);

    private void StartRun(IEnumerable<string> companionIds)
    {
        Context.Progression.StartNewRun(SelectedClass.Id, Look, companionIds);
        Context.Scenes.Replace(new HubScene(Context));
    }

    // ------------------------------------------------------------------ Darstellung
    public override void Draw(SpriteBatch spriteBatch)
    {
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.6f);
        BitmapFont font = Context.Font;
        Texture2D pixel = Context.Assets.Pixel;
        float centerX = CirclesGame.VirtualWidth / 2f;

        Context.TitleFont.DrawCentered(spriteBatch, _step == Step.Look ? "Erschaffe deine Gestalt" : "Wähle eine Begleitseele", centerX, 6, Palette.Gold);

        // Links: große Vorschau auf einem "Altar"
        var altar = new Rectangle(24, 36, 150, 180);
        UiDraw.Panel(spriteBatch, pixel, altar);
        InfernoFunnel.DrawEllipse(spriteBatch, pixel, new Vector2(altar.Center.X, 190), 40f, 8f, Palette.Ember * 0.6f);
        _preview.Draw(spriteBatch, new Vector2(altar.Center.X, 190), false, Color.White, new Vector2(5f));
        font.DrawCentered(spriteBatch, Look.Name, altar.Center.X, altar.Top + 6, Palette.Faith);
        font.DrawCentered(spriteBatch, SelectedClass.Name, altar.Center.X, altar.Bottom - 16, Palette.Bone);

        var panel = new Rectangle(186, 36, CirclesGame.VirtualWidth - 206, 180);
        UiDraw.Panel(spriteBatch, pixel, panel);
        if (_step == Step.Look) DrawOptions(spriteBatch, font, pixel, panel);
        else
        {
            _companionMenu.Draw(spriteBatch, font, panel.Center.X, panel.Top + 10, maxVisible: 8);
            font.DrawCenteredLines(spriteBatch, font.Wrap(_companionMenu.Selected?.Hint ?? "", panel.Width - 20), panel.Center.X, panel.Bottom - 40, Palette.Bone * 0.8f);
            font.DrawCentered(spriteBatch, "Befreie Gefangene in den Kerkern für weitere Seelen.", panel.Center.X, panel.Bottom - 14, Palette.Ash);
        }

        string confirmGlyph = Context.Input.Glyph(GameAction.Confirm),
               cancelGlyph = Context.Input.Glyph(GameAction.Cancel),
               randomGlyph = Context.Input.Glyph(GameAction.Randomize);
        string help = _step == Step.Look
            ? (CurrentRow == Row.Name
                ? $"Tippen: Name · Runter/{confirmGlyph} weiter · {randomGlyph} Zufall · {cancelGlyph} zurück"
                : $"Hoch/Runter Zeile · Links/Rechts ändern · {confirmGlyph} weiter · {randomGlyph} Zufall · {cancelGlyph} zurück")
            : $"{confirmGlyph} wählen · {cancelGlyph} zurück";
        font.DrawCentered(spriteBatch, help, centerX, CirclesGame.VirtualHeight - 14, Palette.Ash);
        spriteBatch.End();
    }

    private void DrawOptions(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Rectangle panel)
    {
        float y = panel.Top + 8;
        foreach (Row row in _rows)
        {
            bool isSelected = row == CurrentRow;
            Color color = isSelected ? Palette.Gold : Palette.Bone * 0.85f;
            string label = row switch
            {
                Row.Name => "Name",
                Row.Class => "Klasse",
                Row.Skin => "Hautton",
                Row.Hair => "Frisur",
                Row.HairColor => "Haarfarbe",
                Row.Accent => "Wappenfarbe",
                _ => "",
            };
            if (row == Row.Continue)
            {
                font.DrawCentered(spriteBatch, isSelected ? "· Weiter zur Begleitseele ·" : "Weiter zur Begleitseele", panel.Center.X, y + 4, color);
                break;
            }

            font.DrawShadowed(spriteBatch, label, new Vector2(panel.Left + 10, y), color);
            var valuePosition = new Vector2(panel.Left + 90, y);
            switch (row)
            {
                case Row.Name:
                    bool cursorVisible = isSelected && _time % 1f < 0.5f;   // blinkender Cursor
                    font.DrawShadowed(spriteBatch, _name + (cursorVisible ? "_" : ""), valuePosition, Palette.Faith);
                    break;
                case Row.Class:
                    font.DrawShadowed(spriteBatch, $"‹ {SelectedClass.Name} ›", valuePosition, Palette.Faith);
                    break;
                case Row.Hair:
                    string hairName = _options.HairStyles.Count == 0 ? "-" : _options.HairStyles[_hair].Name;
                    font.DrawShadowed(spriteBatch, $"‹ {hairName} ›", valuePosition, Palette.Faith);
                    break;
                default:
                    DrawSwatches(spriteBatch, pixel, valuePosition, row);
                    break;
            }
            y += font.LineHeight + 7;
        }

        // Klassenbeschreibung + Werte unten im Panel
        float infoTop = panel.Bottom - 58;
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(panel.Left + 8, (int)infoTop - 4, panel.Width - 16, 1), Palette.Gold * 0.4f);
        font.DrawShadowed(spriteBatch, font.Wrap(SelectedClass.Description, panel.Width - 20), new Vector2(panel.Left + 10, infoTop), Palette.Bone * 0.8f);
        Dictionary<StatType, float> stats = StatSheet.ParseAll(SelectedClass.BaseStats);
        string statLine = $"Leben {Value(stats, StatType.MaxHealth)} · Mana {Value(stats, StatType.MaxMana)} · Rüstung {Value(stats, StatType.Armor)} · Tempo {Value(stats, StatType.MoveSpeed)}";
        font.DrawShadowed(spriteBatch, statLine, new Vector2(panel.Left + 10, panel.Bottom - 14), Palette.Soul);
    }

    /// <summary>Farbfelder statt Text: die gewählte Farbe ist größer und golden umrandet.</summary>
    private void DrawSwatches(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, Row row)
    {
        (List<string> colors, int selected) = row switch
        {
            Row.Skin => (_options.SkinTones, _skin),
            Row.HairColor => (_options.HairColors, _hairColor),
            _ => (_options.AccentColors, _accent),
        };
        for (int index = 0; index < colors.Count; index++)
        {
            bool isChosen = index == selected;
            var swatch = new Rectangle((int)position.X + index * 13, (int)position.Y - (isChosen ? 1 : 0), isChosen ? 11 : 9, isChosen ? 10 : 8);
            UiDraw.Rect(spriteBatch, pixel, swatch, ColorUtil.FromHex(colors[index], Color.White));
            UiDraw.Border(spriteBatch, pixel, swatch, isChosen ? Palette.Gold : Palette.Shadow);
        }
    }

    private static string Value(Dictionary<StatType, float> stats, StatType stat) =>
        (stats.TryGetValue(stat, out float value) ? value : StatSheet.Defaults.GetValueOrDefault(stat)).ToString("0");
}
