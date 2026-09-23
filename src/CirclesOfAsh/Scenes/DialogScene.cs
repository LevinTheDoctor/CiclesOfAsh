using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Dialog-Overlay: zeigt die Zeile eines NPCs mit Typewriter-Effekt, darunter die Auswahl-
/// möglichkeiten. Der Dialog friert die Szene darunter (IsOverlay), läuft also auch im Dungeon.
/// Navigation komplett per GameAction -> Tastatur UND Controller.
/// </summary>
public sealed class DialogScene : SceneBase
{
    private readonly DialogDefinition _dialog;
    private readonly Npc? _npc;
    private DialogLineDefinition _line;
    private readonly List<DialogChoiceDefinition> _choices;
    private int _choiceIndex;
    private float _typewriter;
    private string? _feedback;
    private float _feedbackTimer;

    public DialogScene(GameContext context, DialogDefinition dialog, DialogLineDefinition entry, Npc? npc) : base(context)
    {
        _dialog = dialog;
        _npc = npc;
        _line = entry;
        // Keine expliziten Choices = implizit "Weiter/Verabschieden"
        _choices = entry.Choices.Count > 0
            ? entry.Choices.ToList()
            : new List<DialogChoiceDefinition> { new() { Label = "Weiter", Next = "" } };
        if (entry.Choices.Count == 0 && string.IsNullOrEmpty(entry.Text) == false && dialog.Lines.All(line => line.Id != "farewell"))
        {
            // einfaches Gespräch: weiter beendet den Dialog
        }
        _typewriter = 0f;
    }

    public override bool IsOverlay => true;

    public override void Update(float deltaSeconds)
    {
        InputState input = Context.Input;

        // Typewriter: Zeichen um Zeichen (ca. 60 Zeichen/Sekunde); Taste drücken = sofort vollständig
        float charactersPerSecond = 60f;
        bool isTyping = _typewriter < _line.Text.Length;
        if (isTyping)
        {
            _typewriter += charactersPerSecond * deltaSeconds;
            if (input.WasPressed(GameAction.Confirm) || input.WasPressed(GameAction.Interact))
                _typewriter = _line.Text.Length;   // Rest sofort anzeigen
            return;
        }

        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= deltaSeconds;
            if (input.WasPressed(GameAction.Confirm) || input.WasPressed(GameAction.Cancel) || input.WasPressed(GameAction.Interact))
            {
                _feedbackTimer = 0f;
                Context.Scenes.Pop();
            }
            return;
        }

        // Auswahl navigieren
        if (_choices.Count > 1)
        {
            if (input.WasPressed(GameAction.Down)) { _choiceIndex = (_choiceIndex + 1) % _choices.Count; Context.Audio.Play("pickup", 0.2f, 0.4f); }
            if (input.WasPressed(GameAction.Up)) { _choiceIndex = (_choiceIndex - 1 + _choices.Count) % _choices.Count; Context.Audio.Play("pickup", 0.2f, 0.4f); }
        }

        if (input.WasPressed(GameAction.Cancel))
        {
            Context.Scenes.Pop();
            return;
        }
        if (!input.WasPressed(GameAction.Confirm) && !input.WasPressed(GameAction.Interact)) return;

        DialogChoiceDefinition choice = _choices[_choiceIndex];
        string? feedback = Context.Dialogs.ApplyEffect(choice, _npc);
        if (feedback is not null)
        {
            // Effekt mit Rückmeldung quittieren, dann Dialog schließen
            _feedback = feedback;
            _feedbackTimer = 2.6f;
            return;
        }

        if (choice.Next.Length == 0)
        {
            Context.Scenes.Pop();
            return;
        }
        DialogLineDefinition? next = Context.Dialogs.FindLine(_dialog, choice.Next);
        if (next is null)
        {
            Context.Scenes.Pop();
            return;
        }
        _line = next;
        _choices.Clear();
        _choices.AddRange(next.Choices.Count > 0 ? next.Choices : new List<DialogChoiceDefinition> { new() { Label = "Weiter", Next = "" } });
        _choiceIndex = 0;
        _typewriter = 0f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D pixel = Context.Assets.Pixel;
        BitmapFont font = Context.Font;
        float centerX = CirclesGame.VirtualWidth / 2f;

        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.5f);

        var panel = new Rectangle(30, CirclesGame.VirtualHeight - 108, CirclesGame.VirtualWidth - 60, 96);
        UiDraw.Panel(spriteBatch, pixel, panel);

        string speaker = _npc is not null ? _npc.Definition.Name : "";
        if (speaker.Length > 0) font.DrawShadowed(spriteBatch, speaker, new Vector2(panel.Left + 8, panel.Top + 4), Palette.Gold);

        string fullText = _feedback is not null && _feedbackTimer > 0f ? _feedback : _line.Text;
        int visibleCharacters = Math.Min(fullText.Length, (int)_typewriter);
        string shown = fullText[..visibleCharacters];
        var body = new Rectangle(panel.Left + 6, panel.Top + 16, panel.Width - 12, 46);
        font.DrawShadowed(spriteBatch, font.Wrap(shown, body.Width), new Vector2(body.Left, body.Top), Palette.Bone);

        // Auswahl unten im Panel
        if (_feedback is null || _feedbackTimer <= 0f)
        {
            float y = panel.Bottom - _choices.Count * (font.LineHeight + 2) - 6;
            for (int index = 0; index < _choices.Count; index++)
            {
                bool isSelected = index == _choiceIndex;
                string label = isSelected ? $"> { _choices[index].Label} <" : _choices[index].Label;
                font.DrawCentered(spriteBatch, label, centerX, y + index * (font.LineHeight + 2),
                    isSelected ? Palette.Gold : Palette.Bone * 0.85f);
            }
        }

        bool isTypingNow = _typewriter < _line.Text.Length && !(_feedback is not null);
        string confirm = Context.Input.Glyph(GameAction.Confirm), cancel = Context.Input.Glyph(GameAction.Cancel);
        string hint = isTypingNow ? $"{confirm}: überspringen" : $"{confirm}: wählen · {cancel}: verlassen";
        font.DrawCentered(spriteBatch, hint, centerX, panel.Top - 10, Palette.Ash * 0.9f);
        spriteBatch.End();
    }
}