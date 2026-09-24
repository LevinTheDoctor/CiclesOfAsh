using CirclesOfAsh.Progression;

namespace CirclesOfAsh.Persistence;

/// <summary>
/// Repository-Pattern: Die Spiellogik kennt nur diese Schnittstelle, nicht SQLite.
/// Alternative Speicherformen (JSON-Datei, Cloud) = neue Klasse, die das Interface implementiert,
/// und eine Zeile in GameContext.Create ändern.
/// </summary>
public interface ISaveRepository
{
    MetaState LoadMeta();
    void SaveMeta(MetaState meta);
    RunState? LoadRun();
    void SaveRun(RunState run);
    void DeleteRun();

    /// <summary>
    /// Lädt die Einstellungen. Fehlende Schlüssel behalten den Standardwert aus
    /// <see cref="GameSettings"/>; eine ScreenScale von 0 bedeutet "nie gesetzt".
    /// Der Aufrufer ruft anschließend <see cref="GameSettings.Sanitize"/>.
    /// </summary>
    GameSettings LoadSettings();
    void SaveSettings(GameSettings settings);
    List<HubDecoPlacement> LoadHubDeco();
    void SaveHubDeco(IEnumerable<HubDecoPlacement> placements);
    List<PetState> LoadPets();
    void SavePets(IEnumerable<PetState> pets);
    Dictionary<string, int> LoadCollectibles();
    void SaveCollectibles(IReadOnlyDictionary<string, int> counts);
}

/// <summary>Ein platziertes Deko-Objekt im Heimwelt-Hub (Rasterposition in Kacheln).</summary>
public sealed record HubDecoPlacement(string PropId, int TileX, int TileY);

/// <summary>
/// Haustier-Daten eines Begleiters: Name, Loyalität (0..100) und gefütterte Snacks.
/// Loyalität wächst durch Streicheln/Füttern und gibt kleine permanente Boni.
/// </summary>
public sealed class PetState
{
    public PetState(string companionId, string name, int loyalty, int fed)
    {
        CompanionId = companionId;
        Name = name;
        Loyalty = loyalty;
        Fed = fed;
    }

    public string CompanionId { get; }
    public string Name { get; set; }
    /// <summary>0..100. Jede Stufe (20 Punkte) gibt +2 % Macht/Leben über den Begleiter (siehe PlayerFactory).</summary>
    public int Loyalty { get; set; }
    /// <summary>Anzahl gefütterter Seelen-Snacks (je +4 Loyalität).</summary>
    public int Fed { get; set; }
    /// <summary>UtcNow als ISO-String: Streicheln nur einmal pro Tag (echte Haustier-Mechanik).</summary>
    public string? LastPettedAt { get; set; }

    /// <summary>Gewählte Farbfassung (Index in <see cref="Companions.CompanionSkins"/>). 0 = Grundfassung.</summary>
    public int Skin { get; set; }

    /// <summary>Loyalitäts-Stufe 0..5 (jede 20 Punkte). Bestimmt den Buff-Betrag.</summary>
    public int LoyaltyStage => Math.Clamp(Loyalty / 20, 0, 5);
}