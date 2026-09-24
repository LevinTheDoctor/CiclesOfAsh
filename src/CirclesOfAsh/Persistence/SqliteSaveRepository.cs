using System.Globalization;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using Microsoft.Data.Sqlite;

namespace CirclesOfAsh.Persistence;

/// <summary>
/// Speichert den Spielstand in einer SQLite-Datei (%APPDATA%/CirclesOfAsh/save.db).
///  * Schema-Versionierung über "PRAGMA user_version" + Migrationsliste -> ältere Spielstände werden
///    beim Start automatisch hochgezogen, statt kaputtzugehen.
///  * Alle Werte über Parameter ($name) -> kein SQL-Injection-Risiko, korrekte Typen.
///  * Mehrere Schreibvorgänge in einer Transaktion -> entweder alles oder nichts gespeichert
///    (kein halber Spielstand, wenn das Spiel mittendrin abstürzt).
/// </summary>
public sealed class SqliteSaveRepository : ISaveRepository
{
    private const string KindAbility = "ability";
    private const string KindCompanion = "companion";
    private const string KindWorld = "world";
    private const string KindUpgrade = "upgrade";
    private const string KindPrison = "prison";
    private const string KindItem = "item";
    private const string KindEquipped = "equipped";

    // Jede Migration bringt die DB genau eine Version weiter. NIEMALS alte Einträge ändern, nur neue anhängen!
    private static readonly string[] Migrations =
    {
        // Version 1
        """
        CREATE TABLE meta (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE TABLE unlocks (
            kind        TEXT NOT NULL,
            id          TEXT NOT NULL,
            unlocked_at TEXT NOT NULL,
            PRIMARY KEY (kind, id)
        );
        CREATE TABLE run (
            id            INTEGER PRIMARY KEY CHECK (id = 1),
            class_id      TEXT    NOT NULL,
            world_id      TEXT    NOT NULL,
            circle_index  INTEGER NOT NULL,
            dungeon_index INTEGER NOT NULL,
            level         INTEGER NOT NULL,
            experience    REAL    NOT NULL,
            seed          INTEGER NOT NULL
        );
        CREATE TABLE run_items (
            kind  TEXT    NOT NULL,
            id    TEXT    NOT NULL,
            value INTEGER NOT NULL,
            PRIMARY KEY (kind, id)
        );
        """,
        // Version 2: Bitten der Gläubigen + Aussehen des Charakters (Schlüssel/Wert, erweiterbar ohne neue Spalten)
        """
        CREATE TABLE missions (
            id       TEXT    PRIMARY KEY,
            status   TEXT    NOT NULL,
            progress INTEGER NOT NULL
        );
        CREATE TABLE run_profile (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """,
        // Version 3: Optionsmenü, Heimwelt (Deko + Haustiere) und Sammelobjekt-Zähler
        """
        CREATE TABLE settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE TABLE hub_deco (
            id       INTEGER PRIMARY KEY AUTOINCREMENT,
            prop_id  TEXT    NOT NULL,
            tile_x   INTEGER NOT NULL,
            tile_y   INTEGER NOT NULL
        );
        CREATE TABLE pets (
            companion_id TEXT PRIMARY KEY,
            name         TEXT NOT NULL,
            loyalty      INTEGER NOT NULL,
            fed          INTEGER NOT NULL,
            petted_at    TEXT NOT NULL
        );
        CREATE TABLE collectibles (
            item_id TEXT PRIMARY KEY,
            count   INTEGER NOT NULL
        );
        """,
    };
    // """ ... """ = Raw String Literal (C# 11): mehrzeiliger Text ohne Escape-Zeichen, ideal für SQL

    private readonly string _connectionString;

    public SqliteSaveRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        Migrate();
    }

    // ------------------------------------------------------------------ Schema
    private void Migrate()
    {
        using SqliteConnection connection = Open();
        long version = Convert.ToInt64(CreateCommand(connection, "PRAGMA user_version;").ExecuteScalar(), CultureInfo.InvariantCulture);
        for (long next = version; next < Migrations.Length; next++)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            CreateCommand(connection, Migrations[next], transaction).ExecuteNonQuery();
            // PRAGMA kann nicht parametrisiert werden; der Wert ist eine interne Zahl -> sicher
            CreateCommand(connection, $"PRAGMA user_version = {next + 1};", transaction).ExecuteNonQuery();
            transaction.Commit();
            Log.Info($"Savegame-Schema auf Version {next + 1} migriert.");
        }
    }

    // ------------------------------------------------------------------ Meta
    public MetaState LoadMeta()
    {
        var meta = new MetaState();
        using SqliteConnection connection = Open();

        using (SqliteDataReader reader = CreateCommand(connection, "SELECT key, value FROM meta;").ExecuteReader())
        {
            var values = new Dictionary<string, string>();
            while (reader.Read()) values[reader.GetString(0)] = reader.GetString(1);
            meta.Believers = ReadLong(values, "believers");
            meta.Deaths = (int)ReadLong(values, "deaths");
            meta.RunsStarted = (int)ReadLong(values, "runs_started");
        }

        using (SqliteDataReader reader = CreateCommand(connection, "SELECT kind, id FROM unlocks;").ExecuteReader())
        {
            while (reader.Read())
            {
                string id = reader.GetString(1);
                // switch-Anweisung mit String-Konstanten
                switch (reader.GetString(0))
                {
                    case KindAbility: meta.UnlockedAbilities.Add(id); break;
                    case KindCompanion: meta.UnlockedCompanions.Add(id); break;
                    case KindWorld: meta.LiberatedWorlds.Add(id); break;
                    case KindPrison: meta.RescuedPrisons.Add(id); break;
                }
            }
        }

        using (SqliteDataReader reader = CreateCommand(connection, "SELECT id, status, progress FROM missions;").ExecuteReader())
        {
            while (reader.Read())
            {
                // Enum.TryParse: unbekannte Status-Texte (z. B. aus einer neueren Version) werden übersprungen
                string status = reader.GetString(1);
                if (status.Equals("pending", StringComparison.OrdinalIgnoreCase))
                {
                    meta.PendingMissionProgress[reader.GetString(0)] = reader.GetInt32(2);
                    continue;
                }
                if (!Enum.TryParse(status, out MissionStatus missionStatus)) continue;
                meta.Missions[reader.GetString(0)] = new MissionProgress { Status = missionStatus, Progress = reader.GetInt32(2) };
            }
        }
        return meta;
    }

    public void SaveMeta(MetaState meta)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        // UPSERT: einfügen oder bei vorhandenem Schlüssel aktualisieren ("excluded" = der abgelehnte neue Datensatz)
        const string upsertMeta = "INSERT INTO meta (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        foreach (var (key, value) in new (string, long)[] { ("believers", meta.Believers), ("deaths", meta.Deaths), ("runs_started", meta.RunsStarted) })
        {
            SqliteCommand command = CreateCommand(connection, upsertMeta, transaction);
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value.ToString(CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }

        const string insertUnlock = "INSERT OR IGNORE INTO unlocks (kind, id, unlocked_at) VALUES ($kind, $id, $at);";
        string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);   // "O" = ISO-8601
        void InsertUnlocks(string kind, IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                SqliteCommand command = CreateCommand(connection, insertUnlock, transaction);
                command.Parameters.AddWithValue("$kind", kind);
                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$at", now);
                command.ExecuteNonQuery();
            }
        }
        InsertUnlocks(KindAbility, meta.UnlockedAbilities);
        InsertUnlocks(KindCompanion, meta.UnlockedCompanions);
        InsertUnlocks(KindWorld, meta.LiberatedWorlds);
        InsertUnlocks(KindPrison, meta.RescuedPrisons);

        // Missionen komplett neu schreiben: abgebrochene Bitten verschwinden so auch aus der DB
        CreateCommand(connection, "DELETE FROM missions;", transaction).ExecuteNonQuery();
        foreach (var (id, progress) in meta.Missions)
        {
            SqliteCommand command = CreateCommand(connection, "INSERT INTO missions (id, status, progress) VALUES ($id, $status, $progress);", transaction);
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$status", progress.Status.ToString());
            command.Parameters.AddWithValue("$progress", progress.Progress);
            command.ExecuteNonQuery();
        }

        // Vorgemerkter Fortschritt (Status "pending" statt Active/Completed)
        const string upsertPending = "INSERT INTO missions (id, status, progress) VALUES ($id, 'pending', $progress) ON CONFLICT(id) DO UPDATE SET progress = excluded.progress;";
        foreach (var (id, progress) in meta.PendingMissionProgress)
        {
            SqliteCommand command = CreateCommand(connection, upsertPending, transaction);
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$progress", progress);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    // ------------------------------------------------------------------ Lauf
    public RunState? LoadRun()
    {
        using SqliteConnection connection = Open();
        RunState run;
        using (SqliteDataReader reader = CreateCommand(connection,
                   "SELECT class_id, world_id, circle_index, dungeon_index, level, experience, seed FROM run WHERE id = 1;").ExecuteReader())
        {
            if (!reader.Read()) return null;
            run = new RunState
            {
                ClassId = reader.GetString(0),
                WorldId = reader.GetString(1),
                CircleIndex = reader.GetInt32(2),
                DungeonIndex = reader.GetInt32(3),
                Level = reader.GetInt32(4),
                Experience = (float)reader.GetDouble(5),
                Seed = reader.GetInt32(6),
            };
        }

        using (SqliteDataReader items = CreateCommand(connection, "SELECT kind, id, value FROM run_items;").ExecuteReader())
        {
            while (items.Read())
            {
                string id = items.GetString(1);
                int value = items.GetInt32(2);
                switch (items.GetString(0))
                {
                    case KindAbility: run.AbilityLevels[id] = value; break;
                    case KindUpgrade: run.UpgradeStacks[id] = value; break;
                    case KindCompanion: run.CompanionIds.Add(id); break;
                    case KindItem: run.Items.AddRange(Enumerable.Repeat(id, Math.Max(1, value))); break;   // Anzahl -> Duplikate
                    case KindEquipped: run.Equipped[(ItemSlot)value] = id; break;   // Cast int -> Enum
                }
            }
        }

        var profile = new Dictionary<string, string>();
        using (SqliteDataReader reader = CreateCommand(connection, "SELECT key, value FROM run_profile;").ExecuteReader())
        {
            while (reader.Read()) profile[reader.GetString(0)] = reader.GetString(1);
        }
        run.Appearance = new CharacterAppearance(
            profile.GetValueOrDefault("name", CharacterAppearance.Default.Name),
            (int)ReadLong(profile, "skin"),
            (int)ReadLong(profile, "hair_style"),
            (int)ReadLong(profile, "hair_color"),
            (int)ReadLong(profile, "accent"),
            // Neu hinzugekommene Felder. run_profile ist eine Schlüssel/Wert-Tabelle, fehlende
            // Schlüssel lesen sich als 0 – ältere Spielstände brauchen also keine Migration.
            (int)ReadLong(profile, "body_type"),
            (int)ReadLong(profile, "makeup"),
            (int)ReadLong(profile, "makeup_color"),
            (int)ReadLong(profile, "wings"));
        run.ArmorDurability = (int)ReadLong(profile, "armor_durability");
        return run;
    }

    public void SaveRun(RunState run)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        SqliteCommand upsertRun = CreateCommand(connection, """
            INSERT OR REPLACE INTO run (id, class_id, world_id, circle_index, dungeon_index, level, experience, seed)
            VALUES (1, $class, $world, $circle, $dungeon, $level, $experience, $seed);
            """, transaction);
        upsertRun.Parameters.AddWithValue("$class", run.ClassId);
        upsertRun.Parameters.AddWithValue("$world", run.WorldId);
        upsertRun.Parameters.AddWithValue("$circle", run.CircleIndex);
        upsertRun.Parameters.AddWithValue("$dungeon", run.DungeonIndex);
        upsertRun.Parameters.AddWithValue("$level", run.Level);
        upsertRun.Parameters.AddWithValue("$experience", run.Experience);
        upsertRun.Parameters.AddWithValue("$seed", run.Seed);
        upsertRun.ExecuteNonQuery();

        CreateCommand(connection, "DELETE FROM run_items;", transaction).ExecuteNonQuery();
        const string insertItem = "INSERT INTO run_items (kind, id, value) VALUES ($kind, $id, $value);";
        // Concat + Select: alle drei Listen in eine einheitliche Folge von (kind, id, value)-Tupeln überführen
        IEnumerable<(string Kind, string Id, int Value)> items =
            run.AbilityLevels.Select(pair => (Kind: KindAbility, Id: pair.Key, Value: pair.Value))
               .Concat(run.UpgradeStacks.Select(pair => (Kind: KindUpgrade, Id: pair.Key, Value: pair.Value)))
               .Concat(run.CompanionIds.Select(id => (Kind: KindCompanion, Id: id, Value: 0)))
               // GroupBy fasst doppelte Items zusammen -> eine Zeile mit Anzahl (Primärschlüssel bleibt eindeutig)
               .Concat(run.Items.GroupBy(id => id).Select(group => (Kind: KindItem, Id: group.Key, Value: group.Count())))
               .Concat(run.Equipped.Select(pair => (Kind: KindEquipped, Id: pair.Value, Value: (int)pair.Key)));
        foreach (var (kind, id, value) in items)
        {
            SqliteCommand command = CreateCommand(connection, insertItem, transaction);
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }

        CreateCommand(connection, "DELETE FROM run_profile;", transaction).ExecuteNonQuery();
        CharacterAppearance look = run.Appearance;
        var profileValues = new (string Key, string Value)[]
        {
            ("name", look.Name),
            ("skin", look.SkinTone.ToString(CultureInfo.InvariantCulture)),
            ("hair_style", look.HairStyle.ToString(CultureInfo.InvariantCulture)),
            ("hair_color", look.HairColor.ToString(CultureInfo.InvariantCulture)),
            ("accent", look.AccentColor.ToString(CultureInfo.InvariantCulture)),
            ("body_type", look.BodyType.ToString(CultureInfo.InvariantCulture)),
            ("makeup", look.Makeup.ToString(CultureInfo.InvariantCulture)),
            ("makeup_color", look.MakeupColor.ToString(CultureInfo.InvariantCulture)),
            ("wings", look.Wings.ToString(CultureInfo.InvariantCulture)),
            ("armor_durability", run.ArmorDurability.ToString(CultureInfo.InvariantCulture)),
        };
        foreach (var (key, value) in profileValues)
        {
            SqliteCommand command = CreateCommand(connection, "INSERT INTO run_profile (key, value) VALUES ($key, $value);", transaction);
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void DeleteRun()
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        CreateCommand(connection, "DELETE FROM run_items; DELETE FROM run_profile; DELETE FROM run;", transaction).ExecuteNonQuery();
        transaction.Commit();
    }

    // ------------------------------------------------------------------ Einstellungen
    public GameSettings LoadSettings()
    {
        var settings = new GameSettings();
        var values = new Dictionary<string, string>();
        using SqliteConnection connection = Open();
        using (SqliteDataReader reader = CreateCommand(connection, "SELECT key, value FROM settings;").ExecuteReader())
        {
            while (reader.Read()) values[reader.GetString(0)] = reader.GetString(1);
        }
        // Fehlt ein Schlüssel (frische Installation, neu hinzugekommene Option), bleibt der
        // Standardwert aus GameSettings stehen. Ohne diese Rückfallwerte wäre ein neues Spiel
        // stumm, weil ein fehlender Lautstärkewert sonst als 0 gelesen würde.
        // Ausnahme screen_scale: 0 heißt bewusst "nicht gesetzt" – GameContext setzt dann
        // die in balance.json hinterlegte Standardgröße ein.
        settings.ScreenScale = (int)ReadLong(values, "screen_scale", 0);
        settings.Fullscreen = ReadLong(values, "fullscreen", settings.Fullscreen ? 1 : 0) != 0;
        settings.VSync = ReadLong(values, "vsync", settings.VSync ? 1 : 0) != 0;
        settings.MasterVolume = ReadFloat(values, "master_volume", settings.MasterVolume);
        settings.MusicVolume = ReadFloat(values, "music_volume", settings.MusicVolume);
        settings.SfxVolume = ReadFloat(values, "sfx_volume", settings.SfxVolume);
        settings.AmbientLift = ReadFloat(values, "ambient_lift", settings.AmbientLift);
        settings.RumbleIntensity = ReadFloat(values, "rumble", settings.RumbleIntensity);
        settings.ShowDamageNumbers = ReadLong(values, "damage_numbers", settings.ShowDamageNumbers ? 1 : 0) != 0;
        if (values.TryGetValue("difficulty", out string? difficulty) && difficulty.Length > 0) settings.DifficultyId = difficulty;
        // Bewusst KEIN Sanitize() hier: es würde die 0 bei screen_scale auf 1 klemmen und damit
        // die Unterscheidung "nicht gesetzt" zerstören. Der Aufrufer (GameContext) setzt erst den
        // Standard aus balance.json ein und klemmt danach.
        return settings;
    }

    public void SaveSettings(GameSettings settings)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        const string upsert = "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        void Write(string key, string value)
        {
            SqliteCommand command = CreateCommand(connection, upsert, transaction);
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
        Write("screen_scale", settings.ScreenScale.ToString(CultureInfo.InvariantCulture));
        Write("fullscreen", settings.Fullscreen ? "1" : "0");
        Write("vsync", settings.VSync ? "1" : "0");
        Write("master_volume", settings.MasterVolume.ToString("0.###", CultureInfo.InvariantCulture));
        Write("music_volume", settings.MusicVolume.ToString("0.###", CultureInfo.InvariantCulture));
        Write("sfx_volume", settings.SfxVolume.ToString("0.###", CultureInfo.InvariantCulture));
        Write("ambient_lift", settings.AmbientLift.ToString("0.###", CultureInfo.InvariantCulture));
        Write("rumble", settings.RumbleIntensity.ToString("0.###", CultureInfo.InvariantCulture));
        Write("damage_numbers", settings.ShowDamageNumbers ? "1" : "0");
        Write("difficulty", settings.DifficultyId);
        transaction.Commit();
    }

    // ------------------------------------------------------------------ Heimwelt
    public List<HubDecoPlacement> LoadHubDeco()
    {
        var placements = new List<HubDecoPlacement>();
        using SqliteConnection connection = Open();
        using SqliteDataReader reader = CreateCommand(connection, "SELECT prop_id, tile_x, tile_y FROM hub_deco ORDER BY id;").ExecuteReader();
        {
            while (reader.Read())
                placements.Add(new HubDecoPlacement(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
        }
        return placements;
    }

    public void SaveHubDeco(IEnumerable<HubDecoPlacement> placements)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        CreateCommand(connection, "DELETE FROM hub_deco;", transaction).ExecuteNonQuery();
        const string insert = "INSERT INTO hub_deco (prop_id, tile_x, tile_y) VALUES ($prop, $x, $y);";
        foreach (HubDecoPlacement placement in placements)
        {
            SqliteCommand command = CreateCommand(connection, insert, transaction);
            command.Parameters.AddWithValue("$prop", placement.PropId);
            command.Parameters.AddWithValue("$x", placement.TileX);
            command.Parameters.AddWithValue("$y", placement.TileY);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public List<PetState> LoadPets()
    {
        var pets = new List<PetState>();
        using SqliteConnection connection = Open();
        using SqliteDataReader reader = CreateCommand(connection, "SELECT companion_id, name, loyalty, fed FROM pets;").ExecuteReader();
        {
            while (reader.Read())
                pets.Add(new PetState(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)));
        }
        return pets;
    }

    public void SavePets(IEnumerable<PetState> pets)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        CreateCommand(connection, "DELETE FROM pets;", transaction).ExecuteNonQuery();
        const string insert = "INSERT INTO pets (companion_id, name, loyalty, fed, petted_at) VALUES ($id, $name, $loyalty, $fed, $at);";
        string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        foreach (PetState pet in pets)
        {
            SqliteCommand command = CreateCommand(connection, insert, transaction);
            command.Parameters.AddWithValue("$id", pet.CompanionId);
            command.Parameters.AddWithValue("$name", pet.Name);
            command.Parameters.AddWithValue("$loyalty", pet.Loyalty);
            command.Parameters.AddWithValue("$fed", pet.Fed);
            command.Parameters.AddWithValue("$at", now);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public Dictionary<string, int> LoadCollectibles()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using SqliteConnection connection = Open();
        using SqliteDataReader reader = CreateCommand(connection, "SELECT item_id, count FROM collectibles;").ExecuteReader();
        {
            while (reader.Read()) counts[reader.GetString(0)] = reader.GetInt32(1);
        }
        return counts;
    }

    public void SaveCollectibles(IReadOnlyDictionary<string, int> counts)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        CreateCommand(connection, "DELETE FROM collectibles;", transaction).ExecuteNonQuery();
        const string insert = "INSERT INTO collectibles (item_id, count) VALUES ($id, $count);";
        foreach (var (itemId, count) in counts)
        {
            SqliteCommand command = CreateCommand(connection, insert, transaction);
            command.Parameters.AddWithValue("$id", itemId);
            command.Parameters.AddWithValue("$count", count);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    // ------------------------------------------------------------------ Helfer
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    private static long ReadLong(Dictionary<string, string> values, string key, long fallback = 0L) =>
        values.TryGetValue(key, out string? raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : fallback;

    private static float ReadFloat(Dictionary<string, string> values, string key, float fallback = 0f) =>
        values.TryGetValue(key, out string? raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : fallback;
}
