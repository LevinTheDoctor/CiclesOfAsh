using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Companions;

/// <summary>Verhalten einer Begleitseele. Act wird im Takt von CompanionDefinition.Interval aufgerufen.</summary>
public interface ICompanionBehavior
{
    /// <summary>true = Aktion ausgeführt (Takt beginnt neu), false = nichts zu tun (nächsten Frame erneut prüfen).</summary>
    bool Act(Companion companion, DungeonWorld world);
}

/// <summary>"attacker": schießt auf den nächsten Gegner. Skaliert mit der Macht des Gottes.</summary>
public sealed class AttackerCompanion : ICompanionBehavior
{
    public bool Act(Companion companion, DungeonWorld world)
    {
        Enemy? target = world.FindNearestEnemy(companion.Center, companion.Definition.Range);
        if (target is null) return false;
        Vector2 direction = MathUtil.SafeNormalize(target.Center - companion.Center, Vector2.UnitX);
        float damage = companion.Definition.Power * world.Player.Stats[StatType.Might];
        world.Spawn(new Projectile(Faction.Player, world.Context.Assets.GetSpriteSheet(companion.Definition.ProjectileSprite),
            companion.Center, direction * 200f, damage, 0, 1.5f, 30f));
        return true;
    }
}

/// <summary>"healer": heilt, sobald der Spieler verletzt ist.</summary>
public sealed class HealerCompanion : ICompanionBehavior
{
    public bool Act(Companion companion, DungeonWorld world)
    {
        Player player = world.Player;
        if (player.Health.Current >= player.Health.Max) return false;
        player.Health.Heal(companion.Definition.Power);
        world.Effects.Burst(player.Center, Palette.Soul, 10, 40f, gravity: -60f);
        world.Effects.Text(player.Center - new Vector2(0, 18), $"+{companion.Definition.Power:0}", Palette.Soul);
        return true;
    }
}

/// <summary>"mana": füllt Mana auf.</summary>
public sealed class ManaCompanion : ICompanionBehavior
{
    public bool Act(Companion companion, DungeonWorld world)
    {
        Player player = world.Player;
        if (player.Mana >= player.MaxMana) return false;
        player.RestoreMana(companion.Definition.Power);
        world.Effects.Burst(player.Center, Palette.Mana, 8, 40f, gravity: -60f);
        return true;
    }
}
