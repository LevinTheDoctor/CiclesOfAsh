using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

/// <summary>
/// Nicht-feindlicher Bewohner: betende Pilger, Eremiten, Gefangene. Interagierbar (Dialog).
/// NPCs sind unverwundbar (kein Health) – sie gehören zur "sicheren Zone" ihrer Nische und
/// machen die Welt bevölkert. Tag erlaubt Spezialfälle (z. B. "rescue" für Escort-Ziele).
/// </summary>
public sealed class Npc : Entity
{
    private readonly AnimationPlayer _animation;
    private float _bobTime;

    public Npc(NpcDefinition definition, SpriteSheet sheet, Vector2 bottomCenter, string tag)
    {
        Definition = definition;
        Tag = tag;
        _animation = new AnimationPlayer(sheet);
        // Wie beim Spieler aus der Bildgröße abgeleitet, damit größere Sprites von selbst passen.
        Size = new Point(Math.Max(4, (int)MathF.Round(sheet.FrameWidth * 0.62f)),
                         Math.Max(4, (int)MathF.Round(sheet.FrameHeight * 0.84f)));
        Position = bottomCenter - new Vector2(Size.X / 2f, Size.Y);
        LightRadius = definition.LightRadius;
        LightColor = ColorUtil.FromHex(definition.LightColor, Color.White);
    }

    public NpcDefinition Definition { get; }
    /// <summary>Frei nutzbarer Zustand: "blessed" nach Segen, "rescue" für Escort-Ziele.</summary>
    public string Tag { get; set; }
    public float LightRadius { get; set; }
    public Color LightColor { get; set; }
    /// <summary>Rescue-NPCs laufen nach der Befreiung zum Ausgang; null = wartet.</summary>
    public Vector2? FleeTarget { get; set; }
    public bool IsFollowing { get; set; }

    public override void Update(DungeonWorld world, float deltaSeconds)
    {
        _animation.Update(deltaSeconds);
        _bobTime += deltaSeconds;

        if (FleeTarget is { } target)
        {
            // Befreite Seelen humpeln Richtung Ausgang – der Spieler beschützt sie unterwegs.
            float direction = MathF.Sign(target.X - Center.X);
            if (MathF.Abs(target.X - Center.X) > 6f)
            {
                Velocity.X = direction * 34f;
                FacingRight = direction > 0f;
            }
            else Velocity.X = 0f;
        }
        else Velocity.X = 0f;

        // Dasselbe Muster wie Enemy.Update: Schwerkraft, dann Kachelkollision. Vorher lief die
        // Seele auf konstanter Höhe stur in X-Richtung – durch Wände und aus der Karte hinaus.
        Velocity.Y = MathF.Min(Velocity.Y + TilePhysics.Gravity * deltaSeconds, TilePhysics.MaxFallSpeed);
        CollisionResult collision = TilePhysics.MoveAndCollide(this, world.Map, deltaSeconds, ignorePlatforms: false);
        OnGround = collision.HasFlag(CollisionResult.Landed);

        // An einer Wand hängen geblieben (z. B. Höhlendeko)? Dann hüpfen, damit sie weiterkommt.
        if (Velocity.X != 0f && collision.HasFlag(CollisionResult.HitWall) && OnGround) Velocity.Y = -240f;

        _animation.Play(MathF.Abs(Velocity.X) > 5f ? "run" : "idle");
    }

    /// <summary>Bodenkontakt für den Sprung aus der Wand (siehe Update).</summary>
    private bool OnGround { get; set; }

    /// <summary>Hub-Variante ohne Welt: nur Idle-Animation (NPCs stehen/beten im Tempel).</summary>
    public void UpdateHub(float deltaSeconds)
    {
        _animation.Update(deltaSeconds);
        _bobTime += deltaSeconds;
    }

    private bool FacingRight { get; set; } = true;

    public override void Draw(SpriteBatch spriteBatch)
    {
        float bob = MathF.Sin(_bobTime * 2f) * 1f;
        _animation.Draw(spriteBatch, BottomCenter + new Vector2(0, bob), !FacingRight, Color.White);
    }
}