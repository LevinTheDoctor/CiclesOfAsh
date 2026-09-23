using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Props;

/// <summary>
/// Kleine Fluchtkreatur (Ratte, Motte): harmlesses Deko-Tier, das vor dem Spieler flieht.
/// Läuft am Boden entlang, dreht an Wänden um und verschwindet irgendwann. Kein Kontakt-
/// schaden, keine Kollision -> die Welt wirkt lebendig, ohne unfair zu werden.
/// Moth flattert zusätzlich mit einer Sinus-Welle.
/// </summary>
public sealed class CritterProp : IPropBehavior
{
    private const float ScareDistance = 40f;
    private const float PanicSeconds = 3.5f;
    private float _panicTimer;
    private float _phase;

    public void Initialize(Prop prop, DungeonWorld world)
    {
        _phase = world.Random.NextSingle() * MathF.Tau;
        prop.Animation.Play("idle");
    }

    public void Update(Prop prop, DungeonWorld world, float deltaSeconds)
    {
        _phase += deltaSeconds;
        float distance = Vector2.Distance(prop.Center, world.Player.Center);
        if (_panicTimer <= 0f && distance < ScareDistance)
        {
            _panicTimer = PanicSeconds;
            if (prop.Index % 3 == 0) world.Context.Audio.Play("flap", 0.2f, world.Random.NextSingle() * 0.5f);
        }

        bool isFlying = prop.Definition.Id.EndsWith("moth");
        if (_panicTimer > 0f)
        {
            _panicTimer -= deltaSeconds;
            float away = MathF.Sign(prop.Center.X - world.Player.Center.X);
            if (away == 0f) away = 1f;
            prop.IsFlipped = away < 0f;
            float speed = 55f;
            if (isFlying)
            {
                prop.Position += new Vector2(away * speed, MathF.Sin(_phase * 9f) * 22f - 8f) * deltaSeconds;
            }
            else
            {
                // Ratte: rennt am Boden, an Wänden umdrehen (Probe 2 px vor dem Bauch)
                int probeX = TileMap.ToTile(away > 0 ? prop.Bounds.Right + 2 : prop.Bounds.Left - 2);
                if (TileMap.IsBlocking(world.Map[probeX, TileMap.ToTile(prop.Center.Y)]))
                {
                    away = -away;
                    prop.IsFlipped = away < 0f;
                }
                prop.Position += new Vector2(away * speed, 0f) * deltaSeconds;
            }
        }
    }
}