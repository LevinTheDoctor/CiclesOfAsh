using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Optionsmenü: Bildschirm (Größe/Vollbild), Audio (Master/Musik/SFX getrennt),
/// Gameplay (Licht, Vibration, Schadenszahlen, Schwierigkeit). Alles sofort persistent.
/// Steuerung komplett per GameAction -> Tastatur UND Controller.
/// </summary>
public sealed class SettingsScene : SceneBase
{
    private enum Row { ScreenScale, Fullscreen, VSync, Master, Music, Sfx, AmbientLift, Rumble, DamageNumbers, Difficulty, Back }
    private readonly Row[] _rows = Enum.GetValues<Row>();
    private int _rowIndex;
    private readonly List<DifficultyDefinition> _difficulties;

    public SettingsScene(GameContext context) : base(context)
    {
        _difficulties = context.Definitions.Difficulties.All.ToList();
    }

    private Row CurrentRow => _rows[_rowIndex];
    private GameSettings Settings => Context.Settings;

    public override bool IsOverlay => true;

    public override void Update(float deltaSeconds)
    {
        InputState input = Context.Input;
        if (input.WasPressed(GameAction.Cancel))
        {
            Context.SaveSettings();
            Context.Scenes.Pop();
            return;
        }

        if (input.WasPressed(GameAction.Down)) Move(1);
        if (input.WasPressed(GameAction.Up)) Move(-1);

        int change = input.WasPressed(GameAction.Right) ? 1 : input.WasPressed(GameAction.Left) ? -1 : 0;
        if (change != 0) ChangeValue(change);

        if (input.WasPressed(GameAction.Confirm) && CurrentRow == Row.Back)
        {
            Context.SaveSettings();
            Context.Scenes.Pop();
        }
    }

    private void Move(int step)
    {
        _rowIndex = (_rowIndex + step + _rows.Length) % _rows.Length;
        Context.Audio.Play("pickup", 0.2f, 0.4f);
    }

    private void ChangeValue(int step)
    {
        switch (CurrentRow)
        {
            case Row.ScreenScale:
                Settings.ScreenScale = Math.Clamp(Settings.ScreenScale + step, 1, 6);
                ApplyScreenSettings();
                break;
            case Row.Fullscreen:
                Settings.Fullscreen = !Settings.Fullscreen;
                ApplyScreenSettings();
                break;
            case Row.VSync:
                Settings.VSync = !Settings.VSync;
                ApplyScreenSettings();
                break;
            case Row.Master:
                Settings.MasterVolume = Math.Clamp(Settings.MasterVolume + step * 0.05f, 0f, 1f);
                break;
            case Row.Music:
                Settings.MusicVolume = Math.Clamp(Settings.MusicVolume + step * 0.05f, 0f, 1f);
                break;
            case Row.Sfx:
                Settings.SfxVolume = Math.Clamp(Settings.SfxVolume + step * 0.05f, 0f, 1f);
                Context.Audio.Play("pickup", 0.5f, 0.3f);   // Probe-Sound
                break;
            case Row.AmbientLift:
                Settings.AmbientLift = Math.Clamp(Settings.AmbientLift + step * 0.05f, 0f, 1f);
                break;
            case Row.Rumble:
                Settings.RumbleIntensity = Math.Clamp(Settings.RumbleIntensity + step * 0.1f, 0f, 1f);
                break;
            case Row.DamageNumbers:
                Settings.ShowDamageNumbers = !Settings.ShowDamageNumbers;
                break;
            case Row.Difficulty:
            {
                int index = Math.Max(0, _difficulties.FindIndex(candidate => candidate.Id == Settings.DifficultyId));
                index = (index + step + _difficulties.Count) % _difficulties.Count;
                Settings.DifficultyId = _difficulties[index].Id;
                Context.Progression.SetDifficulty(Settings.DifficultyId);
                break;
            }
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

        float y = panel.Top + 26;
        foreach (Row row in _rows)
        {
            if (row == Row.Back)
            {
                font.DrawCentered(spriteBatch, CurrentRow == row ? "· Zurück ·" : "Zurück", centerX, panel.Bottom - 14,
                    CurrentRow == row ? Palette.Gold : Palette.Bone);
                break;
            }

            string label = row switch
            {
                Row.ScreenScale => "Bildschirmgröße",
                Row.Fullscreen => "Vollbild",
                Row.VSync => "VSync",
                Row.Master => "Lautstärke",
                Row.Music => "Musik",
                Row.Sfx => "Effekte",
                Row.AmbientLift => "Helligkeit",
                Row.Rumble => "Vibration",
                Row.DamageNumbers => "Schadenszahlen",
                Row.Difficulty => "Schwierigkeit",
                _ => "",
            };
            string value = row switch
            {
                Row.ScreenScale => $"{Settings.ScreenScale}x ({CirclesGame.VirtualWidth * Settings.ScreenScale}x{CirclesGame.VirtualHeight * Settings.ScreenScale})",
                Row.Fullscreen => Settings.Fullscreen ? "An" : "Aus",
                Row.VSync => Settings.VSync ? "An" : "Aus",
                Row.Master => Bar(Settings.MasterVolume),
                Row.Music => Bar(Settings.MusicVolume),
                Row.Sfx => Bar(Settings.SfxVolume),
                Row.AmbientLift => Bar(Settings.AmbientLift),
                Row.Rumble => Bar(Settings.RumbleIntensity),
                Row.DamageNumbers => Settings.ShowDamageNumbers ? "An" : "Aus",
                Row.Difficulty => _difficulties.FirstOrDefault(d => d.Id == Settings.DifficultyId)?.Name ?? "?",
                _ => "",
            };
            bool isSelected = CurrentRow == row;
            Color color = isSelected ? Palette.Gold : Palette.Bone * 0.85f;
            font.DrawShadowed(spriteBatch, label, new Vector2(panel.Left + 12, y), color);
            font.DrawShadowed(spriteBatch, isSelected ? $"‹ {value} ›" : value, new Vector2(panel.Left + 130, y),
                isSelected ? Palette.Faith : Palette.Bone);
            y += font.LineHeight + 3;
        }

        // Beschreibung der gewählten Zeile
        string? hint = CurrentRow switch
        {
            Row.Difficulty => _difficulties.FirstOrDefault(d => d.Id == Settings.DifficultyId)?.Description,
            Row.AmbientLift => "Hellt die Grundhelligkeit der Verliese auf – gegen zu starke Dunkelheit.",
            Row.Music => "Nur der Soundtrack. Wirkt sofort.",
            Row.Sfx => "Nur Soundeffekte. Wirkt sofort.",
            _ => null,
        };
        if (hint is not null)
            font.DrawCenteredLines(spriteBatch, font.Wrap(hint, panel.Width - 30), centerX, panel.Bottom - 44, Palette.Ash);

        font.DrawCentered(spriteBatch, "Hoch/Runter: Zeile · Links/Rechts: ändern · Esc: zurück", centerX, CirclesGame.VirtualHeight - 12, Palette.Ash);
        spriteBatch.End();
    }

    private static string Bar(float ratio)
    {
        int filled = (int)MathF.Round(ratio * 10f);
        return new string('█', filled) + new string('░', 10 - filled);
    }
}

/// <summary>
/// Zentrale Anwendung der Bildschirm-Einstellungen. CirclesGame registriert hier seinen
/// GraphicsDeviceManager (statisch, einmalig) – Szenen können so den Modus ändern, ohne
/// die Game-Klasse zu kennen.
/// </summary>
public static class ScreenSetup
{
    private static GraphicsDeviceManager? _manager;

    public static void Register(GraphicsDeviceManager manager) => _manager = manager;

    public static void Apply(GameContext context, GameSettings settings)
    {
        if (_manager is null) return;
        _manager.PreferredBackBufferWidth = CirclesGame.VirtualWidth * settings.ScreenScale;
        _manager.PreferredBackBufferHeight = CirclesGame.VirtualHeight * settings.ScreenScale;
        _manager.SynchronizeWithVerticalRetrace = settings.VSync;
        // Nur tatsächlich umschalten, wenn der gewünschte Zustand abweicht (Toggle = Umschalter!)
        if (_manager.IsFullScreen != settings.Fullscreen) _manager.ToggleFullScreen();
        _manager.ApplyChanges();
    }
}