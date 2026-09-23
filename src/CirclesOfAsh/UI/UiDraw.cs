using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;

namespace CirclesOfAsh.UI;

/// <summary>Kleine Zeichenhelfer für die Oberfläche (Panels, Balken). Alles basiert auf einem 1x1-Pixel.</summary>
public static class UiDraw
{
    /// <summary>Einheitlicher SpriteBatch-Start für UI: nicht-vormultipliziertes Alpha + pixelscharf.</summary>
    public static void Begin(SpriteBatch spriteBatch) =>
        spriteBatch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);

    public static void Rect(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area, Color color) =>
        spriteBatch.Draw(pixel, area, color);

    /// <summary>Gotisches Panel: dunkle Füllung, goldener Rahmen, innere Schattenlinie.</summary>
    public static void Panel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area)
    {
        Rect(spriteBatch, pixel, area, Palette.PanelFill);
        Border(spriteBatch, pixel, area, Palette.Gold * 0.8f);
        Rectangle inner = area;
        inner.Inflate(-2, -2);   // Inflate mit negativen Werten verkleinert das Rechteck
        Border(spriteBatch, pixel, inner, Palette.Shadow);
    }

    public static void Border(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area, Color color)
    {
        Rect(spriteBatch, pixel, new Rectangle(area.Left, area.Top, area.Width, 1), color);
        Rect(spriteBatch, pixel, new Rectangle(area.Left, area.Bottom - 1, area.Width, 1), color);
        Rect(spriteBatch, pixel, new Rectangle(area.Left, area.Top, 1, area.Height), color);
        Rect(spriteBatch, pixel, new Rectangle(area.Right - 1, area.Top, 1, area.Height), color);
    }

    public static void Bar(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area, float ratio, Color fill)
    {
        Rect(spriteBatch, pixel, area, Color.Black * 0.7f);
        int width = (int)((area.Width - 2) * Math.Clamp(ratio, 0f, 1f));
        Rect(spriteBatch, pixel, new Rectangle(area.Left + 1, area.Top + 1, width, area.Height - 2), fill);
        Border(spriteBatch, pixel, area, Palette.Ash * 0.8f);
    }

    /// <summary>Menü-Hintergrund. backgroundId erlaubt kreisabhängige Hintergründe (tiefer = röter/dunkler).</summary>
    public static void Backdrop(SpriteBatch spriteBatch, GameContext context, float darkness = 0.45f, string backgroundId = "background.limbo")
    {
        spriteBatch.Draw(context.Assets.GetTexture(backgroundId), Vector2.Zero, Color.White);
        spriteBatch.Draw(context.Assets.GetTexture("background.mid"), Vector2.Zero, Color.White);
        Rect(spriteBatch, context.Assets.Pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * darkness);
    }
}

/// <summary>Senkrechtes Auswahlmenü (Hoch/Runter + Bestätigen). Wiederverwendet in allen Menüszenen.</summary>
public sealed class MenuList
{
    // Record als Menüeintrag: Text + Aktion (Action = Methode ohne Parameter/Rückgabe) + optional deaktiviert
    public sealed record Entry(string Label, Action OnSelect, bool IsEnabled = true, string? Hint = null);

    private readonly List<Entry> _entries = new();
    /// <summary>
    /// Bildschirmrechteck je Eintrag, von Draw gefüllt und von Update ausgewertet.
    /// Immediate-Mode-Muster: Gezeichnet wird vor dem nächsten Update, die Rechtecke sind also
    /// aktuell. So braucht die Maus kein eigenes Layout-Wissen.
    /// </summary>
    private readonly Dictionary<int, Rectangle> _hitBoxes = new();

    public int SelectedIndex { get; private set; }
    public int Count => _entries.Count;
    public Entry? Selected => _entries.Count == 0 ? null : _entries[SelectedIndex];

    public MenuList Add(string label, Action onSelect, bool isEnabled = true, string? hint = null)
    {
        _entries.Add(new Entry(label, onSelect, isEnabled, hint));
        if (!_entries[SelectedIndex].IsEnabled && isEnabled) SelectedIndex = _entries.Count - 1;
        return this;   // Fluent Interface: menu.Add(...).Add(...)
    }

    /// <summary>Auswahl wiederherstellen, z. B. nachdem das Menü neu aufgebaut wurde.</summary>
    public void Select(int index)
    {
        if (_entries.Count > 0) SelectedIndex = Math.Clamp(index, 0, _entries.Count - 1);
    }

    public void Update(InputState input, AudioService audio)
    {
        if (_entries.Count == 0) return;
        int step = input.WasPressed(GameAction.Down) ? 1 : input.WasPressed(GameAction.Up) ? -1 : 0;
        if (step != 0)
        {
            // Deaktivierte Einträge überspringen; "+ Count" verhindert negative Modulo-Ergebnisse
            for (int attempt = 0; attempt < _entries.Count; attempt++)
            {
                SelectedIndex = (SelectedIndex + step + _entries.Count) % _entries.Count;
                if (_entries[SelectedIndex].IsEnabled) break;
            }
            audio.Play("pickup", 0.2f, 0.4f);
        }
        // Maus: Überfahren wählt, Klick löst aus. Nur wenn die Maus auch benutzt wird – sonst
        // würde ein ruhender Zeiger die Auswahl der Tastatur dauerhaft überschreiben.
        if (input.LastDevice == InputDevice.Mouse)
        {
            foreach ((int index, Rectangle box) in _hitBoxes)
            {
                if (!box.Contains(input.MousePosition) || !_entries[index].IsEnabled) continue;
                if (index != SelectedIndex)
                {
                    SelectedIndex = index;
                    audio.Play("pickup", 0.2f, 0.4f);
                }
                if (input.MouseWasPressed)
                {
                    audio.Play("unseal", 0.3f, 0.5f);
                    _entries[index].OnSelect();
                    return;
                }
                break;
            }
        }

        if (input.WasPressed(GameAction.Confirm) && _entries[SelectedIndex].IsEnabled)
        {
            audio.Play("unseal", 0.3f, 0.5f);
            _entries[SelectedIndex].OnSelect();
        }
    }

    /// <param name="maxVisible">Bei langen Listen wird um die Auswahl herum gescrollt.</param>
    public void Draw(SpriteBatch spriteBatch, BitmapFont font, float centerX, float top, int maxVisible = int.MaxValue)
    {
        int first = Math.Clamp(SelectedIndex - maxVisible / 2, 0, Math.Max(0, _entries.Count - maxVisible));
        int last = Math.Min(_entries.Count, first + maxVisible);
        _hitBoxes.Clear();
        for (int index = first; index < last; index++)
        {
            Entry entry = _entries[index];
            bool isSelected = index == SelectedIndex;
            string label = isSelected ? $"· {entry.Label} ·" : entry.Label;
            Color color = !entry.IsEnabled ? Palette.Ash * 0.6f : isSelected ? Palette.Gold : Palette.Bone;
            float y = top + (index - first) * (font.LineHeight + 4);
            font.DrawCentered(spriteBatch, label, centerX, y, color);

            // Trefferfläche etwas breiter als der Text, damit auch knapp daneben noch gilt.
            int width = font.MeasureWidth(label) + 16;
            _hitBoxes[index] = new Rectangle((int)(centerX - width / 2f), (int)y - 1, width, font.LineHeight + 2);
        }
    }
}
