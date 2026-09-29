using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Optionsmenü mit fünf Reitern: Bildschirm · Audio · Steuerung · Gameplay · Sprache.
/// Q/E (Gamepad X/Y) wechselt den Reiter, Hoch/Runter die Zeile, Links/Rechts den Wert.
/// Der Reiter „Steuerung" zeigt den erkannten Controller samt Profil und seine Tastenbilder (mit
/// Vorschau aller Familien) und ist der spätere Ort für frei belegbare Tasten (Roadmap).
/// Alles wirkt sofort und wird gespeichert.
/// Steuerung komplett per GameAction -> Tastatur UND Controller.
/// </summary>
public sealed class SettingsScene : SceneBase
{
    private enum Tab { Screen, Audio, Control, Gameplay, Language }
    private enum ScreenRow { Scale, Fullscreen, VSync }
    private enum AudioRow { Master, Music, Sfx }
    private enum ControlRow { Controller, Profile, Bindings, Glyphs }
    private enum GameplayRow { AmbientLift, Rumble, DamageNumbers, Tutorial, BossFights, Difficulty }

    private const Tab LastTab = Tab.Language;
    /// <summary>Zeilenhöhe im Sprach-Reiter: Die Flaggen (16 px) brauchen mehr Platz als eine Textzeile.</summary>
    private const int LanguageRowHeight = 22;

    private readonly List<DifficultyDefinition> _difficulties;
    private readonly IReadOnlyList<LanguageDefinition> _languages;
    private Tab _tab;
    private int _rowIndex;

    /// <summary>Familien für die Tastenbild-Vorschau. Index 0 der Auswahl = "automatisch" (erkannter Controller).</summary>
    private static readonly string[] PreviewFamilies = { "xbox", "playstation", "switch", InputState.KeyboardGlyphFamily };
    private int _glyphPreview;

    /// <summary>Aktionen der Legende im Reiter „Steuerung", in Anzeigereihenfolge (Quelltext der Namen).</summary>
    private static readonly (GameAction Action, string Name)[] LegendActions =
    {
        (GameAction.Jump, Loc.N("Springen")), (GameAction.Attack, Loc.N("Angriff")),
        (GameAction.Block, Loc.N("Block")), (GameAction.Dash, Loc.N("Dash")),
        (GameAction.AbilityOne, Loc.N("Gabe 1")), (GameAction.AbilityTwo, Loc.N("Gabe 2")),
        (GameAction.Interact, Loc.N("Benutzen")), (GameAction.Confirm, Loc.N("Bestätigen")),
        (GameAction.Cancel, Loc.N("Zurück")), (GameAction.Pause, Loc.N("Pause")),
        (GameAction.Randomize, Loc.N("Zufall")),
    };

    /// <summary>Überschrift (deutscher Quelltext, übersetzt beim Zeichnen) und Zeilenzahl eines Reiters.</summary>
    private readonly record struct TabLayout(string Title, int RowCount);

    private TabLayout LayoutOf(Tab tab) => tab switch
    {
        Tab.Screen => new TabLayout(Loc.N("Bildschirm"), 3),
        Tab.Audio => new TabLayout(Loc.N("Audio"), 3),
        Tab.Control => new TabLayout(Loc.N("Steuerung"), 4),
        Tab.Gameplay => new TabLayout(Loc.N("Gameplay"), 6),
        _ => new TabLayout(Loc.N("Sprache"), Math.Max(1, _languages.Count)),
    };

    public SettingsScene(GameContext context) : base(context)
    {
        _difficulties = context.Definitions.Difficulties.All.ToList();
        _languages = context.Localizer.Languages;
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
            SwitchTab(_tab - 1);
            return;
        }
        if (input.WasPressed(GameAction.AbilityTwo) && _tab < LastTab)
        {
            SwitchTab(_tab + 1);
            return;
        }

        // Hoch/Runter: Zeile; Links/Rechts: Wert der Zeile ändern
        if (input.WasPressed(GameAction.Down)) RowIndex = (RowIndex + 1) % RowCount;
        if (input.WasPressed(GameAction.Up)) RowIndex = (RowIndex - 1 + RowCount) % RowCount;

        int change = input.WasPressed(GameAction.Right) ? 1 : input.WasPressed(GameAction.Left) ? -1 : 0;
        if (change != 0) ChangeValue(change);
        // Im Sprach-Reiter wählt auch Bestätigen – dort gibt es keinen Wert zu erhöhen.
        if (_tab == Tab.Language && input.WasPressed(GameAction.Confirm)) SelectLanguage(RowIndex);

        if (input.LastDevice == InputDevice.Mouse) UpdateMouse(input);
    }

    private void SwitchTab(Tab tab)
    {
        _tab = tab;
        // Im Sprach-Reiter steht der Cursor gleich auf der aktiven Sprache.
        RowIndex = tab == Tab.Language ? ActiveLanguageIndex() : Math.Min(RowIndex, RowCount - 1);
        Context.Audio.Play("pickup", 0.2f, 0.4f);
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
                SwitchTab(tab);
                return;
            }
            foreach ((int index, Rectangle box) in _rowBoxes)
            {
                if (!box.Contains(cursor)) continue;
                if (index != RowIndex) Context.Audio.Play("pickup", 0.2f, 0.4f);
                RowIndex = index;
                // Eine Sprache ist mit einem Klick gewählt – es gibt keinen Wert zum Ziehen.
                if (_tab == Tab.Language) SelectLanguage(index);
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
                    case GameplayRow.BossFights:
                        Settings.ManualBossFights = !Settings.ManualBossFights;
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
            case Tab.Language:
                SelectLanguage(RowIndex);
                return;   // SelectLanguage speichert und spielt selbst den Ton
            case Tab.Control:
                // Nur die Vorschau lässt sich blättern; die Belegung selbst ist (noch) fest.
                if ((ControlRow)RowIndex != ControlRow.Glyphs) return;
                _glyphPreview = (_glyphPreview + step + PreviewFamilies.Length + 1) % (PreviewFamilies.Length + 1);
                break;
        }
        Context.SaveSettings();
        Context.Audio.Play("pickup", 0.25f, 0.2f);
    }

    /// <summary>
    /// Stellt die Sprache um. Wirkt sofort: SaveSettings -> GameContext.ApplyLanguage meldet den
    /// Wechsel an alle Szenen im Stapel, deren Menüs sich dann neu beschriften.
    /// </summary>
    private void SelectLanguage(int index)
    {
        if (index < 0 || index >= _languages.Count) return;
        LanguageDefinition language = _languages[index];
        if (language.Id == Context.Localizer.CurrentId) return;
        Settings.Language = language.Id;
        Context.SaveSettings();
        Context.Audio.Play("unseal", 0.35f, 0.4f);
    }

    private int ActiveLanguageIndex()
    {
        for (int index = 0; index < _languages.Count; index++)
            if (_languages[index].Id == Context.Localizer.CurrentId) return index;
        return 0;
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
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Optionen"), centerX, panel.Top + 4, Palette.Gold);

        DrawTabs(spriteBatch, font, pixel, centerX, panel);
        _rowBoxes.Clear();
        _barBoxes.Clear();
        if (_tab == Tab.Language) DrawLanguages(spriteBatch, font, pixel, panel);
        else DrawRows(spriteBatch, font, pixel, panel);
        if (_tab == Tab.Control) DrawGlyphLegend(spriteBatch, font, pixel, panel);

        DrawTabHint(spriteBatch, font, centerX, panel);
        InputState hintInput = Context.Input;
        string hint = Loc.T("{0}/{1}: Reiter · Hoch/Runter: Zeile · Links/Rechts: ändern · {2}: zurück",
            hintInput.Glyph(GameAction.AbilityOne), hintInput.Glyph(GameAction.AbilityTwo), hintInput.Glyph(GameAction.Cancel));
        font.DrawCentered(spriteBatch, hint, centerX, CirclesGame.VirtualHeight - 12, Palette.Ash);
        spriteBatch.End();
    }

    /// <summary>Zeilen der Reiter mit Werten: Beschriftung links, Wert oder Regler rechts.</summary>
    private void DrawRows(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Rectangle panel)
    {
        float y = panel.Top + 40;
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
    }

    /// <summary>
    /// Sprachauswahl: je Sprache ihre Flagge und ihr Name in der EIGENEN Sprache ("English",
    /// "Deutsch"). Wer versehentlich eine Sprache erwischt, die er nicht liest, findet so über die
    /// Flaggen zurück – dafür sind sie da.
    /// </summary>
    private void DrawLanguages(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Rectangle panel)
    {
        float y = panel.Top + 40;
        for (int index = 0; index < _languages.Count; index++)
        {
            LanguageDefinition language = _languages[index];
            bool isSelected = index == RowIndex;
            bool isActive = language.Id == Context.Localizer.CurrentId;
            var row = new Rectangle(panel.Left + 60, (int)y, panel.Width - 120, LanguageRowHeight - 2);
            _rowBoxes[index] = row;
            if (isSelected) UiDraw.Rect(spriteBatch, pixel, row, Palette.Gold * 0.12f);

            var flag = new Rectangle(row.Left + 6, row.Top + 2, 24, 16);
            if (Context.Assets.HasTexture(language.Flag))
                spriteBatch.Draw(Context.Assets.GetTexture(language.Flag), flag, Color.White);
            // Rahmen um die Flagge: Der schwarze Streifen verschwände sonst im dunklen Panel.
            var frame = flag;
            frame.Inflate(1, 1);
            UiDraw.Border(spriteBatch, pixel, frame, isActive ? Palette.Gold : Palette.Ash * 0.8f);

            Color nameColor = isSelected ? Palette.Gold : isActive ? Palette.Faith : Palette.Bone * 0.85f;
            float textY = row.Top + (row.Height - font.LineHeight) / 2f + 1;
            font.DrawShadowed(spriteBatch, language.Name, new Vector2(flag.Right + 10, textY), nameColor);
            if (isActive)
            {
                string marker = Loc.T("aktiv");
                font.DrawShadowed(spriteBatch, marker, new Vector2(row.Right - 8 - font.MeasureWidth(marker), textY), Palette.Faith);
            }
            y += LanguageRowHeight;
        }
    }

    /// <summary>Reiterleiste: aktiver Reiter hervorgehoben, Rest schlicht. Vor "Sprache" steht die aktive Flagge.</summary>
    private void DrawTabs(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, float centerX, Rectangle panel)
    {
        const int flagWidth = 12, flagGap = 4;
        float y = panel.Top + 24;
        _tabBoxes.Clear();
        Tab[] tabs = Enum.GetValues<Tab>();
        Texture2D? flag = ActiveSmallFlag();
        // Breite je Reiter vorab, damit die Leiste als Ganzes zentriert steht.
        int WidthOf(Tab tab) => font.MeasureWidth(Loc.T(LayoutOf(tab).Title)) + 16
                                + (tab == Tab.Language && flag is not null ? flagWidth + flagGap : 0);
        float totalWidth = tabs.Sum(tab => WidthOf(tab) + 8) - 8;
        float x = centerX - totalWidth / 2f;
        foreach (Tab tab in tabs)
        {
            int width = WidthOf(tab);
            bool active = tab == _tab;
            var box = new Rectangle((int)x, (int)y - 2, width, font.LineHeight + 4);
            _tabBoxes[tab] = box;
            if (active) UiDraw.Rect(spriteBatch, pixel, box, Palette.Gold * 0.18f);
            float textX = x + 8;
            if (tab == Tab.Language && flag is not null)
            {
                spriteBatch.Draw(flag, new Vector2(textX, y + 1), Color.White);
                textX += flagWidth + flagGap;
            }
            font.DrawShadowed(spriteBatch, Loc.T(LayoutOf(tab).Title), new Vector2(textX, y), active ? Palette.Gold : Palette.Bone * 0.7f);
            x += width + 8;
        }
    }

    /// <summary>Kleine Flagge der aktiven Sprache (Id + ".small") oder null, wenn es keine gibt.</summary>
    private Texture2D? ActiveSmallFlag()
    {
        string? flagId = Context.Localizer.Current?.Flag;
        if (string.IsNullOrEmpty(flagId)) return null;
        string smallId = flagId + ".small";
        return Context.Assets.HasTexture(smallId) ? Context.Assets.GetTexture(smallId) : null;
    }

    /// <summary>
    /// Zeigt die tatsächlich benutzte Größe. Bei "Auto" steht die errechnete Fenstergröße dabei,
    /// und ein fester Wert, der nicht auf den Bildschirm passt, wird als geklemmt ausgewiesen –
    /// sonst behauptet das Menü 3x, während das Fenster in Wahrheit kleiner ist.
    /// </summary>
    private string ScaleValueText()
    {
        Point window = ScreenSetup.WindowSize(Context, Settings);
        string size = $"{window.X}x{window.Y}";
        if (Settings.ScreenScale <= 0) return Loc.T("Auto ({0})", size);
        int effective = ScreenSetup.EffectiveScale(Context, Settings);
        return effective == Settings.ScreenScale
            ? $"{effective}x ({size})"
            : Loc.T("{0}x → {1}x ({2}, Bildschirm zu klein)", Settings.ScreenScale, effective, size);
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

    /// <summary>Beschriftung + Wert der Zeile (Reiter, Index), schon übersetzt.</summary>
    private (string Label, string Value) RowTexts(Tab tab, int row) => tab switch
    {
        Tab.Screen => (ScreenRow)row switch
        {
            ScreenRow.Scale => (Loc.T("Bildschirmgröße"), ScaleValueText()),
            ScreenRow.Fullscreen => (Loc.T("Vollbild"), OnOff(Settings.Fullscreen)),
            ScreenRow.VSync => (Loc.T("VSync"), OnOff(Settings.VSync)),
            _ => ("", ""),
        },
        Tab.Audio => (AudioRow)row switch
        {
            AudioRow.Master => (Loc.T("Lautstärke"), Percent(Settings.MasterVolume)),
            AudioRow.Music => (Loc.T("Musik"), Percent(Settings.MusicVolume)),
            AudioRow.Sfx => (Loc.T("Effekte"), Percent(Settings.SfxVolume)),
            _ => ("", ""),
        },
        Tab.Control => (ControlRow)row switch
        {
            ControlRow.Controller => (Loc.T("Controller"), Context.Input.HasGamePad ? ControllerName() : Loc.T("keiner")),
            ControlRow.Profile => (Loc.T("Profil"), Context.Input.PadProfile?.Name ?? "–"),
            ControlRow.Bindings => (Loc.T("Belegung"), Loc.T("fest (Roadmap: frei)")),
            ControlRow.Glyphs => (Loc.T("Tastenbilder"), PreviewName()),
            _ => ("", ""),
        },
        _ => (GameplayRow)row switch
        {
            GameplayRow.AmbientLift => (Loc.T("Helligkeit"), Percent(Settings.AmbientLift)),
            GameplayRow.Rumble => (Loc.T("Vibration"), Percent(Settings.RumbleIntensity)),
            GameplayRow.DamageNumbers => (Loc.T("Schadenszahlen"), OnOff(Settings.ShowDamageNumbers)),
            // Schaltet sich nach dem Durchlauf selbst ab; hier wieder einschaltbar.
            GameplayRow.Tutorial => (Loc.T("Tutorial"), Settings.Tutorial ? Loc.T("Beim nächsten Lauf") : Loc.T("Aus")),
            GameplayRow.BossFights => (Loc.T("Bosskämpfe"), Settings.ManualBossFights ? Loc.T("Selbst kämpfen") : Loc.T("Automatik hilft")),
            GameplayRow.Difficulty => (Loc.T("Schwierigkeit"), CurrentDifficulty()?.Name ?? "?"),
            _ => ("", ""),
        },
    };

    private DifficultyDefinition? CurrentDifficulty() =>
        _difficulties.FirstOrDefault(difficulty => difficulty.Id == Settings.DifficultyId);

    private static string OnOff(bool value) => value ? Loc.T("An") : Loc.T("Aus");

    /// <summary>Gerätename des Controllers, gekürzt, damit er in die Zeile passt.</summary>
    private string ControllerName()
    {
        string? name = Context.Input.CurrentPadName;
        if (string.IsNullOrEmpty(name)) return Loc.T("verbunden");
        return name.Length > 26 ? name[..23] + "…" : name;
    }

    // ------------------------------------------------------------------ Tastenbilder
    /// <summary>Familie, deren Bilder die Legende gerade zeigt: gewählt, oder die des erkannten Controllers.</summary>
    private string PreviewFamily => _glyphPreview > 0
        ? PreviewFamilies[_glyphPreview - 1]
        : Context.Input.PadProfile?.GlyphFamily ?? InputState.KeyboardGlyphFamily;

    /// <summary>Beschriftungen zur Vorschau-Familie. null = Tastatur (InputState.LabelFor kennt deren Namen).</summary>
    private ControllerProfile? PreviewProfile =>
        PreviewFamily == InputState.KeyboardGlyphFamily ? null
        : _glyphPreview == 0 ? Context.Input.PadProfile
        : Context.ControllerProfileOfFamily(PreviewFamily);

    private string PreviewName()
    {
        string family = FamilyName(PreviewFamily);
        return _glyphPreview == 0 ? Loc.T("Automatisch ({0})", family) : family;
    }

    private static string FamilyName(string family) => family switch
    {
        "xbox" => "Xbox",
        "playstation" => "PlayStation",
        "switch" => "Nintendo Switch",
        _ => Loc.T("Tastatur"),
    };

    /// <summary>
    /// Legende unter den Zeilen: jede Aktion mit ihrem Tastenbild, zweispaltig. Zeigt die Assets
    /// aus tools/assetgen/interface.py so, wie Hinweise sie künftig benutzen können.
    /// </summary>
    private void DrawGlyphLegend(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Rectangle panel)
    {
        const int rowHeight = ButtonGlyphs.Height + 2;
        const int rowsPerColumn = 6;
        string family = PreviewFamily;
        ControllerProfile? profile = PreviewProfile;
        float top = panel.Top + 40 + RowCount * (font.LineHeight + 3) + 6;
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(panel.Left + 12, (int)top - 4, panel.Width - 24, 1), Palette.Gold * 0.3f);

        // Laufen zuerst: bei Controllern Stick und Steuerkreuz, bei der Tastatur A und D.
        string[] movement = family == InputState.KeyboardGlyphFamily ? new[] { "A", "D" } : new[] { "stick", "dpad" };
        var entries = new List<(string[] Labels, string Name)> { (movement, Loc.T("Bewegen")) };
        entries.AddRange(LegendActions.Select(entry =>
            (new[] { InputState.LabelFor(entry.Action, profile) }, Loc.T(entry.Name))));

        // Breite der Bildgruppe je Eintrag – die Namen einer Spalte stehen dann bündig hinter der breitesten.
        int GroupWidth(string[] labels) => labels.Sum(label => ButtonGlyphs.Measure(Context, family, label) + 2);
        int columns = (entries.Count + rowsPerColumn - 1) / rowsPerColumn;
        for (int column = 0; column < columns; column++)
        {
            var columnEntries = entries.Skip(column * rowsPerColumn).Take(rowsPerColumn).ToList();
            float left = panel.Left + 24 + column * 190;
            float nameX = left + columnEntries.Max(entry => GroupWidth(entry.Labels)) + 4;
            for (int row = 0; row < columnEntries.Count; row++)
            {
                float x = left;
                float y = top + row * rowHeight;
                foreach (string label in columnEntries[row].Labels)
                {
                    ButtonGlyphs.Draw(spriteBatch, Context, family, label, new Vector2(x, y), Color.White);
                    x += ButtonGlyphs.Measure(Context, family, label) + 2;
                }
                font.DrawShadowed(spriteBatch, columnEntries[row].Name, new Vector2(nameX, y + 1), Palette.Bone * 0.85f);
            }
        }
    }

    /// <summary>Kurzhinweis unten im Panel, passend zum Reiter.</summary>
    private void DrawTabHint(SpriteBatch spriteBatch, BitmapFont font, float centerX, Rectangle panel)
    {
        string? hint = _tab switch
        {
            Tab.Screen => Loc.T("Fenstergröße: Auto füllt den Bildschirm, Faktoren sind pixelgenau. Das Fenster lässt sich frei ziehen."),
            Tab.Audio => Loc.T("Alle Regler wirken sofort. Effekte spielen beim Ändern einen Probe-Sound."),
            Tab.Control => Loc.T("Zeigt den erkannten Controller und seine Tastenbilder. Links/Rechts auf „Tastenbilder“ blättert durch die übrigen Controller."),
            Tab.Gameplay => CurrentDifficulty()?.Description,
            Tab.Language => Loc.T("Die Sprache wechselt sofort. Fehlt eine Übersetzung, erscheint der deutsche Text."),
            _ => null,
        };
        if (hint is not null)
            font.DrawCenteredLines(spriteBatch, font.Wrap(hint, panel.Width - 30), centerX, panel.Bottom - 44, Palette.Ash);
    }

    /// <summary>Prozentwert; die Schreibweise ist sprachabhängig ("50 %" gegen "50%").</summary>
    private static string Percent(float ratio) => Loc.T("{0} %", (int)MathF.Round(ratio * 100f));

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
    /// Nutzbarer Bereich des Bildschirms in Fensterpunkten (Menüleiste, Titelleiste und Dock
    /// abgezogen). null, wenn es keinen Bildschirm gibt (CI).
    /// </summary>
    private static Point? UsableDisplayArea()
    {
        try
        {
            DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Point((int)(display.Width * UsableWidthFraction), (int)(display.Height * UsableHeightFraction));
        }
        catch (Exception exception)   // ohne Bildschirm (CI) gibt es keinen Adapter
        {
            Log.Warn($"Bildschirmgröße nicht ermittelbar ({exception.GetType().Name}).");
            return null;
        }
    }

    /// <summary>
    /// Größte ganzzahlige Skalierung der 480x270-Leinwand, die auf den Bildschirm passt.
    /// Mindestens 1 – lieber ein zu großes Fenster als gar kein Bild.
    /// </summary>
    private static int AutoScale(int fallback)
    {
        // "is Point area" = Pattern Matching: prüft auf nicht-null und entpackt in einem Schritt
        if (UsableDisplayArea() is not Point area) return fallback;
        return Math.Max(1, Math.Min(area.X / CirclesGame.VirtualWidth, area.Y / CirclesGame.VirtualHeight));
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

    /// <summary>
    /// Fenstergröße beim Anwenden der Einstellungen. "Auto" füllt den nutzbaren Bildschirm im
    /// Seitenverhältnis 16:9 – auch mit krummem Faktor, denn <see cref="CirclesGame"/> skaliert
    /// scharf auf jede Größe. Ohne das bliebe z. B. auf einem 1440x900-Mac nur ein 960x540-Fenster.
    /// Ein fester Faktor liefert weiterhin das exakte Vielfache (pixelgenau).
    /// </summary>
    public static Point WindowSize(GameContext context, GameSettings settings)
    {
        if (settings.ScreenScale <= 0 && UsableDisplayArea() is Point area)
        {
            float fitScale = MathF.Min(area.X / (float)CirclesGame.VirtualWidth, area.Y / (float)CirclesGame.VirtualHeight);
            fitScale = MathF.Max(1f, fitScale);
            return new Point((int)(CirclesGame.VirtualWidth * fitScale), (int)(CirclesGame.VirtualHeight * fitScale));
        }
        int scale = EffectiveScale(context, settings);
        return new Point(CirclesGame.VirtualWidth * scale, CirclesGame.VirtualHeight * scale);
    }

    public static void Apply(GameContext context, GameSettings settings)
    {
        if (_manager is null) return;
        Point size = WindowSize(context, settings);
        int width = size.X;
        int height = size.Y;

        _manager.PreferredBackBufferWidth = width;
        _manager.PreferredBackBufferHeight = height;
        _manager.SynchronizeWithVerticalRetrace = settings.VSync;
        // false = randloses Vollbild in der Desktop-Auflösung statt echtem Moduswechsel. Auf macOS
        // (und bei mehreren Monitoren) wechselt so nicht der ganze Bildschirm die Auflösung, und
        // Cmd+Tab / Mission Control funktionieren wie bei jeder anderen App.
        _manager.HardwareModeSwitch = false;
        // Nur tatsächlich umschalten, wenn der gewünschte Zustand abweicht (Toggle = Umschalter!)
        if (_manager.IsFullScreen != settings.Fullscreen) _manager.ToggleFullScreen();
        _manager.ApplyChanges();

        // Zurücklesen: Das Betriebssystem darf die Anforderung ablehnen. Ohne diese Zeile blieb
        // völlig unsichtbar, dass das Fenster kleiner ist als angefordert.
        Rectangle actual = _manager.GraphicsDevice.PresentationParameters.Bounds;
        string wanted = settings.ScreenScale <= 0 ? "Auto" : $"{settings.ScreenScale}x -> {EffectiveScale(context, settings)}x";
        if (settings.Fullscreen || (actual.Width == width && actual.Height == height))
            Log.Info($"Bildschirm: {wanted}, Fenster {actual.Width}x{actual.Height}.");
        else
            Log.Warn($"Bildschirm: {wanted} angefordert ({width}x{height}), erhalten {actual.Width}x{actual.Height} – es bleibt ein Rand.");
    }
}