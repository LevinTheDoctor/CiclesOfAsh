using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Progression;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Networking;

/// <summary>Warum der Gastgeber ablehnt – als Code, damit jede Seite den Grund in IHRER Sprache zeigt.</summary>
public enum RejectReason : byte { VersionMismatch = 1, Busy = 2 }

/// <summary>Was der Gast beim Handschlag über den Kampf erfährt.</summary>
public sealed record ArenaWelcome(ArenaFighter HostFighter, string OpponentId, int Seed, uint MapChecksum);

/// <summary>Die Tasten eines Frames des Gastes, als Bitmasken (Bit n = GameAction mit dem Wert n).</summary>
public readonly record struct InputFrame(float Horizontal, ushort Held, ushort Pressed, ushort Released)
{
    public static InputFrame Idle => default;   // default = alles 0: nichts gedrückt
}

/// <summary>
/// Das Leitungsformat der Online-Arena. Alles binär mit BinaryWriter/BinaryReader (Little-Endian,
/// Strings mit Längenpräfix). Schreiben und Lesen stehen je Datentyp direkt untereinander, damit
/// beide Seiten nie auseinanderlaufen. Bei jeder Formatänderung <see cref="Version"/> erhöhen.
/// </summary>
public static class ArenaProtocol
{
    /// <summary>Unterschiedliche Protokollversionen spielen nicht miteinander.</summary>
    public const int Version = 1;
    public const int DefaultPort = 47017;

    /// <summary>Spielversion aus der Assembly. Auch sie muss gleich sein: Andere Daten = anderer Kampf.</summary>
    public static string GameVersion { get; } = typeof(CirclesGame).Assembly.GetName().Version?.ToString() ?? "0";

    /// <summary>
    /// Ausnahmen, die beim Lesen einer kaputten (oder böswilligen) Nachricht entstehen. Wer liest,
    /// fängt genau diese und behandelt die Verbindung als gestört – das Spiel selbst stürzt nie ab.
    /// </summary>
    public static bool IsMalformed(Exception exception) =>
        exception is EndOfStreamException or InvalidDataException or FormatException or IOException or ArgumentException;

    // ================================================================= Handschlag
    public static byte[] Hello(ArenaFighter fighter) => Build(writer =>
    {
        writer.Write(Version);
        writer.Write(GameVersion);
        WriteFighter(writer, fighter);
    });

    /// <summary>Liest ein Hello. false = andere Protokoll- oder Spielversion (dann ist <paramref name="fighter"/> null).</summary>
    public static bool TryReadHello(byte[] payload, out ArenaFighter? fighter)
    {
        using var reader = Reader(payload);
        fighter = null;
        if (reader.ReadInt32() != Version || reader.ReadString() != GameVersion) return false;
        fighter = ReadFighter(reader);
        return true;
    }

    public static byte[] Welcome(ArenaWelcome welcome) => Build(writer =>
    {
        WriteFighter(writer, welcome.HostFighter);
        writer.Write(welcome.OpponentId);
        writer.Write(welcome.Seed);
        writer.Write(welcome.MapChecksum);
    });

    public static ArenaWelcome ReadWelcome(byte[] payload)
    {
        using var reader = Reader(payload);
        return new ArenaWelcome(ReadFighter(reader), reader.ReadString(), reader.ReadInt32(), reader.ReadUInt32());
    }

    // ================================================================= Kämpfer
    public static void WriteFighter(BinaryWriter writer, ArenaFighter fighter)
    {
        writer.Write(fighter.Name);
        WriteAppearance(writer, fighter.Appearance);
        WriteRun(writer, fighter.Run);
        writer.Write(fighter.Believers);
        WriteStrings(writer, fighter.EternalGifts);
    }

    /// <summary>CharacterId ist beim fremden Kämpfer 0: Seine Gestalt liegt nicht in unserem Spielstand.</summary>
    public static ArenaFighter ReadFighter(BinaryReader reader)
    {
        string name = reader.ReadString();
        CharacterAppearance appearance = ReadAppearance(reader);
        RunState run = ReadRun(reader);
        run.Appearance = appearance;
        long believers = reader.ReadInt64();
        List<string> gifts = ReadStrings(reader);
        return new ArenaFighter(0, name, appearance, run, believers, gifts);
    }

    private static void WriteAppearance(BinaryWriter writer, CharacterAppearance look)
    {
        writer.Write(look.Name);
        foreach (int value in new[] { look.SkinTone, look.HairStyle, look.HairColor, look.AccentColor, look.BodyType,
                                      look.Makeup, look.MakeupColor, look.Wings })
            writer.Write(value);
    }

    private static CharacterAppearance ReadAppearance(BinaryReader reader) =>
        // Argumente werden in C# garantiert von links nach rechts ausgewertet – gleiche Reihenfolge wie beim Schreiben.
        new(reader.ReadString(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
            reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

    private static void WriteRun(BinaryWriter writer, RunState run)
    {
        writer.Write(run.ClassId);
        writer.Write(run.WorldId);
        writer.Write(run.CircleIndex);
        writer.Write(run.DungeonIndex);
        writer.Write(run.Level);
        writer.Write(run.Experience);
        writer.Write(run.Seed);
        WriteCounts(writer, run.AbilityLevels);
        WriteCounts(writer, run.UpgradeStacks);
        WriteStrings(writer, run.CompanionIds);
        WriteStrings(writer, run.Items);
        writer.Write(run.Equipped.Count);
        foreach (var (slot, itemId) in run.Equipped)
        {
            writer.Write((int)slot);   // Enum als Zahl
            writer.Write(itemId);
        }
        writer.Write(run.ArmorDurability);
        WriteCounts(writer, run.ArmorWear);
        writer.Write(run.ForgeUses);
        writer.Write(run.Underwear);
    }

    private static RunState ReadRun(BinaryReader reader)
    {
        var run = new RunState
        {
            ClassId = reader.ReadString(),
            WorldId = reader.ReadString(),
            CircleIndex = reader.ReadInt32(),
            DungeonIndex = reader.ReadInt32(),
            Level = reader.ReadInt32(),
            Experience = reader.ReadSingle(),
            Seed = reader.ReadInt32(),
            AbilityLevels = ReadCounts(reader),
            UpgradeStacks = ReadCounts(reader),
            CompanionIds = ReadStrings(reader),
            Items = ReadStrings(reader),
        };
        int equippedCount = ReadCount(reader);
        for (int index = 0; index < equippedCount; index++) run.Equipped[(ItemSlot)reader.ReadInt32()] = reader.ReadString();
        run.ArmorDurability = reader.ReadInt32();
        run.ArmorWear = ReadCounts(reader);
        run.ForgeUses = reader.ReadInt32();
        run.Underwear = reader.ReadInt32();
        return run;
    }

    // ================================================================= Eingabe
    /// <summary>Die Tasten dieses Frames als Bitmasken. "1 &lt;&lt; n" = Bit n gesetzt.</summary>
    public static InputFrame CaptureInput(IPlayerInput input)
    {
        ushort held = 0, pressed = 0, released = 0;
        foreach (GameAction action in Enum.GetValues<GameAction>())
        {
            var bit = (ushort)(1 << (int)action);
            if (input.IsDown(action)) held |= bit;          // "|=" = Bit hinzufügen
            if (input.WasPressed(action)) pressed |= bit;
            if (input.WasReleased(action)) released |= bit;
        }
        return new InputFrame(input.Horizontal, held, pressed, released);
    }

    public static byte[] Input(InputFrame frame) => Build(writer =>
    {
        writer.Write(frame.Horizontal);
        writer.Write(frame.Held);
        writer.Write(frame.Pressed);
        writer.Write(frame.Released);
    });

    public static InputFrame ReadInput(byte[] payload)
    {
        using var reader = Reader(payload);
        float horizontal = reader.ReadSingle();
        // NaN oder Unendlich würden über die Geschwindigkeit die ganze Physik vergiften -> 0.
        horizontal = float.IsFinite(horizontal) ? Math.Clamp(horizontal, -1f, 1f) : 0f;
        return new InputFrame(horizontal, reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16());
    }

    // ================================================================= Ende
    public static byte[] End(byte result, float seconds) => Build(writer =>
    {
        writer.Write(result);
        writer.Write(seconds);
    });

    public static (byte Result, float Seconds) ReadEnd(byte[] payload)
    {
        using var reader = Reader(payload);
        return (reader.ReadByte(), reader.ReadSingle());
    }

    // ================================================================= Schnappschuss
    private enum EventCode : byte { Sound = 1, Burst, Ring, Text, Sprite, Shake, Announce }

    public static byte[] Snapshot(WorldMirrorFrame frame) => Build(writer =>
    {
        writer.Write(frame.ElapsedSeconds);

        writer.Write((byte)frame.Players.Count);
        foreach (PlayerMirrorState player in frame.Players)
        {
            WriteVector(writer, player.Position);
            writer.Write((byte)player.Flags);
            writer.Write(player.Animation);
            writer.Write(player.Health);
            writer.Write(player.MaxHealth);
            writer.Write(player.InvulnerableSeconds);
            writer.Write(player.Mana);
            writer.Write(player.Stamina);
            writer.Write(player.ArmorDurability);
            writer.Write(player.ArmorSprite);
            writer.Write(player.ReviveProgress);
            writer.Write((byte)player.AbilityPhases.Count);
            foreach (float phase in player.AbilityPhases) writer.Write(phase);
        }

        writer.Write(frame.Enemies.Count);
        foreach (EnemyMirrorState enemy in frame.Enemies)
        {
            writer.Write(enemy.NetworkId);
            writer.Write(enemy.DefinitionId);
            WriteVector(writer, enemy.Position);
            writer.Write(enemy.FacingRight);
            writer.Write(enemy.Animation);
            writer.Write(enemy.SpawnSeconds);
            writer.Write(enemy.Flashing);
            writer.Write(enemy.Health);
            writer.Write(enemy.MaxHealth);
            writer.Write(enemy.IsActiveBoss);
        }

        writer.Write(frame.Projectiles.Count);
        foreach (ProjectileMirrorState projectile in frame.Projectiles)
        {
            writer.Write(projectile.NetworkId);
            writer.Write(projectile.SheetId);
            WriteVector(writer, projectile.Position);
            WriteVector(writer, projectile.Velocity);
        }

        writer.Write(frame.Pickups.Count);
        foreach (PickupMirrorState pickup in frame.Pickups)
        {
            writer.Write(pickup.NetworkId);
            writer.Write((byte)pickup.Kind);
            writer.Write(pickup.SheetId);
            writer.Write(pickup.Animation);
            WriteVector(writer, pickup.Position);
        }

        writer.Write(frame.ChangedTiles.Count);
        foreach (TileMirrorState tile in frame.ChangedTiles)
        {
            writer.Write(tile.X);
            writer.Write(tile.Y);
            writer.Write((byte)tile.Type);
        }

        writer.Write(frame.RemovedProps.Count);
        foreach (int propId in frame.RemovedProps) writer.Write(propId);

        writer.Write(frame.Events.Count);
        foreach (WorldEvent worldEvent in frame.Events) WriteEvent(writer, worldEvent);
    });

    public static WorldMirrorFrame ReadSnapshot(byte[] payload)
    {
        using var reader = Reader(payload);
        var frame = new WorldMirrorFrame { ElapsedSeconds = reader.ReadSingle() };

        int playerCount = reader.ReadByte();
        for (int index = 0; index < playerCount; index++)
        {
            Vector2 position = ReadVector(reader);
            var flags = (PlayerMirrorFlags)reader.ReadByte();
            string animation = reader.ReadString();
            float health = reader.ReadSingle(), maxHealth = reader.ReadSingle(), invulnerable = reader.ReadSingle();
            float mana = reader.ReadSingle(), stamina = reader.ReadSingle();
            int armor = reader.ReadInt32();
            string armorSprite = reader.ReadString();
            float revive = reader.ReadSingle();
            int phaseCount = reader.ReadByte();
            var phases = new List<float>(phaseCount);
            for (int phase = 0; phase < phaseCount; phase++) phases.Add(reader.ReadSingle());
            frame.Players.Add(new PlayerMirrorState(position, flags, animation, health, maxHealth, invulnerable, mana, stamina,
                                                    armor, armorSprite, revive, phases));
        }

        int enemyCount = ReadCount(reader);
        for (int index = 0; index < enemyCount; index++)
        {
            frame.Enemies.Add(new EnemyMirrorState(reader.ReadInt32(), reader.ReadString(), ReadVector(reader),
                reader.ReadBoolean(), reader.ReadString(), reader.ReadSingle(), reader.ReadBoolean(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadBoolean()));
        }

        int projectileCount = ReadCount(reader);
        for (int index = 0; index < projectileCount; index++)
            frame.Projectiles.Add(new ProjectileMirrorState(reader.ReadInt32(), reader.ReadString(), ReadVector(reader), ReadVector(reader)));

        int pickupCount = ReadCount(reader);
        for (int index = 0; index < pickupCount; index++)
            frame.Pickups.Add(new PickupMirrorState(reader.ReadInt32(), (PickupKind)reader.ReadByte(), reader.ReadString(),
                                                    reader.ReadString(), ReadVector(reader)));

        int tileCount = ReadCount(reader);
        for (int index = 0; index < tileCount; index++)
            frame.ChangedTiles.Add(new TileMirrorState(reader.ReadInt32(), reader.ReadInt32(), (TileType)reader.ReadByte()));

        int removedCount = ReadCount(reader);
        for (int index = 0; index < removedCount; index++) frame.RemovedProps.Add(reader.ReadInt32());

        int eventCount = ReadCount(reader);
        for (int index = 0; index < eventCount; index++) frame.Events.Add(ReadEvent(reader));
        return frame;
    }

    /// <summary>
    /// switch mit Typmustern: prüft den Laufzeittyp des Ereignisses und bindet es gleich an eine
    /// passend typisierte Variable ("case SoundEvent sound").
    /// </summary>
    private static void WriteEvent(BinaryWriter writer, WorldEvent worldEvent)
    {
        switch (worldEvent)
        {
            case SoundEvent sound:
                writer.Write((byte)EventCode.Sound);
                writer.Write(sound.SoundId);
                writer.Write(sound.Volume);
                writer.Write(sound.Pitch);
                break;
            case BurstEvent burst:
                writer.Write((byte)EventCode.Burst);
                WriteVector(writer, burst.Position);
                writer.Write(burst.Color.PackedValue);
                writer.Write(burst.Count);
                writer.Write(burst.Speed);
                writer.Write(burst.Lifetime);
                writer.Write(burst.Gravity);
                break;
            case RingEvent ring:
                writer.Write((byte)EventCode.Ring);
                WriteVector(writer, ring.Center);
                writer.Write(ring.Radius);
                writer.Write(ring.Color.PackedValue);
                writer.Write(ring.Count);
                break;
            case TextEvent text:
                writer.Write((byte)EventCode.Text);
                WriteVector(writer, text.Position);
                writer.Write(text.Text);
                writer.Write(text.Color.PackedValue);
                break;
            case SpriteEvent sprite:
                writer.Write((byte)EventCode.Sprite);
                writer.Write(sprite.SheetId);
                WriteVector(writer, sprite.BottomCenter);
                writer.Write(sprite.Flip);
                break;
            case ShakeEvent shake:
                writer.Write((byte)EventCode.Shake);
                writer.Write(shake.Strength);
                break;
            case AnnounceEvent announce:
                writer.Write((byte)EventCode.Announce);
                writer.Write(announce.Text);
                break;
            default:
                throw new InvalidOperationException($"Unbekanntes Welt-Ereignis {worldEvent.GetType().Name}.");
        }
    }

    private static WorldEvent ReadEvent(BinaryReader reader) => (EventCode)reader.ReadByte() switch
    {
        EventCode.Sound => new SoundEvent(reader.ReadString(), reader.ReadSingle(), reader.ReadSingle()),
        EventCode.Burst => new BurstEvent(ReadVector(reader), new Color(reader.ReadUInt32()), reader.ReadInt32(),
                                          reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
        EventCode.Ring => new RingEvent(ReadVector(reader), reader.ReadSingle(), new Color(reader.ReadUInt32()), reader.ReadInt32()),
        EventCode.Text => new TextEvent(ReadVector(reader), reader.ReadString(), new Color(reader.ReadUInt32())),
        EventCode.Sprite => new SpriteEvent(reader.ReadString(), ReadVector(reader), reader.ReadBoolean()),
        EventCode.Shake => new ShakeEvent(reader.ReadSingle()),
        EventCode.Announce => new AnnounceEvent(reader.ReadString()),
        var unknown => throw new InvalidDataException($"Unbekannter Ereigniscode {unknown}."),
    };

    // ================================================================= Helfer
    private static byte[] Build(Action<BinaryWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer))
        {
            write(writer);
        }
        return buffer.ToArray();
    }

    private static BinaryReader Reader(byte[] payload) => new(new MemoryStream(payload, writable: false));

    private static void WriteVector(BinaryWriter writer, Vector2 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
    }

    private static Vector2 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());

    private static void WriteStrings(BinaryWriter writer, IReadOnlyCollection<string> values)
    {
        writer.Write(values.Count);
        foreach (string value in values) writer.Write(value);
    }

    private static List<string> ReadStrings(BinaryReader reader)
    {
        int count = ReadCount(reader);
        var values = new List<string>(count);
        for (int index = 0; index < count; index++) values.Add(reader.ReadString());
        return values;
    }

    private static void WriteCounts(BinaryWriter writer, Dictionary<string, int> values)
    {
        writer.Write(values.Count);
        foreach (var (key, value) in values)
        {
            writer.Write(key);
            writer.Write(value);
        }
    }

    private static Dictionary<string, int> ReadCounts(BinaryReader reader)
    {
        int count = ReadCount(reader);
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < count; index++) values[reader.ReadString()] = reader.ReadInt32();
        return values;
    }

    /// <summary>Anzahl lesen und prüfen: Eine kaputte Nachricht darf keine Milliarden Einträge anfordern.</summary>
    private static int ReadCount(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > 100_000) throw new InvalidDataException($"Unplausible Anzahl {count}.");
        return count;
    }
}
