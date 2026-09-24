using System.Diagnostics.CodeAnalysis;
using CirclesOfAsh.Assets;
using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Modding;

namespace CirclesOfAsh.Definitions;

/// <summary>
/// Generische Sammlung von Definitionen, indiziert nach ID. "where T : class, IDefinition" ist eine
/// Typ-Einschränkung (Constraint): T muss ein Referenztyp sein UND IDefinition implementieren.
/// </summary>
public sealed class DefinitionSet<T> where T : class, IDefinition
{
    private readonly Dictionary<string, T> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _insertionOrder = new();

    public int Count => _byId.Count;
    public IEnumerable<T> All => _insertionOrder.Select(id => _byId[id]);

    /// <summary>Upsert = Update or Insert: Mods überschreiben gleiche IDs, neue IDs werden angehängt.</summary>
    public void Upsert(T definition)
    {
        if (!_byId.ContainsKey(definition.Id)) _insertionOrder.Add(definition.Id);
        _byId[definition.Id] = definition;
    }

    public bool Contains(string id) => _byId.ContainsKey(id);

    public T Get(string id) => _byId.TryGetValue(id, out T? definition)
        ? definition
        : throw new KeyNotFoundException($"{typeof(T).Name} mit der ID '{id}' ist nicht definiert.");

    /// <summary>Nicht-wurfende Variante für optionale Daten (z. B. Schwierigkeits-Fallback).</summary>
    /// <remarks>NotNullWhen(true): im Erfolgsfall ist "definition" garantiert gesetzt – der Aufrufer braucht kein "!".</remarks>
    public bool TryGet(string id, [NotNullWhen(true)] out T? definition) => _byId.TryGetValue(id, out definition);
}

/// <summary>
/// Lädt alle Spieldaten aus Content/Data (und Mods) und prüft sie auf Konsistenz.
/// Fehler werden gesammelt und gemeinsam gemeldet -> ein Durchlauf zeigt alle Tippfehler.
/// </summary>
public sealed class DefinitionRegistry
{
    public DefinitionSet<ClassDefinition> Classes { get; } = new();
    public DefinitionSet<AbilityDefinition> Abilities { get; } = new();
    public DefinitionSet<EnemyDefinition> Enemies { get; } = new();
    public DefinitionSet<CompanionDefinition> Companions { get; } = new();
    public DefinitionSet<UpgradeDefinition> Upgrades { get; } = new();
    public DefinitionSet<WorldDefinition> Worlds { get; } = new();
    public DefinitionSet<ItemDefinition> Items { get; } = new();
    public DefinitionSet<PropDefinition> Props { get; } = new();
    public DefinitionSet<RoomThemeDefinition> Themes { get; } = new();
    public DefinitionSet<MissionDefinition> Missions { get; } = new();
    public DefinitionSet<DialogDefinition> Dialogs { get; } = new();
    public DefinitionSet<ChatterDefinition> Chatter { get; } = new();
    /// <summary>Tutorial-Schritte in Dateireihenfolge – die ist zugleich die Abfolge im Spiel.</summary>
    public DefinitionSet<TutorialStepDefinition> Tutorial { get; } = new();
    public DefinitionSet<NpcDefinition> Npcs { get; } = new();
    public DefinitionSet<DifficultyDefinition> Difficulties { get; } = new();
    public DefinitionSet<ControllerProfileDefinition> ControllerProfiles { get; } = new();
    public BalanceDefinition Balance { get; private set; } = new();
    public AppearanceDefinition Appearance { get; private set; } = new();
    public IReadOnlyList<string> Tips { get; private set; } = Array.Empty<string>();

    public static DefinitionRegistry Load(ContentLocator locator)
    {
        var registry = new DefinitionRegistry();
        LoadInto(locator, "Data/classes.json", registry.Classes);
        LoadInto(locator, "Data/abilities.json", registry.Abilities);
        LoadInto(locator, "Data/enemies.json", registry.Enemies);
        LoadInto(locator, "Data/companions.json", registry.Companions);
        LoadInto(locator, "Data/upgrades.json", registry.Upgrades);
        LoadInto(locator, "Data/worlds.json", registry.Worlds);
        LoadInto(locator, "Data/items.json", registry.Items);
        LoadInto(locator, "Data/props.json", registry.Props);
        LoadInto(locator, "Data/themes.json", registry.Themes);
        LoadInto(locator, "Data/missions.json", registry.Missions);
        LoadInto(locator, "Data/dialogs.json", registry.Dialogs);
        LoadInto(locator, "Data/chatter.json", registry.Chatter);
        LoadInto(locator, "Data/tutorial.json", registry.Tutorial);
        LoadInto(locator, "Data/npcs.json", registry.Npcs);
        LoadInto(locator, "Data/difficulties.json", registry.Difficulties);
        LoadInto(locator, "Data/controllers.json", registry.ControllerProfiles);
        // Einzelobjekte: die Datei mit der höchsten Priorität gewinnt komplett
        foreach (string path in locator.FindAllLayered("Data/balance.json")) registry.Balance = JsonDefaults.Load<BalanceDefinition>(path);
        foreach (string path in locator.FindAllLayered("Data/appearance.json")) registry.Appearance = JsonDefaults.Load<AppearanceDefinition>(path);
        // Tipps werden über alle Ebenen gesammelt (Mods können eigene ergänzen)
        registry.Tips = locator.FindAllLayered("Data/tips.json").SelectMany(path => JsonDefaults.Load<List<string>>(path)).ToList();
        return registry;
    }

    // Generische Methode: derselbe Code lädt Klassen, Gegner, Welten ... (DRY)
    private static void LoadInto<T>(ContentLocator locator, string relativePath, DefinitionSet<T> target)
        where T : class, IDefinition
    {
        foreach (string path in locator.FindAllLayered(relativePath))
        {
            foreach (T definition in JsonDefaults.Load<List<T>>(path)) target.Upsert(definition);
        }
    }

    public void Validate(BehaviorRegistry behaviors, AssetManager assets)
    {
        var errors = new List<string>();

        // Lokale Funktionen: nur innerhalb von Validate sichtbar, halten den Code lesbar
        void Require(bool condition, string message)
        {
            if (!condition) errors.Add(message);
        }

        void WarnIfSpriteMissing(string spriteId, string owner)
        {
            if (!string.IsNullOrEmpty(spriteId) && !assets.HasSpriteSheet(spriteId))
                Log.Warn($"{owner}: Spritesheet '{spriteId}' fehlt im Manifest.");
        }

        Require(Classes.Count > 0, "Es ist keine Klasse definiert (Data/classes.json).");
        Require(Worlds.Count > 0, "Es ist keine Welt definiert (Data/worlds.json).");

        foreach (ClassDefinition playerClass in Classes.All)
        {
            WarnIfSpriteMissing(playerClass.OutfitSprite, $"Klasse '{playerClass.Id}'");
            WarnIfSpriteMissing(playerClass.AccentSprite, $"Klasse '{playerClass.Id}'");
            foreach (string statName in playerClass.BaseStats.Keys)
                Require(StatSheet.TryParse(statName, out _), $"Klasse '{playerClass.Id}': unbekannter Stat '{statName}'.");
            // Concat verbindet zwei Listen zu einer Sequenz, Distinct entfernt Duplikate
            foreach (string abilityId in playerClass.StartingAbilities.Concat(playerClass.AbilityPool).Distinct())
                Require(Abilities.Contains(abilityId), $"Klasse '{playerClass.Id}': Fähigkeit '{abilityId}' existiert nicht.");
            Require(playerClass.StartingArmor.Length == 0 || Items.Contains(playerClass.StartingArmor),
                $"Klasse '{playerClass.Id}': Startrüstung '{playerClass.StartingArmor}' existiert nicht.");
        }

        foreach (AbilityDefinition ability in Abilities.All)
        {
            Require(behaviors.HasAbility(ability.Behavior), $"Fähigkeit '{ability.Id}': Behavior '{ability.Behavior}' unbekannt.");
            WarnIfSpriteMissing(ability.Sprite, $"Fähigkeit '{ability.Id}'");
        }

        foreach (EnemyDefinition enemy in Enemies.All)
        {
            WarnIfSpriteMissing(enemy.SpriteSheet, $"Gegner '{enemy.Id}'");
            Require(behaviors.HasEnemyBrain(enemy.Brain), $"Gegner '{enemy.Id}': Brain '{enemy.Brain}' unbekannt.");
            foreach (string attack in enemy.Phases.SelectMany(phase => phase.Attacks))
                Require(behaviors.HasBossAttack(attack), $"Boss '{enemy.Id}': Angriff '{attack}' unbekannt.");
            Require(string.IsNullOrEmpty(enemy.SummonEnemy) || Enemies.Contains(enemy.SummonEnemy),
                $"Boss '{enemy.Id}': Beschwörung '{enemy.SummonEnemy}' existiert nicht.");
        }

        foreach (CompanionDefinition companion in Companions.All)
            Require(behaviors.HasCompanion(companion.Behavior), $"Begleiter '{companion.Id}': Behavior '{companion.Behavior}' unbekannt.");

        // Ein Tippfehler im Filter würde die Zeile sonst lautlos nie erreichen.
        foreach (ChatterDefinition chatter in Chatter.All)
            foreach (ChatterLineDefinition line in chatter.Lines)
            {
                Require(line.Companion.Length == 0 || Companions.Contains(line.Companion),
                    $"Zwischenruf '{chatter.Id}': Begleiter '{line.Companion}' existiert nicht.");
                Require(line.Behavior.Length == 0 || behaviors.HasCompanion(line.Behavior),
                    $"Zwischenruf '{chatter.Id}': Begleiter-Verhalten '{line.Behavior}' unbekannt.");
                Require(line.Text.Length > 0, $"Zwischenruf '{chatter.Id}': leere Zeile.");
            }

        // Ein Tippfehler im Auslöser waere hier besonders teuer: Der Schritt wuerde nie bestanden
        // und der Spieler haenge fuer immer an derselben Aufforderung fest.
        var knownTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "wait", "move", "jump", "crouch", "attack", "block", "spin", "interact", "kill" };
        foreach (TutorialStepDefinition step in Tutorial.All)
        {
            Require(knownTriggers.Contains(step.Trigger),
                $"Tutorial-Schritt '{step.Id}': Auslöser '{step.Trigger}' ist dem Code unbekannt.");
            Require(step.Text.Length > 0, $"Tutorial-Schritt '{step.Id}': kein Text.");
        }

        foreach (UpgradeDefinition upgrade in Upgrades.All)
            Require(StatSheet.TryParse(upgrade.Stat, out _), $"Upgrade '{upgrade.Id}': unbekannter Stat '{upgrade.Stat}'.");

        foreach (WorldDefinition world in Worlds.All)
        {
            Require(world.Circles.Count > 0, $"Welt '{world.Id}' hat keine Kreise.");
            foreach (CircleDefinition circle in world.Circles)
            {
                Require(circle.EnemyPool.Count > 0, $"Kreis '{circle.Id}': Gegnerpool ist leer.");
                foreach (SpawnWeight spawn in circle.EnemyPool)
                    Require(Enemies.Contains(spawn.Enemy), $"Kreis '{circle.Id}': Gegner '{spawn.Enemy}' existiert nicht.");
                Require(Enemies.Contains(circle.Boss), $"Kreis '{circle.Id}': Boss '{circle.Boss}' existiert nicht.");
                Require(string.IsNullOrEmpty(circle.BossReward) || Abilities.Contains(circle.BossReward),
                    $"Kreis '{circle.Id}': Belohnung '{circle.BossReward}' existiert nicht.");
                foreach (ThemeWeight theme in circle.Themes)
                    Require(Themes.Contains(theme.Theme), $"Kreis '{circle.Id}': Raumthema '{theme.Theme}' existiert nicht.");
                foreach (string puzzle in circle.Puzzles)
                    Require(behaviors.HasPuzzle(puzzle), $"Kreis '{circle.Id}': Rätsel '{puzzle}' ist nicht registriert.");
                foreach (string collectible in circle.Collectibles)
                    Require(Items.Contains(collectible), $"Kreis '{circle.Id}': Sammelobjekt '{collectible}' existiert nicht.");
                if (circle.Prison is { } prison)   // Property-Pattern: nicht null -> in Variable "prison"
                {
                    Require(Enemies.Contains(prison.MiniBoss), $"Kreis '{circle.Id}': Mini-Boss '{prison.MiniBoss}' existiert nicht.");
                    Require(string.IsNullOrEmpty(prison.Guards) || Enemies.Contains(prison.Guards), $"Kreis '{circle.Id}': Wache '{prison.Guards}' existiert nicht.");
                    Require(string.IsNullOrEmpty(prison.CompanionReward) || Companions.Contains(prison.CompanionReward),
                        $"Kreis '{circle.Id}': Begleiter '{prison.CompanionReward}' existiert nicht.");
                }
            }
        }

        foreach (ItemDefinition item in Items.All)
        {
            // Ein falsch benanntes Rüstungssprite fiele sonst nirgends auf: Die Ebene bliebe
            // einfach weg und die Rüstung wäre unsichtbar, ohne eine einzige Fehlermeldung.
            WarnIfSpriteMissing(item.Sprite, $"Item '{item.Id}'");
            foreach (StatModifierDefinition modifier in item.Modifiers)
                Require(StatSheet.TryParse(modifier.Stat, out _), $"Item '{item.Id}': unbekannter Stat '{modifier.Stat}'.");
        }

        foreach (PropDefinition prop in Props.All)
        {
            Require(behaviors.HasProp(prop.Behavior), $"Prop '{prop.Id}': Behavior '{prop.Behavior}' unbekannt.");
            WarnIfSpriteMissing(prop.SpriteSheet, $"Prop '{prop.Id}'");
        }

        foreach (RoomThemeDefinition theme in Themes.All)
            foreach (ThemePropRule rule in theme.Props)
                Require(Props.Contains(rule.Prop), $"Raumthema '{theme.Id}': Prop '{rule.Prop}' existiert nicht.");

        foreach (MissionDefinition mission in Missions.All)
            Require(mission.Count > 0, $"Mission '{mission.Id}': Count muss größer als 0 sein.");

        foreach (DialogDefinition dialog in Dialogs.All)
        {
            Require(dialog.Lines.Count > 0, $"Dialog '{dialog.Id}': keine Zeilen definiert.");
            foreach (DialogLineDefinition line in dialog.Lines)
            foreach (DialogChoiceDefinition choice in line.Choices)
                Require(choice.Next.Length == 0 || dialog.Lines.Any(candidate => candidate.Id == choice.Next),
                    $"Dialog '{dialog.Id}': Ziel '{choice.Next}' existiert nicht.");
        }

        foreach (NpcDefinition npc in Npcs.All)
        {
            WarnIfSpriteMissing(npc.SpriteSheet, $"NPC '{npc.Id}'");
            Require(!string.IsNullOrEmpty(npc.DialogId) && Dialogs.Contains(npc.DialogId), $"NPC '{npc.Id}': Dialog '{npc.DialogId}' existiert nicht.");
        }

        Require(Difficulties.Contains("devout"), "difficulties.json: die Standard-Stufe 'devout' fehlt.");

        // Genau ein Auffangprofil (leeres "match") – sonst hinge die Wahl von der Dateireihenfolge ab.
        Require(ControllerProfiles.All.Count(profile => profile.Match.Count == 0) == 1,
            "controllers.json: es muss genau ein Profil ohne \"match\" geben (Auffangprofil).");

        Require(Appearance.SkinTones.Count > 0 && Appearance.HairStyles.Count > 0, "appearance.json: Hauttöne und Frisuren dürfen nicht leer sein.");
        WarnIfSpriteMissing(Appearance.BodySprite, "Aussehen (Körper)");

        if (errors.Count > 0)
        {
            string message = "Fehlerhafte Spieldaten:\n  - " + string.Join("\n  - ", errors);
            Log.Error(message);
            throw new InvalidDataException(message);
        }
        Log.Info($"Daten geladen: {Classes.Count} Klassen, {Abilities.Count} Fähigkeiten, {Enemies.Count} Gegner, {Worlds.Count} Welten.");
    }
}
