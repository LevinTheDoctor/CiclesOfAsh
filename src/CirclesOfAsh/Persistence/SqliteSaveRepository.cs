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
                if (!Enum.TryParse(reader.GetString(1), out MissionStatus status)) continue;
                meta.Missions[reader.GetString(0)] = new MissionProgress { Status = status, Progress = reader.GetInt32(2) };
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
            (int)ReadLong(profile, "accent"));
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

    private static long ReadLong(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : 0L;
}
