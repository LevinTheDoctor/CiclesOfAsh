namespace CirclesOfAsh.Progression;

/// <summary>
/// Spieler-Einstellungen (Optionsmenü). Werden in der SQLite-DB (Tabelle settings) persistiert.
/// Bildschirm und Audio lassen sich getrennt regeln; Tastatur/Controller bleiben vorerst fest.
/// </summary>
public sealed class GameSettings
{
    // ------------------------------------------------------------------ Bildschirm
    /// <summary>Ganzzahliger Skalierungsfaktor der virtuellen 480x270-Auflösung (1..6).</summary>
    public int ScreenScale { get; set; } = 3;
    public bool Fullscreen { get; set; }
    public bool VSync { get; set; } = true;

    // ------------------------------------------------------------------ Audio
    /// <summary>0..1 – wirkt auf ALLE Klänge (Musik und SFX) als Obergrenze.</summary>
    public float MasterVolume { get; set; } = 0.8f;
    /// <summary>0..1 – nur der Soundtrack.</summary>
    public float MusicVolume { get; set; } = 0.6f;
    /// <summary>0..1 – nur Soundeffekte.</summary>
    public float SfxVolume { get; set; } = 0.8f;

    // ------------------------------------------------------------------ Gameplay
    /// <summary>0..1 – hebt die Grundhelligkeit in Verliesen an (Licht-Regler im Optionsmenü).</summary>
    public float AmbientLift { get; set; } = 0.32f;
    /// <summary>0 = aus, 1 = volle Vibration. Controller-Feedback bei Treffern und Boss-Angriffen.</summary>
    public float RumbleIntensity { get; set; } = 0.6f;
    public bool ShowDamageNumbers { get; set; } = true;
    /// <summary>Id aus difficulties.json ("devout" = Standard). Wirkt ab dem nächsten Dungeon.</summary>
    public string DifficultyId { get; set; } = "devout";

    /// <summary>Klemmt alle Werte in gültige Bereiche (nach dem Laden aus der DB).</summary>
    public void Sanitize()
    {
        ScreenScale = Math.Clamp(ScreenScale, 1, 6);
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
        SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
        AmbientLift = Math.Clamp(AmbientLift, 0f, 1f);
        RumbleIntensity = Math.Clamp(RumbleIntensity, 0f, 1f);
        DifficultyId = string.IsNullOrWhiteSpace(DifficultyId) ? "devout" : DifficultyId.Trim();
    }
}