using CirclesOfAsh.Assets;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Persistence;
using CirclesOfAsh.Pets;

namespace CirclesOfAsh.Companions;

/// <summary>
/// Farbfassungen ("Skins") einer Begleitseele.
///
/// Die Sprites liegen im Manifest nach einer festen Regel: die Grundfassung unter der ID aus
/// <c>companions.json</c>, die Varianten unter <c>companion.&lt;begleiter-id&gt;.&lt;fassung&gt;</c>.
/// Dadurch braucht eine neue Fassung KEINE Code-Änderung – es genügt ein Manifest-Eintrag, und
/// fehlt eine Fassung, fällt die Liste hier einfach kürzer aus. Genau deshalb kann der
/// Grafik-Agent seine Dateien erweitern, ohne dass hier etwas nachgezogen werden muss.
/// </summary>
public static class CompanionSkins
{
    /// <summary>Suffix im Manifest und der Name, der dem Spieler angezeigt wird.</summary>
    private static readonly (string Suffix, string Name)[] Variants =
    {
        ("", "Grundfassung"),
        (".pale", "Bleich"),
        (".deep", "Tiefdunkel"),
    };

    /// <summary>Alle Fassungen, für die wirklich ein Spritesheet existiert (Reihenfolge = Zyklus).</summary>
    public static List<(string SheetId, string Name)> Available(AssetManager assets, CompanionDefinition definition)
    {
        var result = new List<(string, string)>();
        foreach (var (suffix, name) in Variants)
        {
            string sheetId = suffix.Length == 0 ? definition.SpriteSheet : $"companion.{definition.Id}{suffix}";
            if (assets.HasSpriteSheet(sheetId)) result.Add((sheetId, name));
        }
        // Ohne jede Fassung wäre der Begleiter unsichtbar – die Grundfassung bleibt drin,
        // GetSpriteSheet meldet dann den fehlenden Eintrag an einer Stelle.
        if (result.Count == 0) result.Add((definition.SpriteSheet, Variants[0].Name));
        return result;
    }

    /// <summary>Gewählter Index, auf die vorhandenen Fassungen begrenzt.</summary>
    public static int IndexOf(GameContext context, CompanionDefinition definition)
    {
        int count = Available(context.Assets, definition).Count;
        int stored = PetService.GetPet(context, definition.Id)?.Skin ?? 0;
        return count == 0 ? 0 : Math.Clamp(stored, 0, count - 1);
    }

    /// <summary>Spritesheet-ID, mit der dieser Begleiter gerade gezeichnet wird.</summary>
    public static string SheetOf(GameContext context, CompanionDefinition definition)
    {
        List<(string SheetId, string Name)> skins = Available(context.Assets, definition);
        return skins[Math.Clamp(IndexOf(context, definition), 0, skins.Count - 1)].SheetId;
    }

    /// <summary>
    /// Schaltet auf die nächste Fassung weiter und speichert sie. Gibt den Text für den Dialog
    /// zurück (null, wenn es nichts zu wechseln gibt).
    /// </summary>
    public static string? Cycle(GameContext context, CompanionDefinition definition)
    {
        List<(string SheetId, string Name)> skins = Available(context.Assets, definition);
        PetState? pet = PetService.GetPet(context, definition.Id);
        if (pet is null)
        {
            // Erst in diesem Lauf befreit: der Haustier-Eintrag entsteht sonst erst beim naechsten Start.
            PetService.EnsurePets(context);
            pet = PetService.GetPet(context, definition.Id);
            if (pet is null) return null;
        }
        if (skins.Count < 2) return $"{pet.Name} kennt nur diese eine Gestalt.";

        pet.Skin = (IndexOf(context, definition) + 1) % skins.Count;
        PetService.Save(context);
        context.Audio.Play("unseal", 0.4f, 0.4f);
        return $"{pet.Name} nimmt eine neue Gestalt an: {skins[pet.Skin].Name}.";
    }
}
