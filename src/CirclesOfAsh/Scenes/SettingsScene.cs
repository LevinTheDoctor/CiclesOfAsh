using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Optionsmenü mit vier Reitern: Bildschirm · Audio · Steuerung · Gameplay.
/// Links/Rechts wechselt den Reiter, Hoch/Runter die Zeile, Werte über die Schultertasten.
/// Der Reiter „Steuerung" zeigt den erkannten Controller samt Profil und ist der spätere Ort für
/// frei belegbare Tasten (Content/Data/input.json, Roadmap). Alles sofort persistent.
/// Steuerung komplett per GameAction -> Tastatur UND Controller.
/// </summary>
public sealed class SettingsScene : SceneBase
{
    private enum Tab { Screen, Audio, Control, Gameplay }
    private enum ScreenRow { Scale, Fullscreen, VSync }
    private enum AudioRow { Master, Music, Sfx }
    private enum GameplayRow { AmbientLift, Rumble, DamageNumbers, Tutorial, Difficulty }

    private readonly List<DifficultyDefinition> _difficulties;
    private Tab _tab;
    private int _rowIndex;

    private readonly record struct TabLayout(string Title, int RowCount);
    private static TabLayout LayoutOf(Tab tab) => tab switch
    {
        Tab.Screen => new TabLayout("Bildschirm", 3),
        Tab.Audio => new TabLayout("Audio", 3),
        Tab.Control => new TabLayout("Steuerung", 3),
        _ => new TabLayout("Gameplay", 5),
    };

    public SettingsScene(GameContext context) : base(context)
    {
        _difficulties = context.Definitions.Difficulties.All.ToList();
    }

    // Trefferflaechen, von Draw gefuellt und von Update ausgewertet (wie in MenuList).
    private readonly Dictionary<Tab, Rectangle> _tabBoxes = new();
    private readonly Dictionary<int, Rectangle> _rowBoxes = new();
    private readonly Dictionary<int, Rectangle> _barBoxes = new();
    /// <summary>true, solange auf einem Regler gedrueckt gehalten wird (Ziehen darf den Balken verlassen).</summary>
    private bool _draggingBar;

    private GameSettings Settings => Context.Settings;
    private int RowCount => LayoutOf(_tab).RowCount;
    private int RowIndex
    {
        get => _rowIndex;
        set => _rowIndex = Math.Clamp(value, 0, RowCount - 1);
    }

    public override bool IsOverlay => true;

    public override void Update(float deltaSeconds)
    {
        InputState input = Context.Input;

        if (input.WasPressed(GameAction.Cancel))
        {
            Leave();
            return;
        }

        // Reiter wechseln: AbilityOne/Two (Tastatur Q/E, Gamepad X/Y).
        // Links/Rechts ist bewusst NICHT dafür reserviert – das ist die Taste, die jeder zuerst
        // drückt, um einen Wert zu ändern. Vorher lag es umgekehrt, und ohne Controller schien das
        // Menü gar nicht bedienbar zu sein.
        if (input.WasPressed(GameAction.AbilityOne) && _tab > Tab.Screen)
        {
            _tab--;
            RowIndex = Math.Min(RowIndex, RowCount - 1);
            Context.Audio.Play("pickup", 0.2f, 0.4f);
            return;
        }
        if (input.WasPressed(GameAction.AbilityTwo) && _tab < Tab.Gameplay)
        {
            _tab++;
            RowIndex = Math.Min(RowIndex, RowCount - 1);
            Context.Audio.Play("pickup", 0.2f, 0.4f);
            return;
        }

        // Hoch/Runter: Zeile; Links/Rechts: Wert der Zeile ändern
        if (input.WasPressed(GameAction.Down)) RowIndex = (RowIndex + 1) % RowCount;
        if (input.WasPressed(GameAction.Up)) RowIndex = (RowIndex - 1 + RowCount) % RowCount;

        int change = input.WasPressed(GameAction.Right) ? 1 : input.WasPressed(GameAction.Left) ? -1 : 0;
        if (change != 0) ChangeValue(change);

        if (input.LastDevice == InputDevice.Mouse) UpdateMouse(input);
    }

    /// <summary>
    /// Maus: Reiter anklicken, Zeile anklicken, Regler direkt ziehen. Die Rechtecke stammen aus
    /// dem letzten Draw – deshalb muss hier kein Layout doppelt gerechnet werden.
    /// </summary>
    private void UpdateMouse(InputState input)
    {
        Point cursor = input.MousePosition;

        if (input.MouseWasPressed)
        {
            foreach ((Tab tab, Rectangle box) in _tabBoxes)
            {
                if (!box.Contains(cursor) || tab == _tab) continue;
                _tab = tab;
                RowIndex = Math.Min(RowIndex, RowCount - 1);
                Context.Audio.Play("pickup", 0.2f, 0.4f);
                return;
            }
            foreach ((int index, Rectangle box) in _rowBoxes)
            {
                if (!box.Contains(cursor)) continue;
                if (index != RowIndex) Context.Audio.Play("pickup", 0.2f, 0.4f);
                RowIndex = index;
                break;
            }
        }

        if (!input.MouseIsDown)
        {
            _draggingBar = false;
            return;
        }
        if (!_barBoxes.TryGetValue(RowIndex, out Rectangle barBox)) return;

        // Ziehen beginnt nur AUF dem Regler, darf ihn danach aber verlassen – sonst reisst der
        // Wert ab, sobald man beim Ziehen minimal nach oben rutscht.
        if (input.MouseWasPressed && barBox.Contains(cursor)) _draggingBar = true;
        if (!_draggingBar) return;

        float ratio = Math.Clamp((cursor.X - barBox.Left) / (float)Math.Max(1, barBox.Width), 0f, 1f);
        SetRatio(ratio);
    }

    /// <summary>Setzt den Regler der aktuellen Zeile direkt (Maus). Tastatur/Pad gehen über ChangeValue.</summary>
    private void SetRatio(float ratio)
    {
        switch (_tab)
        {
            case Tab.Audio:
                switch ((AudioRow)RowIndex)
                {
                    case AudioRow.Master: Settings.MasterVolume = ratio; break;
                    case AudioRow.Music: Settings.MusicVolume = ratio; break;
                    case AudioRow.Sfx: Settings.SfxVolume = ratio; break;
                    default: return;
                }
                break;
            case Tab.Gameplay:
                switch ((GameplayRow)RowIndex)
                {
                    case GameplayRow.AmbientLift: Settings.AmbientLift = ratio; break;
                    case GameplayRow.Rumble: Settings.RumbleIntensity = ratio; break;
                    default: return;
                }
                break;
            default: return;
        }
        Context.SaveSettings();   // wirkt sofort: Lautstärke, Helligkeit, Vibration
    }

    private void Leave()
    {
        Context.SaveSettings();
        Context.Scenes.Pop();
    }

    /// <summary>Ändert den Wert der aktuellen Zeile im aktuellen Reiter.</summary>
    private void ChangeValue(int step)
    {
        switch (_tab)
        {
            case Tab.Screen:
                switch ((ScreenRow)RowIndex)
                {
                    case ScreenRow.Scale:
                        Settings.ScreenScale = Math.Clamp(Settings.ScreenScale + step, 0, 6);
                        ApplyScreenSettings();
                        break;
                    case ScreenRow.Fullscreen:
                        Settings.Fullscreen = !Settings.Fullscreen;
                        ApplyScreenSettings();
                        break;
                    case ScreenRow.VSync:
                        Settings.VSync = !Settings.VSync;
                        ApplyScreenSettings();
                        break;
                }
                break;
            case Tab.Audio:
                switch ((AudioRow)RowIndex)
                {
                    case AudioRow.Master:
                        Settings.MasterVolume = Math.Clamp(Settings.MasterVolume + step * 0.05f, 0f, 1f);
                        break;
                    case AudioRow.Music:
                        Settings.MusicVolume = Math.Clamp(Settings.MusicVolume + step * 0.05f, 0f, 1f);
                        break;
                    case AudioRow.Sfx:
                        Settings.SfxVolume = Math.Clamp(Settings.SfxVolume + step * 0.05f, 0f, 1f);
                        Context.Audio.Play("pickup", 0.5f, 0.3f);   // Probe-Sound
                        break;
                }
                break;
            case Tab.Gameplay:
                switch ((GameplayRow)RowIndex)
                {
                    case GameplayRow.AmbientLift:
                        Settings.AmbientLift = Math.Clamp(Settings.AmbientLift + step * 0.05f, 0f, 1f);
                        break;
                    case GameplayRow.Rumble:
                        Settings.RumbleIntensity = Math.Clamp(Settings.RumbleIntensity + step * 0.1f, 0f, 1f);
                        break;
                    case GameplayRow.DamageNumbers:
                        Settings.ShowDamageNumbers = !Settings.ShowDamageNumbers;
                        break;
                    case GameplayRow.Tutorial:
                        Settings.Tutorial = !Settings.Tutorial;
                        break;
                    case GameplayRow.Difficulty:
                    {
                        int index = Math.Max(0, _difficulties.FindIndex(candidate => candidate.Id == Settings.DifficultyId));
                        index = (index + step + _difficulties.Count) % _difficulties.Count;
                        Settings.DifficultyId = _difficulties[index].Id;
                        Context.Progression.SetDifficulty(Settings.DifficultyId);
                        break;
                    }
                }
                break;
            case Tab.Control:
                break;   // Reiter ist rein informell, bis input.json umsetzbar ist
        }
        Context.SaveSettings();
        Context.Audio.Play("pickup", 0.25f, 0.2f);
    }

    /// <summary>Setzt Fenster/Vollbild an der GraphicsDeviceManager-Referenz (vom Spiel gesetzt).</summary>
    private void ApplyScreenSettings()
    {
        ScreenSetup.Apply(Context, Settings);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D pixel = Context.Assets.Pixel;
        BitmapFont font = Context.Font;
        float centerX = CirclesGame.VirtualWidth / 2f;

        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.7f);
        var panel = new Rectangle(40, 14, CirclesGame.VirtualWidth - 80, 240);
        UiDraw.Panel(spriteBatch, pixel, panel);
        Context.TitleFont.DrawCentered(spriteBatch, "Optionen", centerX, panel.Top + 4, Palette.Gold);

        DrawTabs(spriteBatch, font, pixel, centerX, panel);

        // Zeilen des aktiven Reiters
        float y = panel.Top + 40;
        _rowBoxes.Clear();
        _barBoxes.Clear();
        for (int index = 0; index < RowCount; index++)
        {
            _rowBoxes[index] = new Rectangle(panel.Left + 8, (int)y - 1, panel.Width - 16, font.LineHeight + 2);
            (string label, string value) = RowTexts(_tab, index);
            bool isSelected = index == RowIndex;
            Color color = isSelected ? Palette.Gold : Palette.Bone * 0.85f;
            font.DrawShadowed(spriteBatch, label, new Vector2(panel.Left + 12, y), color);

            if (RowRatio(_tab, index) is { } ratio)
            {
                // Regler: echter gezeichneter Balken (wie in der HUD) plus Prozentwert dahinter.
                var bar = new Rectangle(panel.Left + BarLeft, (int)y + 2, BarWidth, font.LineHeight - 3);
                _barBoxes[index] = bar;
                UiDraw.Bar(spriteBatch, pixel, bar, ratio, isSelected ? Palette.Gold : Palette.Faith);
                font.DrawShadowed(spriteBatch, value, new Vector2(bar.Right + 8, y),
                    isSelected ? Palette.Faith : Palette.Bone);
            }
            else
            {
                font.DrawShadowed(spriteBatch, isSelected ? $"‹ {value} ›" : value,
                    new Vector2(panel.Left + BarLeft, y), isSelected ? Palette.Faith : Palette.Bone);
            }
            y += font.LineHeight + 3;
        }

        DrawTabHint(spriteBatch, font, centerX, panel);
        InputState hintInput = Context.Input;
        string hint = $"{hintInput.Glyph(GameAction.AbilityOne)}/{hintInput.Glyph(GameAction.AbilityTwo)}: Reiter · "
            + $"Hoch/Runter: Zeile · Links/Rechts: ändern · {hintInput.Glyph(GameAction.Cancel)}: zurück";
        font.DrawCentered(spriteBatch, hint, centerX, CirclesGame.VirtualHeight - 12, Palette.Ash);
        spriteBatch.End();
    }

    /// <summary>Reiterleiste: aktiver Reiter hervorgehoben, Rest schlicht.</summary>
    private void DrawTabs(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, float centerX, Rectangle panel)
    {
        float y = panel.Top + 24;
        _tabBoxes.Clear();
        string[] titles = { "Bildschirm", "Audio", "Steuerung", "Gameplay" };
        float totalWidth = titles.Sum(title => font.MeasureWidth(title) + 24);
        float x = centerX - totalWidth / 2f;
        for (int index = 0; index < titles.Length; index++)
        {
            var tab = (Tab)index;
            int width = font.MeasureWidth(titles[index]) + 24;
            bool active = tab == _tab;
            var box = new Rectangle((int)x - 4, (int)y - 2, width + 8, font.LineHeight + 4);
            _tabBoxes[tab] = box;
            if (active) UiDraw.Rect(spriteBatch, pixel, box, Palette.Gold * 0.18f);
            font.DrawShadowed(spriteBatch, titles[index], new Vector2(x, y), active ? Palette.Gold : Palette.Bone * 0.7f);
            x += width + 8;
        }
    }

    /// <summary>Beschriftung + Wert der Zeile (Reiter, Index).</summary>
    /// <summary>
    /// Zeigt die tatsächlich benutzte Größe. Bei "Auto" steht die errechnete Skalierung dabei,
    /// und ein fester Wert, der nicht auf den Bildschirm passt, wird als geklemmt ausgewiesen –
    /// sonst behauptet das Menü 3x, während das Fenster in Wahrheit kleiner ist.
    /// </summary>
    private string ScaleValueText()
    {
        int effective = ScreenSetup.EffectiveScale(Context, Settings);
        string size = $"{CirclesGame.VirtualWidth * effective}x{CirclesGame.VirtualHeight * effective}";
        if (Settings.ScreenScale <= 0) return $"Auto ({effective}x · {size})";
        return effective == Settings.ScreenScale
            ? $"{effective}x ({size})"
            : $"{Settings.ScreenScale}x → {effective}x ({size}, Bildschirm zu klein)";
    }

    /// <summary>
    /// Anteil 0..1, wenn diese Zeile ein Regler ist – sonst null. Regler werden als gezeichneter
    /// Balken dargestellt; der frühere Textbalken benutzte Blockzeichen, die es im Zeichensatz der
    /// Bitmap-Schrift gar nicht gibt, und war deshalb unsichtbar.
    /// </summary>
    private float? RowRatio(Tab tab, int row) => tab switch
    {
        Tab.Audio => (AudioRow)row switch
        {
            AudioRow.Master => Settings.MasterVolume,
            AudioRow.Music => Settings.MusicVolume,
            AudioRow.Sfx => Settings.SfxVolume,
            _ => null,
        },
        Tab.Gameplay => (GameplayRow)row switch
        {
            GameplayRow.AmbientLift => Settings.AmbientLift,
            GameplayRow.Rumble => Settings.RumbleIntensity,
            _ => null,
        },
        _ => null,
    };

    private (string Label, string Value) RowTexts(Tab tab, int row) => tab switch
    {
        Tab.Screen => (ScreenRow)row switch
        {
            ScreenRow.Scale => ("Bildschirmgröße", ScaleValueText()),
            ScreenRow.Fullscreen => ("Vollbild", Settings.Fullscreen ? "An" : "Aus"),
            ScreenRow.VSync => ("VSync", Settings.VSync ? "An" : "Aus"),
            _ => ("", ""),
        },
        Tab.Audio => (AudioRow)row switch
        {
            AudioRow.Master => ("Lautstärke", Percent(Settings.MasterVolume)),
            AudioRow.Music => ("Musik", Percent(Settings.MusicVolume)),
            AudioRow.Sfx => ("Effekte", Percent(Settings.SfxVolume)),
            _ => ("", ""),
        },
        Tab.Control => (ControlRow)row switch
        {
            ControlRow.Controller => ("Controller", Context.Input.HasGamePad ? ControllerName() : "keiner"),
            ControlRow.Profile => ("Profil", Context.Input.HasGamePad ? ControllerProfile() : "–"),
            ControlRow.Bindings => ("Belegung", "fest (Roadmap: frei)"),
            _ => ("", ""),
        },
        _ => (GameplayRow)row switch
        {
            GameplayRow.AmbientLift => ("Helligkeit", Percent(Settings.AmbientLift)),
            GameplayRow.Rumble => ("Vibration", Percent(Settings.RumbleIntensity)),
            GameplayRow.DamageNumbers => ("Schadenszahlen", Settings.ShowDamageNumbers ? "An" : "Aus"),
            // Schaltet sich nach dem Durchlauf selbst ab; hier wieder einschaltbar.
            GameplayRow.Tutorial => ("Tutorial", Settings.Tutorial ? "Beim nächsten Lauf" : "Aus"),
            GameplayRow.Difficulty => ("Schwierigkeit", _difficulties.FirstOrDefault(d => d.Id == Settings.DifficultyId)?.Name ?? "?"),
            _ => ("", ""),
        },
    };

    private enum ControlRow { Controller, Profile, Bindings }

    /// <summary>Gerätename des Controllers, gekürzt, damit er in die Zeile passt.</summary>
    private string ControllerName()
    {
        string? name = Context.Input.CurrentPadName;
        if (string.IsNullOrEmpty(name)) return "verbunden";
        return name.Length > 26 ? name[..23] + "…" : name;
    }

    private string ControllerProfile()
    {
        string? name = Context.Input.CurrentPadName;
        if (string.IsNullOrEmpty(name)) return "–";
        IReadOnlyDictionary<GameAction, string>? labels = Context.ResolveControllerLabels(name);
        return labels is not null ? "erkannt" : "Standard";
    }

    /// <summary>Kurzhinweis unten im Panel, passend zum Reiter.</summary>
    private void DrawTabHint(SpriteBatch spriteBatch, BitmapFont font, float centerX, Rectangle panel)
    {
        string? hint = _tab switch
        {
            Tab.Screen => "Fenstergröße in Faktoren der virtuellen Auflösung. Wirkt sofort.",
            Tab.Audio => "Alle Regler wirken sofort. Effekte spielen beim Ändern einen Probe-Sound.",
            Tab.Control => "Zeigt den erkannten Controller und sein Beschriftungsprofil (controllers.json).",
            Tab.Gameplay => _difficulties.FirstOrDefault(d => d.Id == Settings.DifficultyId)?.Description,
            _ => null,
        };
        if (hint is not null)
            font.DrawCenteredLines(spriteBatch, font.Wrap(hint, panel.Width - 30), centerX, panel.Bottom - 44, Palette.Ash);
    }

    private static string Percent(float ratio) => $"{(int)MathF.Round(ratio * 100f)} %";

    /// <summary>Breite des gezeichneten Reglers in Pixeln der virtuellen Auflösung.</summary>
    private const int BarWidth = 90;
    private const int BarLeft = 130;
}

/// <summary>
/// Zentrale Anwendung der Bildschirm-Einstellungen. CirclesGame registriert hier seinen
/// GraphicsDeviceManager (statisch, einmalig) – Szenen können so den Modus ändern, ohne
/// die Game-Klasse zu kennen.
/// </summary>
public static class ScreenSetup
{
    /// <summary>
    /// Anteil der Bildschirmhöhe, der für das Fenster benutzt werden darf. Der Rest bleibt für
    /// Menüleiste, Titelleiste und Dock. Wird das ignoriert, verkleinert das Betriebssystem das
    /// Fenster eigenmächtig – und die Leinwand passt dann nicht mehr ganzzahlig hinein.
    /// </summary>
    private const float UsableHeightFraction = 0.92f;
    private const float UsableWidthFraction = 0.98f;

    private static GraphicsDeviceManager? _manager;

    public static void Register(GraphicsDeviceManager manager) => _manager = manager;

    /// <summary>
    /// Größte ganzzahlige Skalierung der 480x270-Leinwand, die auf den Bildschirm passt.
    /// Mindestens 1 – lieber ein zu großes Fenster als gar kein Bild.
    /// </summary>
    public static int AutoScale(int fallback)
    {
        try
        {
            DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int usableWidth = (int)(display.Width * UsableWidthFraction);
            int usableHeight = (int)(display.Height * UsableHeightFraction);
            return Math.Max(1, Math.Min(usableWidth / CirclesGame.VirtualWidth, usableHeight / CirclesGame.VirtualHeight));
        }
        catch (Exception exception)   // ohne Bildschirm (CI) gibt es keinen Adapter
        {
            Log.Warn($"Bildschirmgröße nicht ermittelbar ({exception.GetType().Name}) – nutze {fallback}x.");
            return fallback;
        }
    }

    /// <summary>
    /// Die tatsächlich benutzte Skalierung: 0 bedeutet "automatisch". Ein fest eingestellter Wert
    /// wird trotzdem heruntergeklemmt, wenn er nicht auf den Bildschirm passt – sonst verkleinert
    /// das Betriebssystem das Fenster auf eine krumme Größe und es bleibt ein dicker Rand.
    /// </summary>
    public static int EffectiveScale(GameContext context, GameSettings settings)
    {
        int fallback = context.Definitions.Balance.DefaultScreenScale;
        int automatic = AutoScale(fallback);
        return settings.ScreenScale <= 0 ? automatic : Math.Min(settings.ScreenScale, automatic);
    }

    public static void Apply(GameContext context, GameSettings settings)
    {
        if (_manager is null) return;
        int scale = EffectiveScale(context, settings);
        int width = CirclesGame.VirtualWidth * scale;
        int height = CirclesGame.VirtualHeight * scale;

        _manager.PreferredBackBufferWidth = width;
        _manager.PreferredBackBufferHeight = height;
        _manager.SynchronizeWithVerticalRetrace = settings.VSync;
        // Nur tatsächlich umschalten, wenn der gewünschte Zustand abweicht (Toggle = Umschalter!)
        if (_manager.IsFullScreen != settings.Fullscreen) _manager.ToggleFullScreen();
        _manager.ApplyChanges();

        // Zurücklesen: Das Betriebssystem darf die Anforderung ablehnen. Ohne diese Zeile blieb
        // völlig unsichtbar, dass das Fenster kleiner ist als angefordert.
        Rectangle actual = _manager.GraphicsDevice.PresentationParameters.Bounds;
        string wanted = settings.ScreenScale <= 0 ? $"Auto -> {scale}x" : $"{settings.ScreenScale}x -> {scale}x";
        if (settings.Fullscreen || (actual.Width == width && actual.Height == height))
            Log.Info($"Bildschirm: {wanted}, Fenster {actual.Width}x{actual.Height}.");
        else
            Log.Warn($"Bildschirm: {wanted} angefordert ({width}x{height}), erhalten {actual.Width}x{actual.Height} – es bleibt ein Rand.");
    }
}