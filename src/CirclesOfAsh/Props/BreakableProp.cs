using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Props;

/// <summary>
/// Zerstörbares Deko-Prop (Urnen, Fässer, Knochenhaufen). Reagiert auf Fähigkeits-Schaden:
/// Der Effektbereich einer Fähigkeit ruft OnHitArea; zerplatzt mit Partikeln, Sound und
/// manchmal einer kleinen Belohnung (Seelen-Snack, selten ein Herz). Gibt zerschlagene Welt-
/// Atmosphäre ("mehr Leben", "sachen zerbrechen") ohne Kollisionsproblem: Props blockieren nie.
/// </summary>
public sealed class BreakableProp : IPropBehavior
{
    private const float BreakChanceForLoot = 0.35f;
    private const float HeartChance = 0.08f;

    public void Initialize(Prop prop, DungeonWorld world) => prop.Animation.Play("idle");

    /// <summary>Von Fähigkeiten über die Welt aufgerufen: trifft dieses Prop den Flächenangriff?</summary>
    public void OnHitArea(Prop prop, DungeonWorld world, Vector2 center, float radius)
    {
        if (prop.State != 0) return;
        if (Vector2.DistanceSquared(prop.Center, center) > radius * radius) return;
        Break(prop, world);
    }

    /// <summary>Auch Body-Slam: Spieler landet drauf (von unten dagegen springen).</summary>
    public void Update(Prop prop, DungeonWorld world, float deltaSeconds)
    {
        if (prop.State != 0) return;
        // Zerbricht, wenn der Spieler mit Schwung von oben auf dem Prop landet (Velocity > 200).
        bool playerOnTop = world.Player.Bounds.Intersects(prop.Bounds)
            && world.Player.Bounds.Bottom <= prop.Position.Y + 4
            && world.Player.Velocity.Y > 180f;
        if (playerOnTop) Break(prop, world);
    }

    private static void Break(Prop prop, DungeonWorld world)
    {
        prop.State = 1;
        prop.Remove();
        world.Context.Audio.Play("crumble", 0.5f, world.Random.NextSingle() * 0.4f - 0.2f);
        world.Effects.Burst(prop.Center, Palette.Ash, 12, 80f, 0.4f);
        world.Effects.Burst(prop.Center, Palette.Bone, 6, 60f, 0.3f);
        world.ShakeCamera(0.8f);

        double roll = world.Random.NextDouble();
        if (roll < HeartChance) world.Spawn(new Pickup(PickupKind.Heart,
            world.Context.Assets.GetSpriteSheet("pickup.heart"), prop.Center, 12f, world.Random));
        else if (roll < BreakChanceForLoot) world.Spawn(new Pickup(PickupKind.Soul,
            world.Context.Assets.GetSpriteSheet("pickup.soul"), prop.Center, 1f, world.Random));
    }
}