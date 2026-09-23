using CirclesOfAsh.Core;
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

    /// <summary>Sorgt dafür, dass jeder freigeschaltete Begleiter einen PetState hat (Standardname).</summary>
    public static void EnsurePets(GameContext context)
    {
        foreach (string companionId in context.Progression.Meta.UnlockedCompanions)
        {
            if (GetPet(context, companionId) is not null) continue;
            if (!context.Definitions.Companions.Contains(companionId)) continue;
            string name = context.Definitions.Companions.Get(companionId).Name;
            context.Pets.Add(new PetState(companionId, name, 10, 0));
        }
        Save(context);
    }

    public static string Rename(GameContext context, string companionId, string name)
    {
        PetState? pet = GetPet(context, companionId);
        if (pet is null) return "";
        string trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 14) return "Der Name muss 1 bis 14 Zeichen haben.";
        pet.Name = trimmed;
        Save(context);
        return $"Deine Seele heißt jetzt {trimmed}.";
    }

    /// <summary>Streicheln: +Loyalität, Herz-Partikel-Sound. Nur einmal pro Session pro Seele.</summary>
    public static string? Pet(GameContext context, string companionId)
    {
        PetState? pet = GetPet(context, companionId);
        if (pet is null) return null;
        if (_pettedThisSession.Contains(companionId))
            return $"{pet.Name} wurde heute schon gestreichelt und genießt es sichtbar.";
        _pettedThisSession.Add(companionId);
        pet.Loyalty = Math.Min(100, pet.Loyalty + PetLoyaltyGain);
        context.Audio.Play("pickup", 0.3f, 0.5f);
        Save(context);
        return $"{pet.Name} schmiegelt sich an dich. (+{PetLoyaltyGain} Treue)";
    }

    /// <summary>Füttern: kostet Seelen (Meta-Währung wäre zu teuer – wir nutzen Lauf-XP-artige "Fed"-Zähler + Gläubige).</summary>
    public static string? Feed(GameContext context, string companionId)
    {
        PetState? pet = GetPet(context, companionId);
        if (pet is null) return null;
        long believers = context.Progression.Meta.Believers;
        if (believers < FeedSoulCost)
            return $"Du brauchst {FeedSoulCost} Gläubige als Snack-Opfergabe.";
        context.Progression.Meta.Believers -= FeedSoulCost;
        pet.Fed++;
        pet.Loyalty = Math.Min(100, pet.Loyalty + FeedLoyaltyGain);
        context.Progression.SaveMeta();
        context.Audio.Play("levelup", 0.4f, -0.3f);
        Save(context);
        return $"{pet.Name} verschlingt die Seelen glücklich. (+{FeedLoyaltyGain} Treue)";
    }

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