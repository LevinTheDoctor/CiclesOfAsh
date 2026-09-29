using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Persistence;
using CirclesOfAsh.Progression;

namespace CirclesOfAsh.Pets;

/// <summary>
/// Haustier-Pflege für Begleitseelen: Namen geben, streicheln, füttern. Loyalität (0..100)
/// wächst durch Zuwendung und gibt pro Stufe (20 Punkte) +2 % Macht/Leben im nächsten Dungeon.
/// Läuft komplett auf den PetState-Daten aus der SQLite-DB (Tabelle pets, Migration V3).
/// </summary>
public static class PetService
{
    private const int FeedSoulCost = 5;
    private const int PetLoyaltyGain = 8;
    private const int FeedLoyaltyGain = 4;
    /// <summary>Streicheln nur einmal pro "Tag" (Spielstart) pro Seele – wie echte Haustiere.</summary>
    private static readonly HashSet<string> _pettedThisSession = new(StringComparer.OrdinalIgnoreCase);

    public static List<PetState> GetPets(GameContext context) => context.Pets;

    public static PetState? GetPet(GameContext context, string companionId) =>
        context.Pets.FirstOrDefault(pet => pet.CompanionId.Equals(companionId, StringComparison.OrdinalIgnoreCase));

    public static int GetLoyalty(GameContext context, string companionId) => GetPet(context, companionId)?.Loyalty ?? 0;

    /// <summary>
    /// Anzeigename einer Seele: der vom Spieler vergebene Name – oder, solange sie noch ihren
    /// Standardnamen trägt, der Name der Seele in der aktuellen Sprache. Gespeichert wird der
    /// deutsche Quellname; ohne diese Unterscheidung hieße die Glutseele im Englischen weiter
    /// "Glutseele", weil ein gespeicherter Name nie übersetzt wird.
    /// </summary>
    public static string DisplayName(GameContext context, CompanionDefinition definition)
    {
        PetState? pet = GetPet(context, definition.Id);
        return pet is null || pet.Name == definition.Name.Source ? definition.Name : pet.Name;
    }

    /// <summary>Sorgt dafür, dass jeder freigeschaltete Begleiter einen PetState hat (Standardname).</summary>
    public static void EnsurePets(GameContext context)
    {
        foreach (string companionId in context.Progression.Meta.UnlockedCompanions)
        {
            if (GetPet(context, companionId) is not null) continue;
            if (!context.Definitions.Companions.Contains(companionId)) continue;
            // Der QUELLname, nicht der übersetzte: siehe DisplayName.
            string name = context.Definitions.Companions.Get(companionId).Name.Source;
            context.Pets.Add(new PetState(companionId, name, 10, 0));
        }
        Save(context);
    }

    public static string Rename(GameContext context, string companionId, string name)
    {
        PetState? pet = GetPet(context, companionId);
        if (pet is null) return "";
        string trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 14) return Loc.T("Der Name muss 1 bis 14 Zeichen haben.");
        pet.Name = trimmed;
        Save(context);
        return Loc.T("Deine Seele heißt jetzt {0}.", trimmed);
    }

    /// <summary>Streicheln: +Loyalität, Herz-Partikel-Sound. Nur einmal pro Session pro Seele.</summary>
    public static string? Pet(GameContext context, string companionId)
    {
        PetState? pet = GetPet(context, companionId);
        if (pet is null) return null;
        string name = NameOf(context, companionId, pet);
        if (_pettedThisSession.Contains(companionId))
            return Loc.T("{0} wurde heute schon gestreichelt und genießt es sichtbar.", name);
        _pettedThisSession.Add(companionId);
        pet.Loyalty = Math.Min(100, pet.Loyalty + PetLoyaltyGain);
        context.Audio.Play("pickup", 0.3f, 0.5f);
        Save(context);
        return Loc.T("{0} schmiegelt sich an dich. (+{1} Treue)", name, PetLoyaltyGain);
    }

    /// <summary>Füttern: kostet Seelen (Meta-Währung wäre zu teuer – wir nutzen Lauf-XP-artige "Fed"-Zähler + Gläubige).</summary>
    public static string? Feed(GameContext context, string companionId)
    {
        PetState? pet = GetPet(context, companionId);
        if (pet is null) return null;
        long believers = context.Progression.Meta.Believers;
        if (believers < FeedSoulCost)
            return Loc.T("Du brauchst {0} Gläubige als Snack-Opfergabe.", FeedSoulCost);
        context.Progression.Meta.Believers -= FeedSoulCost;
        pet.Fed++;
        pet.Loyalty = Math.Min(100, pet.Loyalty + FeedLoyaltyGain);
        context.Progression.SaveMeta();
        context.Audio.Play("levelup", 0.4f, -0.3f);
        Save(context);
        return Loc.T("{0} verschlingt die Seelen glücklich. (+{1} Treue)", NameOf(context, companionId, pet), FeedLoyaltyGain);
    }

    /// <summary>Anzeigename über die Definition, wenn es sie (noch) gibt – sonst der gespeicherte.</summary>
    private static string NameOf(GameContext context, string companionId, PetState pet) =>
        context.Definitions.Companions.TryGet(companionId, out CompanionDefinition? definition)
            ? DisplayName(context, definition)
            : pet.Name;

    /// <summary>Buff aus Loyalität: 2% Macht pro Stufe (0..5). Anwendung in PlayerFactory.</summary>
    public static float LoyaltyMightBonus(GameContext context, IEnumerable<string> activeCompanionIds)
    {
        float bonus = 0f;
        foreach (string companionId in activeCompanionIds)
        {
            PetState? pet = GetPet(context, companionId);
            if (pet is null) continue;
            bonus += pet.LoyaltyStage * 0.02f;
        }
        return bonus;
    }

    public static void Save(GameContext context) => context.Saves.SavePets(context.Pets);
}