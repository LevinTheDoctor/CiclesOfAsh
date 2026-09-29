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
///  * Ab Version 5 gibt es mehrere Gestalten (Tabelle characters); runs, run_items und
///    run_profile tragen die character_id ihrer Gestalt.
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
        // Version 4: Farbfassung ("Skin") je Begleitseele. Default 0 = Grundfassung,
        // ältere Spielstände sehen damit unverändert aus.
        """
        ALTER TABLE pets ADD COLUMN skin INTEGER NOT NULL DEFAULT 0;
        """,
        // Version 5: mehrere Gestalten (Charaktere) mit je einem Lauf. Das Aussehen zieht aus
        // run_profile in die neue Tabelle characters; runs, run_items und run_profile hängen jetzt
        // an character_id. Ein vorhandener Lauf wird zur Gestalt Nr. 1 – kein Spielstand geht verloren.
        // SQLite kann einen Primärschlüssel nicht per ALTER TABLE ändern. Deshalb das von SQLite
        // empfohlene Vorgehen: neue Tabelle anlegen, Daten umkopieren, alte löschen, neue umbenennen.
        """
        CREATE TABLE characters (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            name           TEXT    NOT NULL,
            skin           INTEGER NOT NULL DEFAULT 0,
            hair_style     INTEGER NOT NULL DEFAULT 0,
            hair_color     INTEGER NOT NULL DEFAULT 0,
            accent         INTEGER NOT NULL DEFAULT 0,
            body_type      INTEGER NOT NULL DEFAULT 0,
            makeup         INTEGER NOT NULL DEFAULT 0,
            makeup_color   INTEGER NOT NULL DEFAULT 0,
            wings          INTEGER NOT NULL DEFAULT 0,
            last_class_id  TEXT    NOT NULL DEFAULT '',
            created_at     TEXT    NOT NULL,
            last_played_at TEXT    NOT NULL,
            runs           INTEGER NOT NULL DEFAULT 0,
            deaths         INTEGER NOT NULL DEFAULT 0,
            deepest_circle INTEGER NOT NULL DEFAULT 0,
            arena_wins     INTEGER NOT NULL DEFAULT 0
        );
        -- Pivot: Die Schlüssel/Wert-Zeilen aus run_profile werden zu Spalten. Pro Schlüssel liefert
        -- CASE nur in "seiner" Zeile einen Wert, MAX() sammelt ihn über die Gruppe ein.
        -- LEFT JOIN ... ON 1 = 1: auch ohne run_profile-Zeilen entsteht genau eine Gestalt.
        INSERT INTO characters (id, name, skin, hair_style, hair_color, accent, body_type, makeup, makeup_color, wings,
                                last_class_id, created_at, last_played_at, runs, deepest_circle)
        SELECT 1,
               COALESCE(MAX(CASE WHEN p.key = 'name' THEN p.value END), 'Namenloser'),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'skin'         THEN p.value END), '0') AS INTEGER),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'hair_style'   THEN p.value END), '0') AS INTEGER),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'hair_color'   THEN p.value END), '0') AS INTEGER),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'accent'       THEN p.value END), '0') AS INTEGER),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'body_type'    THEN p.value END), '0') AS INTEGER),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'makeup'       THEN p.value END), '0') AS INTEGER),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'makeup_color' THEN p.value END), '0') AS INTEGER),
               CAST(COALESCE(MAX(CASE WHEN p.key = 'wings'        THEN p.value END), '0') AS INTEGER),
               r.class_id,
               strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
               strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
               1,
               r.circle_index + 1
        FROM run AS r LEFT JOIN run_profile AS p ON 1 = 1
        WHERE r.id = 1
        GROUP BY r.id;

        CREATE TABLE runs (
            character_id  INTEGER PRIMARY KEY,
            class_id      TEXT    NOT NULL,
            world_id      TEXT    NOT NULL,
            circle_index  INTEGER NOT NULL,
            dungeon_index INTEGER NOT NULL,
            level         INTEGER NOT NULL,
            experience    REAL    NOT NULL,
            seed          INTEGER NOT NULL
        );
        INSERT INTO runs (character_id, class_id, world_id, circle_index, dungeon_index, level, experience, seed)
        SELECT 1, class_id, world_id, circle_index, dungeon_index, level, experience, seed FROM run WHERE id = 1;

        CREATE TABLE character_run_items (
            character_id INTEGER NOT NULL,
            kind         TEXT    NOT NULL,
            id           TEXT    NOT NULL,
            value        INTEGER NOT NULL,
            PRIMARY KEY (character_id, kind, id)
        );
        INSERT INTO character_run_items (character_id, kind, id, value)
        SELECT 1, kind, id, value FROM run_items WHERE EXISTS (SELECT 1 FROM run WHERE id = 1);

        CREATE TABLE character_run_profile (
            character_id INTEGER NOT NULL,
            key          TEXT    NOT NULL,
            value        TEXT    NOT NULL,
            PRIMARY KEY (character_id, key)
        );
        -- Nur, was zum Lauf gehört (Kleidung, Schmiede, Unterwäsche). Das Aussehen steht jetzt in characters.
        INSERT INTO character_run_profile (character_id, key, value)
        SELECT 1, key, value FROM run_profile
        WHERE key NOT IN ('name', 'skin', 'hair_style', 'hair_color', 'accent', 'body_type', 'makeup', 'makeup_color', 'wings')
          AND EXISTS (SELECT 1 FROM run WHERE id = 1);

        DROP TABLE run_items;
        DROP TABLE run_profile;
        DROP TABLE run;
        ALTER TABLE character_run_items RENAME TO run_items;
        ALTER TABLE character_run_profile RENAME TO run_profile;

        INSERT OR REPLACE INTO meta (key, value)
        SELECT 'active_character', '1' WHERE EXISTS (SELECT 1 FROM characters WHERE id = 1);
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
            meta.ActiveCharacterId = (int)ReadLong(values, "active_character");
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
        var metaValues = new (string Key, long Value)[]
        {
            ("believers", meta.Believers),
            ("deaths", meta.Deaths),
            ("runs_started", meta.RunsStarted),
            ("active_character", meta.ActiveCharacterId),
        };
        foreach (var (key, value) in metaValues)
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

    // ------------------------------------------------------------------ Gestalten
    public List<SavedCharacter> LoadCharacters()
    {
        var characters = new List<SavedCharacter>();
        using SqliteConnection connection = Open();
        // LEFT JOIN: Jede Gestalt kommt vor, auch ohne Lauf – dann sind die Spalten aus runs NULL.
        using SqliteDataReader reader = CreateCommand(connection, """
            SELECT c.id, c.name, c.skin, c.hair_style, c.hair_color, c.accent, c.body_type, c.makeup, c.makeup_color, c.wings,
                   c.last_class_id, c.created_at, c.last_played_at, c.runs, c.deaths, c.deepest_circle, c.arena_wins,
                   r.class_id, r.world_id, r.circle_index, r.dungeon_index, r.level
            FROM characters AS c LEFT JOIN runs AS r ON r.character_id = c.id
            ORDER BY c.last_played_at DESC, c.id DESC;
            """).ExecuteReader();
        while (reader.Read())
        {
            characters.Add(new SavedCharacter
            {
                Id = reader.GetInt32(0),
                Appearance = new CharacterAppearance(reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                    reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8),
                    reader.GetInt32(9)),
                LastClassId = reader.GetString(10),
                CreatedAt = ReadDate(reader.GetString(11)),
                LastPlayedAt = ReadDate(reader.GetString(12)),
                Runs = reader.GetInt32(13),
                Deaths = reader.GetInt32(14),
                DeepestCircle = reader.GetInt32(15),
                ArenaWins = reader.GetInt32(16),
                // IsDBNull: Ohne Lauf liefert der LEFT JOIN NULL statt einer Klassen-Id.
                CurrentRun = reader.IsDBNull(17)
                    ? null
                    : new RunSummary(reader.GetString(17), reader.GetString(18), reader.GetInt32(19),
                                     reader.GetInt32(20), reader.GetInt32(21)),
            });
        }
        return characters;
    }

    public void SaveCharacter(SavedCharacter character)
    {
        (string Column, object Value)[] columns = CharacterColumns(character);
        bool isNew = character.Id == 0;
        // Spaltennamen sind feste Konstanten aus CharacterColumns -> Einsetzen per Interpolation ist
        // sicher. Die WERTE laufen weiter über Parameter.
        string sql = isNew
            ? $"INSERT INTO characters ({string.Join(", ", columns.Select(column => column.Column))}) " +
              $"VALUES ({string.Join(", ", columns.Select(column => "$" + column.Column))}); SELECT last_insert_rowid();"
            : $"UPDATE characters SET {string.Join(", ", columns.Select(column => $"{column.Column} = ${column.Column}"))} WHERE id = $id;";

        using SqliteConnection connection = Open();
        SqliteCommand command = CreateCommand(connection, sql);
        foreach (var (column, value) in columns) command.Parameters.AddWithValue("$" + column, value);
        if (isNew)
        {
            // last_insert_rowid() = die Id, die SQLite eben vergeben hat (gilt je Verbindung).
            character.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return;
        }
        command.Parameters.AddWithValue("$id", character.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteCharacter(int characterId)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        DeleteRunRows(connection, transaction, characterId);
        SqliteCommand command = CreateCommand(connection, "DELETE FROM characters WHERE id = $id;", transaction);
        command.Parameters.AddWithValue("$id", characterId);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    /// <summary>
    /// Spalte -> Wert einer Gestalt. EINE Liste steuert INSERT, UPDATE und die Parameter (DRY):
    /// Eine neue Spalte braucht hier eine Zeile – plus die Migration, die sie anlegt.
    /// </summary>
    private static (string Column, object Value)[] CharacterColumns(SavedCharacter character)
    {
        CharacterAppearance look = character.Appearance;
        return new (string Column, object Value)[]
        {
            ("name", look.Name),
            ("skin", look.SkinTone),
            ("hair_style", look.HairStyle),
            ("hair_color", look.HairColor),
            ("accent", look.AccentColor),
            ("body_type", look.BodyType),
            ("makeup", look.Makeup),
            ("makeup_color", look.MakeupColor),
            ("wings", look.Wings),
            ("last_class_id", character.LastClassId),
            ("created_at", WriteDate(character.CreatedAt)),
            ("last_played_at", WriteDate(character.LastPlayedAt)),
            ("runs", character.Runs),
            ("deaths", character.Deaths),
            ("deepest_circle", character.DeepestCircle),
            ("arena_wins", character.ArenaWins),
        };
    }

    // ------------------------------------------------------------------ Lauf einer Gestalt
    public RunState? LoadRun(int characterId)
    {
        using SqliteConnection connection = Open();
        RunState run;
        using (SqliteDataReader reader = CreateCharacterCommand(connection,
                   "SELECT class_id, world_id, circle_index, dungeon_index, level, experience, seed FROM runs WHERE character_id = $character;",
                   characterId).ExecuteReader())
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

        using (SqliteDataReader items = CreateCharacterCommand(connection,
                   "SELECT kind, id, value FROM run_items WHERE character_id = $character;", characterId).ExecuteReader())
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
        using (SqliteDataReader reader = CreateCharacterCommand(connection,
                   "SELECT key, value FROM run_profile WHERE character_id = $character;", characterId).ExecuteReader())
        {
            while (reader.Read()) profile[reader.GetString(0)] = reader.GetString(1);
        }
        // Das Aussehen gehört seit Version 5 der Gestalt (Tabelle characters) – der Aufrufer setzt es.
        run.ArmorDurability = (int)ReadLong(profile, "armor_durability");
        run.ArmorWear = ReadWear(profile, "armor_wear");
        run.ForgeUses = (int)ReadLong(profile, "forge_uses");
        run.Underwear = (int)ReadLong(profile, "underwear");
        return run;
    }

    public void SaveRun(int characterId, RunState run)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        SqliteCommand upsertRun = CreateCharacterCommand(connection, """
            INSERT OR REPLACE INTO runs (character_id, class_id, world_id, circle_index, dungeon_index, level, experience, seed)
            VALUES ($character, $class, $world, $circle, $dungeon, $level, $experience, $seed);
            """, characterId, transaction);
        upsertRun.Parameters.AddWithValue("$class", run.ClassId);
        upsertRun.Parameters.AddWithValue("$world", run.WorldId);
        upsertRun.Parameters.AddWithValue("$circle", run.CircleIndex);
        upsertRun.Parameters.AddWithValue("$dungeon", run.DungeonIndex);
        upsertRun.Parameters.AddWithValue("$level", run.Level);
        upsertRun.Parameters.AddWithValue("$experience", run.Experience);
        upsertRun.Parameters.AddWithValue("$seed", run.Seed);
        upsertRun.ExecuteNonQuery();

        CreateCharacterCommand(connection, "DELETE FROM run_items WHERE character_id = $character;", characterId, transaction).ExecuteNonQuery();
        const string insertItem = "INSERT INTO run_items (character_id, kind, id, value) VALUES ($character, $kind, $id, $value);";
        // Concat + Select: alle Listen in eine einheitliche Folge von (kind, id, value)-Tupeln überführen
        IEnumerable<(string Kind, string Id, int Value)> items =
            run.AbilityLevels.Select(pair => (Kind: KindAbility, Id: pair.Key, Value: pair.Value))
               .Concat(run.UpgradeStacks.Select(pair => (Kind: KindUpgrade, Id: pair.Key, Value: pair.Value)))
               .Concat(run.CompanionIds.Select(id => (Kind: KindCompanion, Id: id, Value: 0)))
               // GroupBy fasst doppelte Items zusammen -> eine Zeile mit Anzahl (Primärschlüssel bleibt eindeutig)
               .Concat(run.Items.GroupBy(id => id).Select(group => (Kind: KindItem, Id: group.Key, Value: group.Count())))
               .Concat(run.Equipped.Select(pair => (Kind: KindEquipped, Id: pair.Value, Value: (int)pair.Key)));
        foreach (var (kind, id, value) in items)
        {
            SqliteCommand command = CreateCharacterCommand(connection, insertItem, characterId, transaction);
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }

        CreateCharacterCommand(connection, "DELETE FROM run_profile WHERE character_id = $character;", characterId, transaction).ExecuteNonQuery();
        var profileValues = new (string Key, string Value)[]
        {
            ("armor_durability", run.ArmorDurability.ToString(CultureInfo.InvariantCulture)),
            ("armor_wear", WriteWear(run.ArmorWear)),
            ("forge_uses", run.ForgeUses.ToString(CultureInfo.InvariantCulture)),
            ("underwear", run.Underwear.ToString(CultureInfo.InvariantCulture)),
        };
        foreach (var (key, value) in profileValues)
        {
            SqliteCommand command = CreateCharacterCommand(connection,
                "INSERT INTO run_profile (character_id, key, value) VALUES ($character, $key, $value);", characterId, transaction);
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void DeleteRun(int characterId)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        DeleteRunRows(connection, transaction, characterId);
        transaction.Commit();
    }

    /// <summary>
    /// Löscht alle Zeilen eines Laufs. SQLite prüft Fremdschlüssel nur mit "PRAGMA foreign_keys = ON"
    /// (und das pro Verbindung) – statt sich darauf zu verlassen, räumt der Code die Tabellen selbst auf.
    /// </summary>
    private static void DeleteRunRows(SqliteConnection connection, SqliteTransaction transaction, int characterId)
    {
        // Tabellennamen sind Konstanten (kein Nutzertext) -> Interpolation ist hier unbedenklich.
        foreach (string table in new[] { "run_items", "run_profile", "runs" })
            CreateCharacterCommand(connection, $"DELETE FROM {table} WHERE character_id = $character;", characterId, transaction)
                .ExecuteNonQuery();
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
        settings.Tutorial = ReadLong(values, "tutorial", settings.Tutorial ? 1 : 0) != 0;
        settings.ManualBossFights = ReadLong(values, "manual_boss", settings.ManualBossFights ? 1 : 0) != 0;
        if (values.TryGetValue("difficulty", out string? difficulty) && difficulty.Length > 0) settings.DifficultyId = difficulty;
        // Neu ohne Migration: settings ist Schlüssel/Wert, ein alter Spielstand hat den Schlüssel
        // einfach noch nicht und bleibt bei der Standardsprache.
        if (values.TryGetValue("language", out string? language) && language.Length > 0) settings.Language = language;
        settings.ArenaAddress = values.GetValueOrDefault("arena_address", "");
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
        Write("tutorial", settings.Tutorial ? "1" : "0");
        Write("manual_boss", settings.ManualBossFights ? "1" : "0");
        Write("difficulty", settings.DifficultyId);
        Write("language", settings.Language);
        Write("arena_address", settings.ArenaAddress);
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
        using SqliteDataReader reader = CreateCommand(connection, "SELECT companion_id, name, loyalty, fed, skin FROM pets;").ExecuteReader();
        {
            while (reader.Read())
                pets.Add(new PetState(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3))
                {
                    Skin = reader.GetInt32(4),
                });
        }
        return pets;
    }

    public void SavePets(IEnumerable<PetState> pets)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        CreateCommand(connection, "DELETE FROM pets;", transaction).ExecuteNonQuery();
        const string insert = "INSERT INTO pets (companion_id, name, loyalty, fed, petted_at, skin) VALUES ($id, $name, $loyalty, $fed, $at, $skin);";
        string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        foreach (PetState pet in pets)
        {
            SqliteCommand command = CreateCommand(connection, insert, transaction);
            command.Parameters.AddWithValue("$id", pet.CompanionId);
            command.Parameters.AddWithValue("$name", pet.Name);
            command.Parameters.AddWithValue("$loyalty", pet.Loyalty);
            command.Parameters.AddWithValue("$fed", pet.Fed);
            command.Parameters.AddWithValue("$at", now);
            command.Parameters.AddWithValue("$skin", pet.Skin);
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

    /// <summary>Befehl mit schon gebundenem $character – jede Abfrage eines Laufs filtert nach seiner Gestalt.</summary>
    private static SqliteCommand CreateCharacterCommand(SqliteConnection connection, string sql, int characterId,
                                                        SqliteTransaction? transaction = null)
    {
        SqliteCommand command = CreateCommand(connection, sql, transaction);
        command.Parameters.AddWithValue("$character", characterId);
        return command;
    }

    /// <summary>Zeitpunkte als ISO-8601 in UTC ("O"): sortierbar als Text und ohne Zeitzonen-Rätsel.</summary>
    private static string WriteDate(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    /// <summary>RoundtripKind: Das "Z" am Ende bleibt als UTC erhalten, statt in Ortszeit umgerechnet zu werden.</summary>
    private static DateTime ReadDate(string raw) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed
            : DateTime.UnixEpoch;

    /// <summary>
    /// Zustand abgelegter Kleidung als eine Zeile: "id:treffer;id:treffer". Bewusst KEINE eigene
    /// Tabelle: run_profile ist Schlüssel/Wert, ein fehlender Schlüssel liest sich als leer, und
    /// damit kamen ältere Spielstände ohne eigene Migration aus.
    /// </summary>
    private static Dictionary<string, int> ReadWear(Dictionary<string, string> values, string key)
    {
        var wear = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!values.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw)) return wear;
        foreach (string entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = entry.LastIndexOf(':');
            if (separator <= 0) continue;
            if (int.TryParse(entry[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hits))
                wear[entry[..separator]] = hits;
        }
        return wear;
    }

    private static string WriteWear(Dictionary<string, int> wear) =>
        string.Join(';', wear.Select(entry => $"{entry.Key}:{entry.Value.ToString(CultureInfo.InvariantCulture)}"));

    private static long ReadLong(Dictionary<string, string> values, string key, long fallback = 0L) =>
        values.TryGetValue(key, out string? raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : fallback;

    private static float ReadFloat(Dictionary<string, string> values, string key, float fallback = 0f) =>
        values.TryGetValue(key, out string? raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : fallback;
}
