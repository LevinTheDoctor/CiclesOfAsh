using CirclesOfAsh.Abilities;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using CirclesOfAsh.World;

namespace CirclesOfAsh.UI;

/// <summary>Kampf-Oberfläche: Leben, Mana, Seelen/Stufe, Gläubige, Fähigkeiten, Minikarte, Boss-Leiste, Ansagen.</summary>
public sealed class Hud
{
    private const int MinimapCellWidth = 7;
    private const int MinimapCellHeight = 4;
    private readonly GameContext _context;

    public Hud(GameContext context) => _context = context;

    public void Draw(SpriteBatch spriteBatch, DungeonWorld world)
    {
        Texture2D pixel = _context.Assets.Pixel;
        var font = _context.Font;
        var player = world.Player;
        UiDraw.Begin(spriteBatch);

        // Links oben: Ressourcen
        UiDraw.Bar(spriteBatch, pixel, new Rectangle(6, 6, 90, 7), player.Health.Ratio, Palette.Blood);
        UiDraw.Bar(spriteBatch, pixel, new Rectangle(6, 15, 70, 5), player.Mana / MathF.Max(1f, player.MaxMana), Palette.Mana);
        font.DrawShadowed(spriteBatch, $"{player.Health.Current:0}/{player.Health.Max:0}", new Vector2(100, 5), Palette.Bone);

        // Unten: Seelenleiste (XP) über die volle Breite
        float needed = _context.Progression.ExperienceForNextLevel(world.Run.Level);
        UiDraw.Bar(spriteBatch, pixel, new Rectangle(0, CirclesGame.VirtualHeight - 4, CirclesGame.VirtualWidth, 4), world.Run.Experience / needed, Palette.Soul);
        font.DrawShadowed(spriteBatch, $"Stufe {world.Run.Level}", new Vector2(6, CirclesGame.VirtualHeight - 16), Palette.Soul);

        // Oben mittig: Ort + Wellenstatus
        DungeonPlan plan = world.Plan;
        string location = plan.IsBossDungeon ? $"{plan.Circle.Name} · Thronsaal" : $"{plan.Circle.Name} · Verlies {plan.DungeonIndex + 1}";
        font.DrawCentered(spriteBatch, location, CirclesGame.VirtualWidth / 2f, 4, Palette.Bone * 0.9f);
        string? waveText = world.Waves.StatusText;
        string progress = waveText ?? (plan.IsBossDungeon ? "" : $"Arenen {world.Waves.ClearedArenas}/{world.Waves.TotalArenas}");
        font.DrawCentered(spriteBatch, progress, CirclesGame.VirtualWidth / 2f, 14, waveText is null ? Palette.Ash : Palette.Ember);
        // Verbliebene Gegner des AKTIVEN Kampfes: Ist die Welle vermeintlich leer, sieht der
        // Spieler hier sofort, dass noch etwas lebt (z. B. ein getarnter Egel in einer Ecke).
        if (world.Waves.IsFighting && world.Waves.ActiveArena is { } fight)
        {
            int enemiesLeft = world.AliveEnemyCountOf(fight.OwnerKey);
            if (enemiesLeft > 0)
                font.DrawCentered(spriteBatch, $"Verdammte: {enemiesLeft}", CirclesGame.VirtualWidth / 2f, 24, Palette.Ember * 0.8f);
        }
        if (world.Puzzle is { IsSolved: false } puzzle)   // Property-Pattern: nicht null UND noch nicht gelöst
            font.DrawCentered(spriteBatch, puzzle.Hint, CirclesGame.VirtualWidth / 2f, 34, Palette.Soul);

        // Gläubige unter der Minikarte
        string believers = $"Gläubige {_context.Progression.Meta.Believers}";
        font.DrawShadowed(spriteBatch, believers, new Vector2(CirclesGame.VirtualWidth - 6 - font.MeasureWidth(believers), 44), Palette.Faith);

        DrawMissions(spriteBatch, world);
        DrawManualAbilities(spriteBatch, world);
        DrawMinimap(spriteBatch, world);
        DrawBossBar(spriteBatch, world);

        string? announcement = world.CurrentAnnouncement;
        if (announcement is not null) font.DrawCentered(spriteBatch, announcement, CirclesGame.VirtualWidth / 2f, 60, Palette.Faith);
        if (player.IsStealthed) font.DrawCentered(spriteBatch, "– getarnt –", CirclesGame.VirtualWidth / 2f, 72, Palette.Violet);

        spriteBatch.End();
    }

    /// <summary>Aktive Bitten rechts unter den Gläubigen – mit Fortschritt.</summary>
    private void DrawMissions(SpriteBatch spriteBatch, DungeonWorld world)
    {
        var font = _context.Font;
        MissionService missions = _context.Progression.Missions;
        float y = 56;
        foreach (MissionDefinition mission in missions.Active)
        {
            string text = $"{mission.Title} {missions.ProgressOf(mission)}/{mission.Count}";
            font.DrawShadowed(spriteBatch, text, new Vector2(CirclesGame.VirtualWidth - 6 - font.MeasureWidth(text), y), Palette.Bone * 0.8f);
            y += font.LineHeight;
        }
    }

    private void DrawManualAbilities(SpriteBatch spriteBatch, DungeonWorld world)
    {
        var font = _context.Font;
        Texture2D pixel = _context.Assets.Pixel;
        float y = CirclesGame.VirtualHeight - 28;
        float x = 60;
        foreach (AbilityInstance ability in world.Player.Abilities)
        {
            if (ability.Definition.Activation != AbilityActivation.Manual) continue;
            string label = $"{_context.Input.Prompt(ability.Definition.InputAction)} {ability.Definition.Name}";
            float cooldown = ability.EffectiveCooldown(world.Player.Stats);
            float ready = cooldown <= 0f ? 1f : 1f - ability.CooldownRemaining / cooldown;
            int width = font.MeasureWidth(label) + 6;
            UiDraw.Bar(spriteBatch, pixel, new Rectangle((int)x, (int)y + 10, width, 3), ready, ready >= 1f ? Palette.Gold : Palette.Ash);
            font.DrawShadowed(spriteBatch, label, new Vector2(x + 3, y), ready >= 1f ? Palette.Bone : Palette.Ash);
            x += width + 6;
        }
    }

    /// <summary>Metroidvania-Minikarte: besuchte Räume + angrenzende (unbesuchte) Räume als Umriss.</summary>
    private void DrawMinimap(SpriteBatch spriteBatch, DungeonWorld world)
    {
        Texture2D pixel = _context.Assets.Pixel;
        Point grid = world.Layout.GridSize;
        int width = grid.X * MinimapCellWidth + 4;
        int height = grid.Y * MinimapCellHeight + 4;
        var frame = new Rectangle(CirclesGame.VirtualWidth - width - 6, 6, width, height);
        UiDraw.Rect(spriteBatch, pixel, frame, Color.Black * 0.55f);

        foreach (RoomNode room in world.Layout.Rooms)
        {
            bool isKnown = room.IsVisited || HasVisitedNeighbor(world, room);
            if (!isKnown) continue;
            var cell = new Rectangle(frame.Left + 2 + room.GridPosition.X * MinimapCellWidth,
                                     frame.Top + 2 + room.GridPosition.Y * MinimapCellHeight,
                                     MinimapCellWidth - 1, MinimapCellHeight - 1);
            Color color = room.Type switch
            {
                RoomType.Arena => room.IsCleared ? Palette.Ash : Palette.Blood,
                RoomType.Boss => Palette.Blood,
                RoomType.Treasure => Palette.Gold,
                RoomType.Prison => room.IsCleared ? Palette.Soul : Palette.Violet,
                RoomType.Puzzle => Palette.Soul * 0.8f,
                RoomType.Exit => world.IsGoalActive ? Palette.Faith : Palette.Ash,
                _ => Palette.Ash * 0.8f,
            };
            if (!room.IsVisited) color *= 0.35f;
            if (room == world.CurrentRoom) color = Palette.Bone;
            UiDraw.Rect(spriteBatch, pixel, cell, color);
        }
    }

    /// <summary>Ist ein über einen Ausgang verbundener Nachbarraum schon besucht? Dann ist dieser Raum "bekannt".</summary>
    private static bool HasVisitedNeighbor(DungeonWorld world, RoomNode room) =>
        room.Exits.Keys.Any(direction =>
        {
            Point offset = direction switch
            {
                Direction.Left => new Point(-1, 0),
                Direction.Right => new Point(1, 0),
                Direction.Up => new Point(0, -1),
                _ => new Point(0, 1),
            };
            Point neighbor = room.GridPosition + offset;
            return world.Layout.Rooms.Any(other => other.IsVisited && other.GridPosition == neighbor);
        });

    private void DrawBossBar(SpriteBatch spriteBatch, DungeonWorld world)
    {
        var boss = world.ActiveBoss;
        if (boss is null || boss.IsSpawning) return;
        var area = new Rectangle(90, CirclesGame.VirtualHeight - 36, CirclesGame.VirtualWidth - 180, 6);
        UiDraw.Bar(spriteBatch, _context.Assets.Pixel, area, boss.Health.Ratio, Palette.Blood);
        _context.Font.DrawCentered(spriteBatch, boss.Definition.Name, CirclesGame.VirtualWidth / 2f, area.Top - 11, Palette.Bone);
    }
}
