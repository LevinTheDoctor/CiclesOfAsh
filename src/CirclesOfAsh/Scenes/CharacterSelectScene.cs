using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Gestaltenauswahl: alle gespeicherten Charaktere mit Vorschau und Werdegang. Von hier geht es ins
/// Spiel, in den Editor (neue Gestalt, neuer Abstieg, Aussehen ändern) – oder eine Gestalt wird
/// gelöscht. Zwei Schritte wie im Editor (kleiner Zustandsautomat): erst die Liste, dann die
/// Aktionen der gewählten Gestalt.
/// </summary>
public sealed class CharacterSelectScene : SceneBase
{
    private enum Step { List, Actions }

    /// <summary>Höhe beider Panels – darunter bleibt Platz für die Hilfezeile.</summary>
    private const int PanelHeight = 208;
    /// <summary>Vorschau in dreifacher Größe: aus 24x32 Pixeln werden 72x96.</summary>
    private const float PreviewScale = 3f;
    /// <summary>Oberkante der Trennlinie im Detailpanel. Darunter stehen Aktionen oder ein Hinweis.</summary>
    private const int DividerOffset = 124;

    private MenuList _list = new();
    private MenuList _actions = new();
    private Step _step = Step.List;
    private LayeredSprite? _preview;
    /// <summary>Für welche Gestalt die Vorschau gebaut wurde – neu gebaut wird nur bei einem Wechsel.</summary>
    private SavedCharacter? _previewOf;

    public CharacterSelectScene(GameContext context) : base(context) { }

    public override void OnEnter() => BuildList(0);

    private IReadOnlyList<SavedCharacter> Characters => Context.Progression.Characters;

    /// <summary>Die Gestalt unter dem Auswahlbalken – null auf "Neue Gestalt erschaffen" (letzter Eintrag).</summary>
    private SavedCharacter? Selected =>
        _list.SelectedIndex < Characters.Count ? Characters[_list.SelectedIndex] : null;

    // ------------------------------------------------------------------ Menüs
    private void BuildList(int selectedIndex)
    {
        _list = new MenuList();
        foreach (SavedCharacter character in Characters) _list.Add(character.Name, OpenActions);
        // isEnabled: Sind alle Plätze belegt, bleibt der Eintrag sichtbar, lässt sich aber nicht wählen.
        _list.Add(Loc.T("Neue Gestalt erschaffen"), () => Context.Scenes.Replace(new CharacterCreatorScene(Context)),
                  isEnabled: Context.Progression.CanCreateCharacter);
        _list.Select(selectedIndex);
    }

    private void OpenActions()
    {
        if (Selected is not { } character) return;   // Eigenschaftsmuster: nicht null -> in "character" binden
        BuildActions(character);
        _step = Step.Actions;
    }

    private void BuildActions(SavedCharacter character)
    {
        bool hasRun = character.CurrentRun is not null;
        _actions = new MenuList();
        if (hasRun) _actions.Add(Loc.T("Abstieg fortsetzen"), () => Play(character));
        else _actions.Add(Loc.T("Neuen Abstieg beginnen"), () => OpenCreator(CreatorMode.NewRun, character));
        _actions.Add(Loc.T("Aussehen ändern"), () => OpenCreator(CreatorMode.EditLook, character));
        if (hasRun)
        {
            _actions.Add(Loc.T("Lauf aufgeben (zählt als Tod)"), () => Confirm(
                Loc.T("Lauf aufgeben?"),
                Loc.T("{0} verliert Lauf, Klasse und Ausrüstung. Die Gestalt selbst bleibt.", character.Name),
                Loc.T("Aufgeben"),
                () => GiveUp(character)));
        }
        _actions.Add(Loc.T("Gestalt löschen"), () => Confirm(
            Loc.T("Gestalt löschen?"),
            Loc.T("{0} verschwindet samt Lauf für immer. Gläubige, Gaben und Begleitseelen bleiben.", character.Name),
            Loc.T("Löschen"),
            () => Delete(character)));
        _actions.Add(Loc.T("Zurück"), () => _step = Step.List);
    }

    // ------------------------------------------------------------------ Aktionen
    private void Play(SavedCharacter character)
    {
        Context.Progression.SelectCharacter(character);
        Context.Scenes.Replace(new HubScene(Context));
    }

    private void OpenCreator(CreatorMode mode, SavedCharacter character) =>
        Context.Scenes.Replace(new CharacterCreatorScene(Context, mode, character));

    private void GiveUp(SavedCharacter character)
    {
        ProgressionService progression = Context.Progression;
        progression.SelectCharacter(character);   // HandleDeath trifft immer die aktive Gestalt
        Context.Scenes.Replace(new GameOverScene(Context, progression.HandleDeath()));
    }

    private void Delete(SavedCharacter character)
    {
        int index = _list.SelectedIndex;
        Context.Progression.DeleteCharacter(character);
        _step = Step.List;
        BuildList(index);   // Select klemmt den Index, falls der letzte Eintrag verschwunden ist
    }

    private void Confirm(string title, string question, string confirmLabel, Action onConfirm) =>
        Context.Scenes.Push(new ConfirmScene(Context, title, question, confirmLabel, onConfirm));

    // ------------------------------------------------------------------ Eingabe
    public override void Update(float deltaSeconds)
    {
        RefreshPreview();
        _preview?.Update(deltaSeconds);   // "?." ruft nur auf, wenn es eine Vorschau gibt

        InputState input = Context.Input;
        if (_step == Step.Actions)
        {
            if (input.WasPressed(GameAction.Cancel)) _step = Step.List;
            else _actions.Update(input, Context.Audio);
            return;
        }
        if (input.WasPressed(GameAction.Cancel)) Context.Scenes.Replace(new TitleScene(Context));
        else _list.Update(input, Context.Audio);
    }

    /// <summary>Baut die Vorschau neu, sobald eine andere Gestalt unter dem Auswahlbalken liegt.</summary>
    private void RefreshPreview()
    {
        SavedCharacter? character = Selected;
        if (character == _previewOf && (_preview is not null || character is null)) return;
        _previewOf = character;
        _preview = character is null ? null : CreatePreview(character);
        _preview?.Play("idle");
    }

    /// <summary>
    /// So, wie die Gestalt gerade dasteht: Mit Lauf in dessen Klasse und Kleidung (samt Verfall und
    /// Unterwäsche), ohne Lauf in der Startkleidung der zuletzt gespielten Klasse.
    /// </summary>
    private LayeredSprite CreatePreview(SavedCharacter character)
    {
        ProgressionService progression = Context.Progression;
        DefinitionRegistry definitions = Context.Definitions;
        ClassDefinition playerClass = progression.DisplayClassOf(character);
        RunState? run = progression.PeekRun(character);
        string? armor = run is null
            ? EquipmentService.StartingArmorSprite(definitions, playerClass)
            : EquipmentService.ArmorSprite(definitions, run, Context.Assets);
        return CharacterVisuals.Create(Context, playerClass, character.Appearance, armor, run?.Underwear ?? 0);
    }

    // ------------------------------------------------------------------ Darstellung
    public override void Draw(SpriteBatch spriteBatch)
    {
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.6f);
        BitmapFont font = Context.Font;
        Texture2D pixel = Context.Assets.Pixel;
        float centerX = CirclesGame.VirtualWidth / 2f;
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Deine Gestalten"), centerX, 6, Palette.Gold);

        var listPanel = new Rectangle(16, 36, 176, PanelHeight);
        UiDraw.Panel(spriteBatch, pixel, listPanel);
        _list.Draw(spriteBatch, font, listPanel.Center.X, listPanel.Top + 10, maxVisible: 12);
        font.DrawCentered(spriteBatch, Loc.T("{0} von {1} Plätzen belegt", Characters.Count, ProgressionService.MaxCharacters),
                          listPanel.Center.X, listPanel.Bottom - 14, Palette.Ash);

        var detailPanel = new Rectangle(listPanel.Right + 12, 36, CirclesGame.VirtualWidth - listPanel.Right - 28, PanelHeight);
        UiDraw.Panel(spriteBatch, pixel, detailPanel);
        if (Selected is { } character) DrawDetails(spriteBatch, font, pixel, detailPanel, character);
        else
        {
            string hint = Loc.T("Eine neue Gestalt beginnt ohne Lauf. Gläubige, ewige Gaben, Begleitseelen und den Tempel teilt sie mit allen anderen.");
            font.DrawCenteredLines(spriteBatch, font.Wrap(hint, detailPanel.Width - 24), detailPanel.Center.X,
                                   detailPanel.Top + 70, Palette.Bone * 0.85f);
        }

        string help = Loc.T("{0} wählen · {1} zurück", Context.Input.Glyph(GameAction.Confirm), Context.Input.Glyph(GameAction.Cancel));
        font.DrawCentered(spriteBatch, help, centerX, CirclesGame.VirtualHeight - 14, Palette.Ash);
        spriteBatch.End();
    }

    private void DrawDetails(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Rectangle panel, SavedCharacter character)
    {
        // Links die Figur auf ihrem Glutsockel, rechts daneben Name, Lauf und Werdegang.
        var feet = new Vector2(panel.Left + 52, panel.Top + 112);
        InfernoFunnel.DrawEllipse(spriteBatch, pixel, feet, 28f, 6f, Palette.Ember * 0.6f);
        _preview?.Draw(spriteBatch, feet, false, Color.White, new Vector2(PreviewScale));

        var position = new Vector2(panel.Left + 104, panel.Top + 10);
        font.DrawShadowed(spriteBatch, character.Name, position, Palette.Faith);
        position.Y += 14;
        foreach ((string text, Color color) in DescribeRun(character))
        {
            font.DrawShadowed(spriteBatch, text, position, color);
            position.Y += 12;
        }

        position.Y += 6;
        string deepest = character.DeepestCircle > 0 ? character.DeepestCircle.ToString() : "–";
        string[] record =
        {
            Loc.T("Abstiege: {0}", character.Runs),
            Loc.T("Tode: {0}", character.Deaths),
            Loc.T("Tiefster Kreis: {0}", deepest),
            Loc.T("Arena-Siege: {0}", character.ArenaWins),
        };
        foreach (string line in record)
        {
            font.DrawShadowed(spriteBatch, line, position, Palette.Soul * 0.9f);
            position.Y += 12;
        }

        int dividerY = panel.Top + DividerOffset;
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(panel.Left + 8, dividerY, panel.Width - 16, 1), Palette.Gold * 0.4f);
        if (_step == Step.Actions)
        {
            _actions.Draw(spriteBatch, font, panel.Center.X, dividerY + 8);
            return;
        }
        string hint = Loc.T("Gläubige, ewige Gaben und Begleitseelen gehören allen Gestalten gemeinsam.");
        font.DrawCenteredLines(spriteBatch, font.Wrap(hint, panel.Width - 24), panel.Center.X, dividerY + 12, Palette.Ash);
    }

    /// <summary>Zwei Zeilen über den Lauf: Klasse und Stufe, dann wo er gerade steht.</summary>
    private IEnumerable<(string Text, Color Color)> DescribeRun(SavedCharacter character)
    {
        // "yield return" = Iterator: liefert die Zeilen einzeln, ohne eine Liste anzulegen.
        if (character.CurrentRun is not { } run)
        {
            yield return (Loc.T("Kein laufender Abstieg"), Palette.Ash);
            if (character.LastClassId.Length > 0)
                yield return (Loc.T("Zuletzt: {0}", Context.Progression.DisplayClassOf(character).Name), Palette.Ash);
            yield break;
        }

        yield return (Loc.T("{0} · Stufe {1}", Context.Progression.DisplayClassOf(character).Name, run.Level), Palette.Bone);
        string circleName = Context.Definitions.Worlds.TryGet(run.WorldId, out WorldDefinition? world)
                            && run.CircleIndex < world.Circles.Count
            ? world.Circles[run.CircleIndex].Name.Translated
            : "";
        yield return (Loc.T("{0}. Kreis: {1} · Verlies {2}", run.CircleIndex + 1, circleName, run.DungeonIndex + 1),
                      Palette.Bone * 0.85f);
    }
}
