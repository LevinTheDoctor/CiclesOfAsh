using CirclesOfAsh.Core;

namespace CirclesOfAsh.Definitions;

// ============================================================================================
// Datenklassen ("Definitionen") = unveränderliche Baupläne, 1:1 aus den JSON-Dateien in Content/Data.
// Sie enthalten KEINE Logik. Das Verhalten steckt in austauschbaren Behavior-Klassen, die über
// einen String-Schlüssel ("Behavior": "projectile") verknüpft werden -> Data-Driven Design.
// ============================================================================================

/// <summary>Gemeinsame Schnittstelle: alles mit einer eindeutigen ID kann in ein <see cref="DefinitionSet{T}"/>.</summary>
public interface IDefinition
{
    string Id { get; }
}

public sealed class ClassDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Kleidungs-Ebene der Klasse (feste Farben) für den Charakter-Editor.</summary>
    public string OutfitSprite { get; init; } = "";
    /// <summary>Einfärbbare Akzent-Ebene (Wappenrock, Stola, Schal).</summary>
    public string AccentSprite { get; init; } = "";
    public Dictionary<string, float> BaseStats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> StartingAbilities { get; init; } = new();
    /// <summary>Fähigkeiten, die beim Level-Up angeboten werden dürfen.</summary>
    public List<string> AbilityPool { get; init; } = new();
}

public enum AbilityActivation
{
    Auto,     // feuert selbstständig, sobald bereit (Vampire-Survivors-Prinzip)
    Manual,   // per Taste (InputAction)
    Passive,  // kein Auslösen; wirkt dauerhaft (Orbit) oder einmalig beim Ausrüsten (Doppelsprung)
}

public sealed class AbilityDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Behavior { get; init; } = "";
    public AbilityActivation Activation { get; init; } = AbilityActivation.Auto;
    public GameAction InputAction { get; init; } = GameAction.AbilityOne;
    public float Cooldown { get; init; } = 1f;
    public float ManaCost { get; init; }
    public float Damage { get; init; }
    public float DamagePerLevel { get; init; }
    public float Range { get; init; } = 160f;
    public float Radius { get; init; } = 24f;
    public float Speed { get; init; } = 220f;
    public int Count { get; init; } = 1;
    /// <summary>Alle n Stufen +1 Projektil/Orb. 0 = nie.</summary>
    public int CountEveryLevels { get; init; }
    public int Pierce { get; init; }
    public float Duration { get; init; }
    public float Knockback { get; init; } = 60f;
    public int MaxLevel { get; init; } = 5;
    public string Sprite { get; init; } = "";
    public string Sound { get; init; } = "";
}

public sealed class BossPhaseDefinition
{
    /// <summary>Phase gilt, solange Leben/Max &lt;= diesem Wert (1.0 = von Anfang an).</summary>
    public float HealthBelow { get; init; } = 1f;
    public List<string> Attacks { get; init; } = new();
    public float SpeedMultiplier { get; init; } = 1f;
    public float PauseBetweenAttacks { get; init; } = 1.2f;
}

public sealed class EnemyDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string SpriteSheet { get; init; } = "";
    public string Brain { get; init; } = "walker";
    public float MaxHealth { get; init; } = 10f;
    public float ContactDamage { get; init; } = 8f;
    public float MoveSpeed { get; init; } = 50f;
    public int Width { get; init; } = 12;
    public int Height { get; init; } = 14;
    public bool IsFlying { get; init; }
    public int SoulValue { get; init; } = 1;
    public float KnockbackResistance { get; init; }
    public string ProjectileSprite { get; init; } = "projectile.enemy_orb";
    public float ProjectileDamage { get; init; } = 6f;
    public float ProjectileSpeed { get; init; } = 120f;
    public float AttackInterval { get; init; } = 2.5f;
    public bool IsBoss { get; init; }
    /// <summary>Mini-Boss: eigene Lebensleiste, bewacht Gefangene. Nutzt ebenfalls Phasen.</summary>
    public bool IsMiniBoss { get; init; }
    /// <summary>Färbung (#RRGGBB) – so kann ein Sprite für mehrere Gegnervarianten dienen.</summary>
    public string Tint { get; init; } = "#FFFFFF";
    public string SummonEnemy { get; init; } = "";
    public List<BossPhaseDefinition> Phases { get; init; } = new();
}

public sealed class CompanionDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string SpriteSheet { get; init; } = "";
    public string Behavior { get; init; } = "";
    public float Power { get; init; } = 5f;
    public float Interval { get; init; } = 2f;
    public float Range { get; init; } = 150f;
    public string ProjectileSprite { get; init; } = "projectile.ember";
    /// <summary>Ab so vielen Gläubigen wird der Begleiter dauerhaft freigeschaltet. -1 = nur durch Befreiung aus einem Kerker.</summary>
    public int UnlockAtBelievers { get; init; }
}

public sealed class UpgradeDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Stat { get; init; } = "";
    public float Amount { get; init; }
    public bool IsPercent { get; init; }
    public int MaxStacks { get; init; } = 5;
}

public sealed class SpawnWeight
{
    public string Enemy { get; init; } = "";
    public int Weight { get; init; } = 1;
}

/// <summary>Ein "Kreis" (Schicht) einer Welt – nach Dantes Inferno. Jeder Kreis hat N Dungeons, der letzte ist der Boss.</summary>
public sealed class CircleDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Lore { get; init; } = "";
    public float Difficulty { get; init; } = 1f;
    public string TileTint { get; init; } = "#FFFFFF";
    public string BackgroundTint { get; init; } = "#FFFFFF";
    public string Tileset { get; init; } = "tiles.limbo";
    public string Background { get; init; } = "background.limbo";
    /// <summary>Grundhelligkeit ohne Lichtquellen. Je tiefer der Kreis, desto dunkler (#RRGGBB).</summary>
    public string AmbientLight { get; init; } = "#9090A0";
    /// <summary>Verfall 0..1: mehr zerbrochene Wände und bröckelnde Plattformen.</summary>
    public float Decay { get; init; }
    /// <summary>Umgebungspartikel: dust | ash | embers | drips | none.</summary>
    public string AmbientParticles { get; init; } = "dust";
    public List<ThemeWeight> Themes { get; init; } = new();
    /// <summary>Mögliche Rätsel vor dem Siegeltor (Schlüssel aus der BehaviorRegistry).</summary>
    public List<string> Puzzles { get; init; } = new();
    public PrisonDefinition? Prison { get; init; }
    /// <summary>Item-IDs von Sammelobjekten, die in den Verliesen verteilt werden (für Missionen).</summary>
    public List<string> Collectibles { get; init; } = new();
    public int CollectiblesPerDungeon { get; init; } = 2;
    public List<SpawnWeight> EnemyPool { get; init; } = new();
    public string Boss { get; init; } = "";
    /// <summary>Fähigkeit, die beim Sieg über den Boss PERMANENT freigeschaltet wird.</summary>
    public string BossReward { get; init; } = "";
    public int BelieversPerDungeon { get; init; } = 10;
}

public sealed class WorldDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Lore { get; init; } = "";
    public List<CircleDefinition> Circles { get; init; } = new();
    /// <summary>Bonus-Gläubige, wenn alle Kreise befreit sind.</summary>
    public int BelieversOnLiberation { get; init; } = 100;
}

/// <summary>Globale Stellschrauben (Content/Data/balance.json). Balancing ohne Neukompilieren.</summary>
public sealed class BalanceDefinition
{
    public int DungeonsPerCircle { get; init; } = 5;
    public float DifficultyStepPerDungeon { get; init; } = 0.35f;
    public int BaseRoomCount { get; init; } = 5;
    public int RoomsPerDungeonIndex { get; init; } = 1;
    public int TreasureBranches { get; init; } = 2;
    public int BaseWavesPerArena { get; init; } = 2;
    public int BaseWaveSize { get; init; } = 12;
    public float WaveGrowth { get; init; } = 0.25f;
    public float SpawnInterval { get; init; } = 0.3f;
    public int MaxAliveEnemies { get; init; } = 45;
    public float WaveBreakSeconds { get; init; } = 2f;
    public float XpBase { get; init; } = 5f;
    public float XpGrowth { get; init; } = 1.25f;
    public int UpgradeChoices { get; init; } = 3;
    public int CompanionSlots { get; init; } = 1;
    /// <summary>Anteil der Gläubigen, die nach dem Tod treu bleiben (0.25 = 25 %).</summary>
    public float BelieverRetentionOnDeath { get; init; } = 0.25f;
    /// <summary>+X Schaden (Might) pro 100 Gläubige. 0.05 = +5 %.</summary>
    public float MightPerHundredBelievers { get; init; } = 0.05f;
    public float HealthPerHundredBelievers { get; init; } = 0.05f;
    public float HeartDropChance { get; init; } = 0.04f;
    public float ManaDropChance { get; init; } = 0.06f;
    public int MaxActiveMissions { get; init; } = 3;
    public int ChestsPerDungeon { get; init; } = 1;
    public int LeverCount { get; init; } = 3;
    public int MaxDetours { get; init; } = 2;
    public float CrumbleDelay { get; init; } = 0.45f;
    public float CrumbleRespawnSeconds { get; init; } = 4f;
    public float BrazierTimeLimit { get; init; } = 14f;
}

// ================================================================= Neu in v2

/// <summary>Kerker mit Gefangenen, bewacht von einem Mini-Boss (optionaler Raum in EINEM Verlies pro Kreis).</summary>
public sealed class PrisonDefinition
{
    public int DungeonIndex { get; init; } = 2;
    public string MiniBoss { get; init; } = "";
    public string Guards { get; init; } = "";
    public int GuardCount { get; init; } = 3;
    public int Captives { get; init; } = 3;
    /// <summary>Begleiter, der durch die Befreiung dauerhaft freigeschaltet wird.</summary>
    public string CompanionReward { get; init; } = "";
    public int Believers { get; init; } = 25;
}

public sealed class ThemeWeight
{
    public string Theme { get; init; } = "";
    public int Weight { get; init; } = 1;
}

public enum ItemSlot { Lamp, Amulet, Ring, Collectible }

public enum ItemRarity { Common, Rare, Sacred }

public sealed class StatModifierDefinition
{
    public string Stat { get; init; } = "";
    public float Amount { get; init; }
    public bool IsPercent { get; init; }
}

public sealed class ItemDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public ItemSlot Slot { get; init; }
    public ItemRarity Rarity { get; init; }
    public List<StatModifierDefinition> Modifiers { get; init; } = new();
    /// <summary>Lichtfarbe, wenn das Item als Laterne getragen wird.</summary>
    public string LightColor { get; init; } = "#FFD6AA";
    /// <summary>Taucht erst ab diesem Kreis (0-basiert) als Beute auf.</summary>
    public int MinCircle { get; init; }
}

public enum PropAnchor { Floor, Ceiling, Wall }

public sealed class PropDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string SpriteSheet { get; init; } = "";
    /// <summary>Verhalten aus der BehaviorRegistry: static, bats, lever, rune_pillar, brazier, chest, cage, mural.</summary>
    public string Behavior { get; init; } = "static";
    public int Width { get; init; } = 16;
    public int Height { get; init; } = 16;
    public float LightRadius { get; init; }
    public string LightColor { get; init; } = "#FFB060";
    /// <summary>Stärke des Flackerns (0 = ruhiges Licht).</summary>
    public float LightFlicker { get; init; } = 0.08f;
    public bool Interactable { get; init; }
    /// <summary>Text der Interaktionsanzeige, z. B. "Hebel ziehen".</summary>
    public string Prompt { get; init; } = "";
}

public enum RoomShape { Normal, Cave, Flooded }

public sealed class ThemePropRule
{
    public string Prop { get; init; } = "";
    public PropAnchor Anchor { get; init; }
    public int Min { get; init; }
    public int Max { get; init; } = 1;
}

/// <summary>"Persönlichkeit" eines Raums: Form (Höhle, geflutet) + welche Props wo auftauchen.</summary>
public sealed class RoomThemeDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public RoomShape Shape { get; init; }
    public List<ThemePropRule> Props { get; init; } = new();
}

public enum MissionType { Collect, Slay, Rescue, CompleteCircle }

/// <summary>Bitte eines Gläubigen. Target "*" = beliebiges Ziel dieses Typs.</summary>
public sealed class MissionDefinition : IDefinition
{
    public string Id { get; init; } = "";
    public string Giver { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public MissionType Type { get; init; }
    public string Target { get; init; } = "*";
    public int Count { get; init; } = 1;
    public int RewardBelievers { get; init; } = 20;
    /// <summary>Wird erst ab so vielen Gläubigen angeboten.</summary>
    public int RequiredBelievers { get; init; }
}

public sealed class HairStyleDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Leer = kahl.</summary>
    public string Sprite { get; init; } = "";
}

/// <summary>Auswahlmöglichkeiten des Charakter-Editors (Content/Data/appearance.json).</summary>
public sealed class AppearanceDefinition
{
    public string BodySprite { get; init; } = "char.body";
    public List<string> SkinTones { get; init; } = new();
    public List<HairStyleDefinition> HairStyles { get; init; } = new();
    public List<string> HairColors { get; init; } = new();
    public List<string> AccentColors { get; init; } = new();
}
