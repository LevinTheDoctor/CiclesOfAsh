using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Props;

/// <summary>
/// Verhalten eines Props (Strategy-Pattern). Alle Methoden haben Standardimplementierungen,
/// Behaviors überschreiben nur, was sie brauchen. Signale ("open", "light", "error" ...) schicken
/// Rätsel oder die Welt, ohne die konkrete Klasse zu kennen.
/// </summary>
public interface IPropBehavior
{
    void Initialize(Prop prop, DungeonWorld world) { }
    void Update(Prop prop, DungeonWorld world, float deltaSeconds) { }
    /// <summary>Spieler drückt die Interaktionstaste. true = etwas ist passiert.</summary>
    bool Interact(Prop prop, DungeonWorld world) => false;
    void OnSignal(Prop prop, DungeonWorld world, string signal) { }
    void Draw(SpriteBatch spriteBatch, Prop prop) => prop.DrawSprite(spriteBatch);
}

/// <summary>"static": Deko, optional mit Licht (Laternen, Fackeln, Fenster ...).</summary>
public sealed class StaticProp : IPropBehavior { }

/// <summary>"bats": hängt an der Decke und flattert davon, sobald der Spieler näher kommt.</summary>
public sealed class BatProp : IPropBehavior
{
    private const float ScareDistance = 110f;
    private const float FlightSeconds = 2.5f;
    private Vector2 _velocity;

    public void Initialize(Prop prop, DungeonWorld world) => prop.Animation.Play("hang");

    public void Update(Prop prop, DungeonWorld world, float deltaSeconds)
    {
        if (prop.State == 0)
        {
            if (Vector2.Distance(prop.Center, world.Player.Center) > ScareDistance) return;
            prop.State = 1;
            prop.Animation.Play("fly");
            float away = MathF.Sign(prop.Center.X - world.Player.Center.X);
            if (away == 0f) away = 1f;
            _velocity = new Vector2(away * (70f + world.Random.NextSingle() * 60f), -30f - world.Random.NextSingle() * 50f);
            prop.IsFlipped = away < 0f;
            // Nur jede dritte Fledermaus macht Geräusch -> ein Schwarm klingt nicht wie ein Maschinengewehr
            if (prop.Index % 3 == 0) world.Context.Audio.Play("flap", 0.25f, world.Random.NextSingle() * 0.4f);
            return;
        }
        prop.Timer += deltaSeconds;
        _velocity.Y += MathF.Sin(prop.Timer * 14f) * 120f * deltaSeconds;   // Flatterbewegung
        prop.Position += _velocity * deltaSeconds;
        prop.Tint = Color.White * MathF.Max(0f, 1f - prop.Timer / FlightSeconds);
        if (prop.Timer >= FlightSeconds) prop.Remove();
    }
}

/// <summary>"lever": einmal umlegbar, meldet sich beim Rätsel.</summary>
public sealed class LeverProp : IPropBehavior
{
    public void Initialize(Prop prop, DungeonWorld world) => prop.Animation.Play("off");

    public bool Interact(Prop prop, DungeonWorld world)
    {
        if (prop.State != 0) return false;
        prop.State = 1;
        prop.CanInteract = false;
        prop.Animation.Play("on");
        world.Context.Audio.Play("lever", 0.7f);
        world.Effects.Burst(prop.Center, Palette.Soul, 10, 50f);
        world.NotifyPuzzle(prop);
        return true;
    }
}

/// <summary>"rune_pillar": zeigt ein Runensymbol (Index). Farbe je nach Zustand: 0 ruhig, 1 aktiv, 2 Fehler.</summary>
public sealed class RunePillarProp : IPropBehavior
{
    private SpriteSheet? _runes;

    public void Initialize(Prop prop, DungeonWorld world) => _runes = world.Context.Assets.GetSpriteSheet("ui.runes");

    public bool Interact(Prop prop, DungeonWorld world)
    {
        if (prop.State == 1) return false;
        world.Context.Audio.Play("lever", 0.5f, 0.4f);
        world.NotifyPuzzle(prop);
        return true;
    }

    public void OnSignal(Prop prop, DungeonWorld world, string signal)
    {
        prop.State = signal switch { "activate" => 1, "error" => 2, _ => 0 };
        prop.Animation.Play(prop.State switch { 1 => "active", 2 => "error", _ => "idle" });
        prop.CanInteract = prop.State != 1;
        prop.LightRadius = prop.State == 1 ? 34f : 0f;
    }

    public void Draw(SpriteBatch spriteBatch, Prop prop)
    {
        prop.DrawSprite(spriteBatch);
        if (_runes is null) return;
        // Runensymbol in das Leuchtfeld der Säule legen (Feld liegt 9..17 px unter der Oberkante)
        var position = new Vector2(prop.Position.X + 4, prop.Position.Y + 9);
        spriteBatch.Draw(_runes.Texture, position, _runes.GetCell(prop.Index, 0), Color.Black * 0.8f);
    }
}

/// <summary>"mural": Steintafel mit der richtigen Runenreihenfolge (Payload) als Hinweis.</summary>
public sealed class MuralProp : IPropBehavior
{
    private SpriteSheet? _runes;

    public void Initialize(Prop prop, DungeonWorld world) => _runes = world.Context.Assets.GetSpriteSheet("ui.runes");

    public void Draw(SpriteBatch spriteBatch, Prop prop)
    {
        prop.DrawSprite(spriteBatch);
        if (_runes is null) return;
        float startX = prop.Center.X - prop.Payload.Count * 9 / 2f;
        for (int index = 0; index < prop.Payload.Count; index++)
        {
            var position = new Vector2(startX + index * 9, prop.Position.Y + 8);
            spriteBatch.Draw(_runes.Texture, position, _runes.GetCell(prop.Payload[index], 0), Palette.Soul);
        }
    }
}

/// <summary>"brazier": Kohlenbecken, das per Interaktion entzündet wird. Das Rätsel entscheidet über das Erlöschen.</summary>
public sealed class BrazierProp : IPropBehavior
{
    public void Initialize(Prop prop, DungeonWorld world) => OnSignal(prop, world, "extinguish");

    public bool Interact(Prop prop, DungeonWorld world)
    {
        if (prop.State == 1) return false;
        OnSignal(prop, world, "light");
        world.Context.Audio.Play("unseal", 0.4f, 0.3f);
        world.NotifyPuzzle(prop);
        return true;
    }

    public void OnSignal(Prop prop, DungeonWorld world, string signal)
    {
        bool lit = signal == "light";
        bool wasLit = prop.State == 1;
        prop.State = lit ? 1 : 0;
        prop.CanInteract = !lit;
        prop.Animation.Play(lit ? "lit" : "unlit");
        prop.LightRadius = lit ? 64f : 0f;
        if (!lit && wasLit) world.Effects.Burst(prop.Center, Palette.Ash, 8, 30f, gravity: -30f);   // Rauch beim Erlöschen
    }
}

/// <summary>"chest": öffnet sich und wirft ein Item aus. Tag "chest_treasure" = bessere Beute.</summary>
public sealed class ChestProp : IPropBehavior
{
    public void Initialize(Prop prop, DungeonWorld world) => prop.Animation.Play("closed");

    public bool Interact(Prop prop, DungeonWorld world)
    {
        if (prop.State != 0) return false;
        prop.State = 1;
        prop.CanInteract = false;
        prop.Animation.Play("open");
        world.OpenChest(prop, isTreasure: prop.Tag == "chest_treasure");
        return true;
    }
}

/// <summary>"cage": Käfig mit Gefangenem. Öffnet sich auf das Signal "open" (nach dem Sieg über den Mini-Boss).</summary>
public sealed class CageProp : IPropBehavior
{
    public void Initialize(Prop prop, DungeonWorld world) => prop.Animation.Play("locked");

    public void OnSignal(Prop prop, DungeonWorld world, string signal)
    {
        if (signal != "open" || prop.State != 0) return;
        prop.State = 1;
        prop.Animation.Play("open");
        world.Effects.Burst(prop.Center, Palette.Soul, 24, 60f, 1.2f, gravity: -80f);   // die befreite Seele steigt auf
    }
}

/// <summary>
/// "pressure_plate": senkt sich, solange etwas Schweres darauf steht – der Spieler selbst
/// oder ein Schiebeblock. Meldet jeden Zustandswechsel ans Rätsel, nicht nur das Drücken:
/// das Gewichtsrätsel muss auch mitbekommen, wenn eine Platte wieder hochkommt.
/// </summary>
public sealed class PressurePlateProp : IPropBehavior
{
    /// <summary>Wie weit die Mitte eines Blocks von der Plattenmitte abweichen darf.</summary>
    private const float BlockTolerance = 10f;

    public void Initialize(Prop prop, DungeonWorld world) => prop.Animation.Play("raised");

    public void Update(Prop prop, DungeonWorld world, float deltaSeconds)
    {
        bool weighted = HasWeightOn(prop, world);
        if (weighted == (prop.State == 1)) return;

        prop.State = weighted ? 1 : 0;
        prop.Animation.Play(weighted ? "pressed" : "raised");
        world.Context.Audio.Play("lever", weighted ? 0.45f : 0.3f, weighted ? -0.5f : -0.2f);
        world.NotifyPuzzle(prop);
    }

    private static bool HasWeightOn(Prop prop, DungeonWorld world)
    {
        // Der Spieler zählt, wenn er mit den Fuessen auf der Platte steht.
        Player player = world.Player;
        if (player.Position.Y + player.Size.Y >= prop.Position.Y - 2f
            && player.Position.Y + player.Size.Y <= prop.Position.Y + prop.Size.Y + 4f
            && player.Center.X >= prop.Position.X && player.Center.X <= prop.Position.X + prop.Size.X)
            return true;

        foreach (Prop block in world.PropsWithTag("block"))
            if (MathF.Abs(block.Center.X - prop.Center.X) <= BlockTolerance
                && MathF.Abs(block.Position.Y + block.Size.Y - (prop.Position.Y + prop.Size.Y)) <= 6f)
                return true;
        return false;
    }
}

/// <summary>
/// "push_block": rutscht auf Tastendruck EINE Kachel in die Richtung, in die der Spieler schaut.
/// Bewusst kein Schieben mit dem Körper: dafür bräuchte der Block echte Kollision, und der
/// Spieler würde bei jedem Sprung daran hängenbleiben.
/// </summary>
public sealed class PushBlockProp : IPropBehavior
{
    private const float SlideSeconds = 0.18f;
    private Vector2 _from;
    private Vector2 _to;
    private float _slide = 1f;

    public void Initialize(Prop prop, DungeonWorld world)
    {
        _from = _to = prop.Position;
        prop.Animation.Play("idle");
    }

    public bool Interact(Prop prop, DungeonWorld world)
    {
        if (_slide < 1f) return false;   // rutscht noch
        int direction = world.Player.Center.X <= prop.Center.X ? 1 : -1;   // weg vom Spieler
        var target = new Vector2(prop.Position.X + direction * TileMap.TileSize, prop.Position.Y);
        if (IsTargetBlocked(prop, world, target) || !HasFloorAt(prop, world, target))
        {
            world.Context.Audio.Play("error", 0.4f, -0.4f);
            return false;
        }

        _from = prop.Position;
        _to = target;
        _slide = 0f;
        world.Context.Audio.Play("crumble", 0.35f, -0.6f);
        return true;
    }

    public void Update(Prop prop, DungeonWorld world, float deltaSeconds)
    {
        if (_slide >= 1f) return;
        _slide = MathF.Min(1f, _slide + deltaSeconds / SlideSeconds);
        prop.Position = Vector2.Lerp(_from, _to, _slide);
        if (_slide < 1f) return;

        // Netz: Interact laesst gar keinen Schub ins Leere mehr zu, aber falls doch einmal Boden
        // unter dem Block verschwindet (broeckelnde Kachel), faellt er nur bis zum unteren Rand
        // SEINES Raums. Frueher rutschte er durch einen Schacht in den Raum darunter und war weg –
        // das Raetsel blieb fuer immer unloesbar.
        float roomBottom = (prop.Room.TileBounds.Bottom - 1) * TileMap.TileSize;
        while (!IsSolidBelow(prop, world) && prop.Position.Y + prop.Size.Y < roomBottom)
            prop.Position = new Vector2(prop.Position.X, prop.Position.Y + TileMap.TileSize);
    }

    /// <summary>Liegt unter der Zielkachel fester Boden? Sonst wuerde der Block ins Loch geschoben.</summary>
    private static bool HasFloorAt(Prop prop, DungeonWorld world, Vector2 target)
    {
        int row = TileMap.ToTile(target.Y + prop.Size.Y);
        int left = TileMap.ToTile(target.X + 2);
        int right = TileMap.ToTile(target.X + prop.Size.X - 3);
        for (int column = left; column <= right; column++)
        {
            if (!world.Map.IsInside(column, row)) return false;
            TileType tile = world.Map[column, row];
            if (TileMap.IsBlocking(tile) || TileMap.IsPlatform(tile)) return true;
        }
        return false;
    }

    private static bool IsTargetBlocked(Prop prop, DungeonWorld world, Vector2 target)
    {
        var box = new Rectangle((int)MathF.Round(target.X) + 2, (int)MathF.Round(target.Y) + 2,
                                prop.Size.X - 4, prop.Size.Y - 4);
        if (TilePhysics.IsBlocked(world.Map, box)) return true;
        // Zwei Blöcke auf derselben Kachel wären nicht mehr auseinanderzuziehen.
        foreach (Prop other in world.PropsWithTag("block"))
            if (other != prop && MathF.Abs(other.Position.X - target.X) < TileMap.TileSize * 0.8f
                && MathF.Abs(other.Position.Y - target.Y) < TileMap.TileSize * 0.8f)
                return true;
        return false;
    }

    private static bool IsSolidBelow(Prop prop, DungeonWorld world)
    {
        int row = TileMap.ToTile(prop.Position.Y + prop.Size.Y);
        int left = TileMap.ToTile(prop.Position.X + 2);
        int right = TileMap.ToTile(prop.Position.X + prop.Size.X - 3);
        for (int column = left; column <= right; column++)
        {
            if (!world.Map.IsInside(column, row)) return true;   // Rand zählt als Boden
            TileType tile = world.Map[column, row];
            if (TileMap.IsBlocking(tile) || TileMap.IsPlatform(tile)) return true;
        }
        return false;
    }
}

/// <summary>
/// "mirror": vier Stellungen im Kreis. Nur die Diagonalen lenken den Strahl um,
/// flach gestellt blockt der Spiegel ihn – das ist die eigentliche Aufgabe des Rätsels.
/// State 0 = "|", 1 = "/", 2 = "–", 3 = "\".
/// </summary>
public sealed class MirrorProp : IPropBehavior
{
    private static readonly string[] Clips = { "angle0", "angle45", "angle90", "angle135" };

    public void Initialize(Prop prop, DungeonWorld world) => prop.Animation.Play(Clips[prop.State & 3]);

    public bool Interact(Prop prop, DungeonWorld world)
    {
        prop.State = (prop.State + 1) & 3;
        prop.Animation.Play(Clips[prop.State]);
        world.Context.Audio.Play("lever", 0.5f, 0.6f);
        world.NotifyPuzzle(prop);
        return true;
    }

    public void OnSignal(Prop prop, DungeonWorld world, string signal)
    {
        // Der Strahl meldet, ob er diesen Spiegel gerade trifft -> er glimmt dann.
        prop.LightRadius = signal == "lit" ? 30f : 0f;
        prop.Tint = signal == "lit" ? Color.White : new Color(200, 200, 210);
    }
}
