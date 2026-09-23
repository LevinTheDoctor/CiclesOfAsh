using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Pets;
using CirclesOfAsh.Progression;

namespace CirclesOfAsh.Dialogs;

/// <summary>
/// Wertet die Dialog-Bedingungen ("If") aus und führt Choice-Effekte aus:
///   blessing      – einmaliger Lauf-Buff (Macht +10 %, angewandt auf die Stat-Quelle "blessing")
///   accept_mission – eine verfügbare Bitte im DUNGEON annehmen (das größte Quest-Ärgernis)
///   pet_pet / feed_pet – Haustier-Interaktion (nur im Hub, siehe PetService)
/// </summary>
public sealed class DialogService
{
    private readonly GameContext _context;
    private readonly Dictionary<string, bool> _blessingsGiven = new(StringComparer.OrdinalIgnoreCase);

    public DialogService(GameContext context) => _context = context;

    /// <summary>Bedingung eines Dialog-Knotens prüfen. null/leer = trifft immer.</summary>
    public bool MatchesCondition(string? condition, Entities.Npc? npc)
    {
        switch (condition)
        {
            case null:
            case "":
                return true;
            case "blessed":
                // "blessed": Nur wenn der NPC einen Segen gibt und er noch nicht genommen wurde.
                return npc is { Definition.GivesBlessing: true } && npc.Tag != "blessed";
            case "poor":
                return _context.Progression.Meta.Believers < 50;
            case "loyal":
                return npc is not null && PetService.GetLoyalty(_context, npc.Definition.Id) >= 60;
            default:
                return true;
        }
    }

    /// <summary>Erste Zeile, deren Bedingung passt (Reihenfolge in JSON = Priorität).</summary>
    public DialogLineDefinition? ResolveEntry(DialogDefinition dialog, Entities.Npc? npc) =>
        dialog.Lines.FirstOrDefault(line => MatchesCondition(line.If, npc));

    public DialogLineDefinition? FindLine(DialogDefinition dialog, string id) =>
        dialog.Lines.FirstOrDefault(line => line.Id == id);

    /// <summary>
    /// Führt den Effekt einer Wahl aus. Gibt einen kurzen Rückmeldetext zurück (oder null).
    /// </summary>
    public string? ApplyEffect(DialogChoiceDefinition choice, Entities.Npc? npc)
    {
        switch (choice.Effect)
        {
            case "blessing" when npc is not null:
                if (npc.Tag == "blessed") return null;
                npc.Tag = "blessed";
                _context.Audio.Play("unseal", 0.7f, 0.2f);
                return "Du fühlst Wärme in deinen Adern. (+10% Macht für diesen Lauf)";
            case "accept_mission":
            {
                MissionDefinition? mission = _context.Progression.Missions.Available.FirstOrDefault(m => m.Id == choice.Target);
                if (mission is null) return null;
                if (!_context.Progression.Missions.Accept(mission))
                    return $"Du trägst bereits zu viele Bitten. ({_context.Progression.Missions.MaxActive} gleichzeitig)";
                _context.Progression.SaveMeta();
                _context.Audio.Play("levelup", 0.4f, 0.3f);
                return $"Bitte angenommen: {mission.Title}";
            }
            case "pet_pet":
                return PetService.Pet(_context, npc?.Definition.Id ?? "");
            case "feed_pet":
                return PetService.Feed(_context, npc?.Definition.Id ?? "");
            default:
                return null;
        }
    }

    /// <summary>Segen als Stat-Bonus auf den Spieler anwenden (aufrufen bei Player-Aufbau + nach Dialog).</summary>
    public void ApplyBlessings(Combat.StatSheet stats)
    {
        if (_blessingsGiven.Values.Any())
            stats.AddPercent(Combat.StatType.Might, 0.10f);
    }

    public void MarkBlessingReceived(string npcTag)
    {
        if (npcTag == "blessed") _blessingsGiven["blessing"] = true;
    }
}