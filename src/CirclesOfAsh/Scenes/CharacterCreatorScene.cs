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
    /// <summary>
    /// Die Deklarationsreihenfolge IST die Anzeigereihenfolge (siehe <c>_rows</c>). Erst wird die
    /// Figur festgelegt – Geschlecht, Statur –, dann die Klasse, dann die Feinheiten.
    /// <c>Continue</c> muss letzter Eintrag bleiben: <c>DrawOptions</c> bricht dort ab.
    /// </summary>
    private enum Row { Name, Gender, Body, Class, Skin, Hair, HairColor, Makeup, MakeupColor, Wings, Accent, Continue }

    private const int MaxNameLength = 14;
    /// <summary>Höhe beider Panels. Darunter bleibt Platz für die Hilfezeile bei y = 256.</summary>
    private const int PanelHeight = 208;
    /// <summary>
    /// Fest reservierter Block am Panelboden: Trennlinie, Klassenbeschreibung (bis drei Zeilen)
    /// und die Werte-Zeile. Die Auswahlzeilen bekommen genau den Rest darüber.
    /// </summary>
    private const int InfoBlockHeight = 56;
    private static readonly string[] RandomNames =
    {
        "Aurel", "Beatrix", "Cassian", "Dante", "Elysia", "Fenris", "Galia", "Ilian", "Lucan", "Mira", "Orin", "Seraphine", "Vigil",
    };

    private readonly List<ClassDefinition> _classes;
    private readonly AppearanceDefinition _options;
    private readonly MenuList _companionMenu = new();
    private readonly Random _random = new();
    /// <summary>
    /// Nur die Zeilen, für die es auch Auswahlmöglichkeiten gibt. Körpertypen, Make-up und Flügel
    /// stehen in appearance.json; fehlen sie noch, erscheint die Zeile gar nicht erst, statt eine
    /// leere Auswahl anzubieten.
    /// </summary>
    private readonly Row[] _rows;
    private readonly BodyTypeCatalog _bodies;
    private Step _step = Step.Look;
    private int _rowIndex;
    private string _name;
    private int _classIndex, _skin, _hair, _hairColor, _accent;
    private int _makeup, _makeupColor, _wings;
    /// <summary>
    /// Zwei Achsen statt eines Körpertyp-Index. Der gespeicherte Index entsteht erst beim
    /// Auslesen (<see cref="Look"/>) – siehe <see cref="BodyTypeCatalog"/>.
    /// </summary>
    private int _gender, _build;
    private LayeredSprite _preview = null!;
    private float _time;

    public CharacterCreatorScene(GameContext context) : base(context)
    {
        _classes = context.Definitions.Classes.All.ToList();
        _options = context.Definitions.Appearance;
        _bodies = new BodyTypeCatalog(_options.BodyTypes);
        _name = RandomNames[_random.Next(RandomNames.Length)];
        _rows = Enum.GetValues<Row>().Where(HasOptions).ToArray();
        RebuildPreview();
    }

    /// <summary>Gibt es für diese Zeile überhaupt etwas zu wählen?</summary>
    private bool HasOptions(Row row) => row switch
    {
        // Lässt sich aus den Daten kein Geschlecht ablesen (fremde IDs aus einem Mod), fällt die
        // Zeile weg und "Statur" listet wieder alle Körpertypen.
        Row.Gender => _bodies.HasGenders,
        Row.Body => _options.BodyTypes.Count > 0,
        Row.Makeup => _options.MakeupStyles.Count > 0,
        Row.MakeupColor => _options.MakeupStyles.Count > 0 && _options.MakeupColors.Count > 0,
        Row.Wings => _options.WingStyles.Count > 0,
        _ => true,
    };

    private Row CurrentRow => _rows[_rowIndex];
    private ClassDefinition SelectedClass => _classes[_classIndex];
    private CharacterAppearance Look => new(_name.Trim().Length > 0 ? _name.Trim() : "Namenloser",
        _skin, _hair, _hairColor, _accent, _bodies.ToBodyType(_gender, _build), _makeup, _makeupColor, _wings);

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
        bool showedBody = ShowsBody;
        _rowIndex = CharacterVisuals.Wrap(_rowIndex + step, _rows.Length);
        Context.Audio.Play("pickup", 0.2f, 0.4f);
        // Nur neu bauen, wenn sich dadurch wirklich etwas ändert (Rüstung an/aus).
        if (showedBody != ShowsBody) RebuildPreview();
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
            // Geschlecht wechseln behält die Statur: von "Weiblich · Trainiert" kommt man auf
            // "Männlich · Trainiert", nicht auf einen beliebigen Körper.
            case Row.Gender: _gender = CharacterVisuals.Wrap(_gender + step, Math.Max(1, _bodies.Genders.Length)); break;
            case Row.Body when _bodies.HasGenders: _build = CharacterVisuals.Wrap(_build + step, _bodies.Builds.Length); break;
            case Row.Body: _build = CharacterVisuals.Wrap(_build + step, _options.BodyTypes.Count); break;
            case Row.Makeup: _makeup = CharacterVisuals.Wrap(_makeup + step, _options.MakeupStyles.Count); break;
            case Row.MakeupColor: _makeupColor = CharacterVisuals.Wrap(_makeupColor + step, _options.MakeupColors.Count); break;
            case Row.Wings: _wings = CharacterVisuals.Wrap(_wings + step, _options.WingStyles.Count); break;
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
        _gender = _random.Next(Math.Max(1, _bodies.Genders.Length));
        _build = _random.Next(Math.Max(1, _bodies.HasGenders ? _bodies.Builds.Length : _options.BodyTypes.Count));
        _makeup = _random.Next(Math.Max(1, _options.MakeupStyles.Count));
        _makeupColor = _random.Next(Math.Max(1, _options.MakeupColors.Count));
        _wings = _random.Next(Math.Max(1, _options.WingStyles.Count));
        Context.Audio.Play("unseal", 0.3f, 0.6f);
        RebuildPreview();
    }

    /// <summary>
    /// Baut die Vorschau neu – mit der Startrüstung der gewählten Klasse, denn genau so betrittst
    /// du das Verlies. Ausgenommen sind die Zeilen Geschlecht und Statur: Dort geht es um die
    /// Figur, und die ist unter dem Panzer nicht zu beurteilen. Die Vorschau zeigt also immer das,
    /// was du gerade bearbeitest – ohne zusätzliche Taste oder Zeile.
    /// </summary>
    private void RebuildPreview() =>
        _preview = CharacterVisuals.Create(Context, SelectedClass, Look, ShowsBody ? null : StartingArmorSprite);

    /// <summary>Auf diesen Zeilen zählt der nackte Körper, nicht die Rüstung darüber.</summary>
    private bool ShowsBody => CurrentRow is Row.Gender or Row.Body;

    /// <summary>Sprite der Rüstung, mit der diese Klasse startet – oder null.</summary>
    private string? StartingArmorSprite =>
        Context.Definitions.Items.TryGet(SelectedClass.StartingArmor, out ItemDefinition? armor)
        && armor.Sprite.Length > 0
            ? armor.Sprite
            : null;

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
        // Beide Panels reichen bis 244 – erst bei 256 steht die Hilfezeile. Die Höhe wurde
        // gebraucht, weil der Editor inzwischen elf Zeilen hat statt sieben.
        var altar = new Rectangle(24, 36, 150, PanelHeight);
        UiDraw.Panel(spriteBatch, pixel, altar);
        float previewBottom = altar.Bottom - 26;   // Standfläche der Figur, nicht mehr fest auf 190
        InfernoFunnel.DrawEllipse(spriteBatch, pixel, new Vector2(altar.Center.X, previewBottom), 40f, 8f, Palette.Ember * 0.6f);
        _preview.Draw(spriteBatch, new Vector2(altar.Center.X, previewBottom), false, Color.White, new Vector2(5f));
        font.DrawCentered(spriteBatch, Look.Name, altar.Center.X, altar.Top + 6, Palette.Faith);
        font.DrawCentered(spriteBatch, SelectedClass.Name, altar.Center.X, altar.Bottom - 14, Palette.Bone);

        var panel = new Rectangle(186, 36, CirclesGame.VirtualWidth - 206, PanelHeight);
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
                : ShowsBody
                    ? $"Hoch/Runter Zeile · Links/Rechts ändern · Rüstung ausgeblendet · {cancelGlyph} zurück"
                    : $"Hoch/Runter Zeile · Links/Rechts ändern · {confirmGlyph} weiter · {randomGlyph} Zufall · {cancelGlyph} zurück")
            : $"{confirmGlyph} wählen · {cancelGlyph} zurück";
        font.DrawCentered(spriteBatch, help, centerX, CirclesGame.VirtualHeight - 14, Palette.Ash);
        spriteBatch.End();
    }

    private void DrawOptions(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Rectangle panel)
    {
        // Abstand aus dem verfügbaren Platz ableiten, NICHT fest verdrahten: Bei wenigen Zeilen
        // bleibt es luftig wie früher, bei vielen rückt es zusammen statt in den Infoblock zu
        // laufen. Genau das ist vorher passiert, als vier Zeilen dazukamen.
        float top = panel.Top + 8;
        float available = panel.Bottom - InfoBlockHeight - top;
        float step = MathF.Min(font.LineHeight + 7, available / Math.Max(1, _rows.Length));

        float y = top;
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
                Row.Gender => "Geschlecht",
                Row.Body => _bodies.HasGenders ? "Statur" : "Gestalt",
                Row.Makeup => "Bemalung",
                Row.MakeupColor => "Bemalungsfarbe",
                Row.Wings => "Flügel",
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
                case Row.Gender:
                    font.DrawShadowed(spriteBatch, $"‹ {_bodies.GenderName(_gender)} ›", valuePosition, Palette.Faith);
                    break;
                case Row.Body:
                    string bodyName = _bodies.HasGenders ? _bodies.BuildName(_build) : OptionName(_options.BodyTypes, _build);
                    font.DrawShadowed(spriteBatch, $"‹ {bodyName} ›", valuePosition, Palette.Faith);
                    break;
                case Row.Makeup:
                    font.DrawShadowed(spriteBatch, $"‹ {OptionName(_options.MakeupStyles, _makeup)} ›", valuePosition, Palette.Faith);
                    break;
                case Row.Wings:
                    font.DrawShadowed(spriteBatch, $"‹ {OptionName(_options.WingStyles, _wings)} ›", valuePosition, Palette.Faith);
                    break;
                default:
                    DrawSwatches(spriteBatch, pixel, valuePosition, row);
                    break;
            }
            y += step;
        }

        // Klassenbeschreibung + Werte unten im Panel
        float infoTop = panel.Bottom - InfoBlockHeight + 6;
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(panel.Left + 8, (int)infoTop - 4, panel.Width - 16, 1), Palette.Gold * 0.4f);
        font.DrawShadowed(spriteBatch, font.Wrap(SelectedClass.Description, panel.Width - 20), new Vector2(panel.Left + 10, infoTop), Palette.Bone * 0.8f);
        Dictionary<StatType, float> stats = StatSheet.ParseAll(SelectedClass.BaseStats);
        string statLine = $"Leben {Value(stats, StatType.MaxHealth)} · Mana {Value(stats, StatType.MaxMana)} · Rüstung {Value(stats, StatType.Armor)} · Tempo {Value(stats, StatType.MoveSpeed)}";
        font.DrawShadowed(spriteBatch, statLine, new Vector2(panel.Left + 10, panel.Bottom - 14), Palette.Soul);
    }

    private static string OptionName(IReadOnlyList<AppearanceOptionDefinition> options, int index) =>
        options.Count == 0 ? "-" : options[CharacterVisuals.Wrap(index, options.Count)].Name;

    /// <summary>Farbfelder statt Text: die gewählte Farbe ist größer und golden umrandet.</summary>
    private void DrawSwatches(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, Row row)
    {
        (List<string> colors, int selected) = row switch
        {
            Row.Skin => (_options.SkinTones, _skin),
            Row.HairColor => (_options.HairColors, _hairColor),
            Row.MakeupColor => (_options.MakeupColors, _makeupColor),
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
