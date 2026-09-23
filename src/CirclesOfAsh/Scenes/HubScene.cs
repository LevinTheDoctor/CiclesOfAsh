using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Persistence;
using CirclesOfAsh.Pets;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Heimwelt-Hub: begehbarer Tempel zwischen den Abstiegen. Hier
///   - steht das Missionsbrett (Bitten annehmen, auch mitten im Spielverlauf),
///   - zeigt der Schrein die gesammelten Reliquien (Collectibles über alle Läufe),
///   - laufen die Begleitseelen frei herum (streicheln/füttern per Dialog = Haustier-System),
///   - beten Gläubige sichtbar (NPCs) und
///   - kann der Spieler Deko frei platzieren (Customization, persistent).
/// </summary>
public sealed class HubScene : SceneBase
{
    private const int HubWidthTiles = 30;
    private const int HubHeightTiles = 17;

    private readonly TileMap _map;
    private readonly Camera2D _camera;
    private readonly List<Npc> _npcs = new();
    private readonly List<Companion> _companions = new();
    private readonly List<Prop> _deco = new();
    private Player _player = null!;
    private readonly LightingSystem _lighting;
    private readonly EffectSystem _effects;
    private float _time;

    // Deko-Modus: Raster-Cursor, Auswahl aus hubfähigen Props
    private bool _decoMode;
    private int _decoCursorX, _decoCursorY;
    private int _decoSelectionIndex;
    private MenuList _decoMenu = new();
    private readonly List<PropDefinition> _decoCatalog = new();

    private static readonly string[] DecoPropIds = { "lamp", "torch", "candles", "statue", "banner", "urn", "bookshelf", "coffin" };

    // Feste Standorte im Tempel. Einmal definiert, damit Erkennung (UpdateHotspots) und
    // Darstellung (Draw*) nie auseinanderlaufen können.
    private static Vector2 BoardSpot => new(4 * TileMap.TileSize + 8, (HubHeightTiles - 5) * TileMap.TileSize);
    private static Vector2 ShrineSpot => new((HubWidthTiles - 4) * TileMap.TileSize + 8, (HubHeightTiles - 5) * TileMap.TileSize);
    /// <summary>Das Höllentor: der Ausgang in den Abstieg, rechts der Tempelmitte auf dem Boden.</summary>
    private static Vector2 GateSpot => new(21 * TileMap.TileSize, (HubHeightTiles - 2) * TileMap.TileSize + 8);
    /// <summary>Die Truhe mit der Ausrüstung des laufenden Abstiegs, links der Tempelmitte.</summary>
    private static Vector2 ChestSpot => new(10 * TileMap.TileSize, (HubHeightTiles - 2) * TileMap.TileSize + 8);
    /// <summary>
    /// Wo der Spieler den Tempel betritt (Mitte der Füße): auf dem Tempelboden zwischen Truhe
    /// und Tempelwärtin, mit dem Höllentor in Blickrichtung rechts.
    /// </summary>
    private static Vector2 PlayerSpawnSpot =>
        new(13 * TileMap.TileSize + TileMap.TileSize / 2f, (HubHeightTiles - 1) * TileMap.TileSize);

    /// <summary>Mietbare Deko-Kosten: Jedes platzierte Stück kostet Gläubige (Wiedervergütung beim Entfernen: 50%).</summary>
    private const int DecoCost = 5;

    public HubScene(GameContext context) : base(context)
    {
        _map = BuildHubMap();
        _camera = new Camera2D(CirclesGame.VirtualWidth, CirclesGame.VirtualHeight);
        _lighting = new LightingSystem(context.GraphicsDevice, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight)
        {
            Ambient = new Color(214, 200, 190),
            // Der Helligkeitsregler aus dem Optionsmenü gilt auch im Tempel – sonst bliebe die
            // Heimwelt stockdunkel, während die Verliese sich aufhellen lassen.
            Brightness = 0.2f + 0.5f * context.Settings.AmbientLift,
        };
        _effects = new EffectSystem(new Random());
    }

    public override void OnEnter()
    {
        _player = PlayerFactory.CreateHubPlayer(Context, PlayerSpawnSpot);
        WarnIfSpawnBlocked();
        _npcs.Clear();
        _companions.Clear();
        _deco.Clear();

        CreateNpcs();
        foreach (Npc npc in _npcs) WarnIfBlocked(npc.Bounds, npc.Definition.Name);
        CreateCompanions();
        LoadDeco();
        RebuildDecoCatalog();

        _camera.SnapTo(_player.Center, _map.PixelBounds);
        Context.Music.Play("music.hub");
        Context.Audio.Play("unseal", 0.3f, 0.4f);
    }

    // ------------------------------------------------------------------ Aufbau
    /// <summary>Festes Tempel-Layout: Boden, zwei Podeste links/rechts, Treppen zur Mitte, Fenster-Nischen.</summary>
    private static TileMap BuildHubMap()
    {
        var map = new TileMap(HubWidthTiles, HubHeightTiles, TileType.Solid);
        map.Fill(new Rectangle(1, 1, HubWidthTiles - 2, HubHeightTiles - 2), TileType.Empty);
        // Boden
        map.Fill(new Rectangle(0, HubHeightTiles - 1, HubWidthTiles, 1), TileType.Solid);
        // Podeste (links und rechts, 3 hoch)
        map.Fill(new Rectangle(2, HubHeightTiles - 4, 5, 3), TileType.Solid);
        map.Fill(new Rectangle(HubWidthTiles - 7, HubHeightTiles - 4, 5, 3), TileType.Solid);
        // Podest-Plattformen oben (begehbar)
        map.Fill(new Rectangle(2, HubHeightTiles - 5, 5, 1), TileType.Platform);
        map.Fill(new Rectangle(HubWidthTiles - 7, HubHeightTiles - 5, 5, 1), TileType.Platform);
        return map;
    }

    /// <summary>
    /// Meldet, wenn der Spieler in einer massiven Kachel landet. Das stürzt nicht ab, sondern
    /// klemmt ihn lautlos fest – genau der Fehler, der den Tempel einmal unspielbar gemacht hat.
    /// Lieber eine Zeile im Log als eine unbewegliche Figur in der Ecke.
    /// </summary>
    private void WarnIfSpawnBlocked() => WarnIfBlocked(_player.Bounds, "Spieler");

    /// <summary>
    /// Prüft eine Kollisionsbox gegen die Kachelkarte und meldet den ersten Treffer.
    /// Gilt für JEDE Figur im Tempel – Pilger und Eremit steckten eine Zeit lang unbemerkt im
    /// Podest, weil diese Prüfung nur für den Spieler lief.
    /// </summary>
    private void WarnIfBlocked(Rectangle box, string who)
    {
        for (int tileY = TileMap.ToTile(box.Top); tileY <= TileMap.ToTile(box.Bottom - 1); tileY++)
        for (int tileX = TileMap.ToTile(box.Left); tileX <= TileMap.ToTile(box.Right - 1); tileX++)
        {
            if (!TileMap.IsBlocking(_map[tileX, tileY])) continue;
            Log.Warn($"Tempel: {who} steckt in einer Wand – Kachel ({tileX}, {tileY}), Position {box.Location}.");
            return;
        }
    }

    private void CreateNpcs()
    {
        // tileY ist die Zeile, die der KÖRPER einnimmt – gestanden wird auf der Zeile darunter.
        void Add(string id, int tileX, int tileY, string tag)
        {
            if (!Context.Definitions.Npcs.Contains(id)) return;
            NpcDefinition definition = Context.Definitions.Npcs.Get(id);
            var bottom = new Vector2(tileX * TileMap.TileSize + TileMap.TileSize / 2f, tileY * TileMap.TileSize + TileMap.TileSize);
            _npcs.Add(new Npc(definition, Context.Assets.GetSpriteSheet(definition.SpriteSheet), bottom, tag));
        }
        // Tempelwärtin in der Mitte beim Schrein
        Add("temple_keeper", HubWidthTiles / 2, HubHeightTiles - 2, "keeper");
        // Pilger und Eremit stehen auf dem FREIEN Tempelboden zwischen den Podesten.
        // Die Podeste selbst füllen x 2..6 und x 23..27 massiv aus (siehe BuildHubMap) – dort
        // standen beide vorher IM Mauerwerk. Belegt sind auf dem Boden außerdem Truhe (10),
        // Spielerstart (13), Tempelwärtin (15) und Höllentor (21).
        Add("pilgrim", 8, HubHeightTiles - 2, "blessed");
        Add("hermit", 19, HubHeightTiles - 2, "blessed");
    }

    /// <summary>Begleitseelen im Hub: schweben um den Spieler (dieselbe Companion-Klasse wie im Dungeon).</summary>
    private void CreateCompanions()
    {
        int slot = 0;
        foreach (string companionId in Context.Progression.Meta.UnlockedCompanions)
        {
            if (!Context.Definitions.Companions.Contains(companionId)) continue;
            CompanionDefinition definition = Context.Definitions.Companions.Get(companionId);
            _companions.Add(new Companion(definition, Context.Assets.GetSpriteSheet(definition.SpriteSheet),
                Context.Behaviors.CreateCompanion(definition.Behavior), slot++, _player.Center));
        }
    }

    private void RebuildDecoCatalog()
    {
        _decoCatalog.Clear();
        foreach (string propId in DecoPropIds)
            if (Context.Definitions.Props.Contains(propId))
                _decoCatalog.Add(Context.Definitions.Props.Get(propId));
    }

    private void LoadDeco()
    {
        foreach (HubDecoPlacement placement in Context.HubDeco)
        {
            PropDefinition? definition = Context.Definitions.Props.Contains(placement.PropId)
                ? Context.Definitions.Props.Get(placement.PropId)
                : null;
            if (definition is null) continue;
            _deco.Add(CreateDecoProp(definition, placement.TileX, placement.TileY));
        }
    }

    private Prop CreateDecoProp(PropDefinition definition, int tileX, int tileY)
    {
        var bottom = new Vector2(tileX * TileMap.TileSize + TileMap.TileSize / 2f, (tileY + 1) * TileMap.TileSize);
        return new Prop(definition, Context.Assets.GetSpriteSheet(definition.SpriteSheet),
            Context.Behaviors.CreateProp(definition.Behavior), bottom, null!, "hub_deco", 0)
        {
            CanInteract = false,
        };
    }

    // ------------------------------------------------------------------ Interaktion
    private enum Hotspot { None, MissionBoard, Shrine, Gate, Inventory, Keeper, Companion, Deco }
    private Hotspot _hotspot = Hotspot.None;
    private Npc? _hotspotNpc;
    private Companion? _hotspotCompanion;

    public override void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        InputState input = Context.Input;

        // Ankündigungs-Timer (auch im Deko-Modus weiterlaufen lassen)
        if (_announcement is { } notice)
        {
            float remaining = notice.Timer - deltaSeconds;
            _announcement = remaining > 0f ? (notice.Text, remaining) : null;
        }

        if (Context.Input.WasPressed(GameAction.Pause))
        {
            Context.Scenes.Push(new HubPauseScene(Context));
            return;
        }

        if (_decoMode)
        {
            UpdateDecoMode(input, deltaSeconds);
            return;
        }

        _player.UpdateHub(_map, input, deltaSeconds);
        foreach (Npc npc in _npcs) npc.UpdateHub(deltaSeconds);
        foreach (Companion companion in _companions) companion.UpdateHub(_player, deltaSeconds);
        foreach (Prop prop in _deco) prop.Update(null!, deltaSeconds);
        _effects.Update(deltaSeconds);
        _camera.Follow(_player.Center, _map.PixelBounds, deltaSeconds);

        UpdateHotspots();
        if (input.WasPressed(GameAction.Interact)) TriggerHotspot();
        if (input.WasPressed(GameAction.Randomize)) ToggleDecoMode();
    }

    private void UpdateHotspots()
    {
        _hotspot = Hotspot.None;
        _hotspotNpc = null;
        _hotspotCompanion = null;

        // Nächstgelegener Kandidat IN REICHWEITE gewinnt, nicht der letzte Treffer der Schleife.
        // Sonst verdeckt ein NPC (der direkt am Missionsbrett steht) dauerhaft die
        // Brett-Interaktion – oder ein ferner Hotspot zeigt überall im Tempel seinen Prompt an.
        const float hotspotRange = 28f;
        float bestDistanceSquared = hotspotRange * hotspotRange;

        void Consider(Vector2 spot, Hotspot candidate)
        {
            float distanceSquared = Vector2.DistanceSquared(_player.Center, spot);
            if (distanceSquared >= bestDistanceSquared) return;
            bestDistanceSquared = distanceSquared;
            _hotspot = candidate;
            _hotspotNpc = null;
            _hotspotCompanion = null;
        }

        // Missionsbrett: links auf dem Podest (feste Position)
        Consider(BoardSpot, Hotspot.MissionBoard);

        // Schrein: rechts auf dem Podest
        Consider(ShrineSpot, Hotspot.Shrine);

        // Truhe und Höllentor stehen auf dem Tempelboden zwischen den Podesten.
        // Beide brauchen einen laufenden Abstieg – ohne Lauf gibt es weder Ausrüstung noch ein Ziel.
        if (Context.Progression.CurrentRun is not null)
        {
            Consider(ChestSpot, Hotspot.Inventory);
            Consider(GateSpot, Hotspot.Gate);
        }

        foreach (Npc npc in _npcs)
        {
            var npcSpot = new Vector2(npc.Center.X, npc.Center.Y);
            float distanceSquared = Vector2.DistanceSquared(_player.Center, npcSpot);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                _hotspot = Hotspot.Keeper;
                _hotspotNpc = npc;
                _hotspotCompanion = null;
            }
        }
        foreach (Companion companion in _companions)
        {
            float distanceSquared = Vector2.DistanceSquared(companion.Center, _player.Center);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                _hotspot = Hotspot.Companion;
                _hotspotCompanion = companion;
                _hotspotNpc = null;
            }
        }
    }

    private void TriggerHotspot()
    {
        switch (_hotspot)
        {
            case Hotspot.MissionBoard:
                Context.Scenes.Push(new MissionBoardScene(Context));
                break;
            case Hotspot.Shrine:
                Context.Scenes.Push(new ShrineScene(Context));
                break;
            case Hotspot.Inventory when Context.Progression.CurrentRun is { } inventoryRun:
                Context.Scenes.Push(new InventoryScene(Context, inventoryRun, player: null));
                break;
            case Hotspot.Gate when Context.Progression.CurrentRun is not null:
                // Das Tor führt zur Kreisübersicht; von dort geht es hinab oder zurück in den Tempel.
                Context.Scenes.Push(new CircleIntroScene(Context));
                break;
            case Hotspot.Keeper when _hotspotNpc is not null:
                // Jeder NPC führt seinen EIGENEN Dialog – zuvor bekam jeder die festen Zeilen
                // der Tempelwärtin, egal ob Pilger oder Eremit.
                OpenNpcDialog(_hotspotNpc);
                break;
            case Hotspot.Companion when _hotspotCompanion is not null:
                OpenCompanionDialog(_hotspotCompanion);
                break;
        }
    }

    private void OpenNpcDialog(Npc npc)
    {
        string dialogId = npc.Definition.DialogId;
        if (!Context.Definitions.Dialogs.Contains(dialogId)) return;
        DialogDefinition dialog = Context.Definitions.Dialogs.Get(dialogId);
        DialogLineDefinition? entry = Context.Dialogs.ResolveEntry(dialog, npc);
        if (entry is not null) Context.Scenes.Push(new DialogScene(Context, dialog, entry, npc));
    }

    /// <summary>Haustier-Dialog einer Begleitseele: nutzt den pet_talk-Dialog mit dynamischem Namen.</summary>
    private void OpenCompanionDialog(Companion companion)
    {
        if (!Context.Definitions.Dialogs.Contains("pet_talk")) return;
        DialogDefinition dialog = Context.Definitions.Dialogs.Get("pet_talk");
        // Kleiner Trick: Wir bauen die "NPC"-Sicht über die Companion-Id als Tag-Quelle.
        var petNpc = new Npc(new NpcDefinition
        {
            Id = companion.Definition.Id,
            Name = PetService.GetPet(Context, companion.Definition.Id)?.Name ?? companion.Definition.Name,
            SpriteSheet = companion.Definition.SpriteSheet,
            DialogId = "pet_talk",
        }, Context.Assets.GetSpriteSheet(companion.Definition.SpriteSheet), companion.Center, "pet")
        {
            Tag = companion.Definition.Id,   // DialogService nutzt die Companion-Id für PetService
        };
        DialogLineDefinition? entry = Context.Dialogs.ResolveEntry(dialog, petNpc);
        if (entry is not null) Context.Scenes.Push(new DialogScene(Context, dialog, entry, petNpc));
    }

    // ------------------------------------------------------------------ Deko-Modus
    private void ToggleDecoMode()
    {
        _decoMode = !_decoMode;
        _decoCursorX = HubWidthTiles / 2;
        _decoCursorY = HubHeightTiles - 2;
        if (_decoMode)
        {
            Context.Audio.Play("lever", 0.4f);
            Announce("Deko-Modus: Bewegen mit Richtungstasten, Platzieren mit Interagieren.");
        }
        else Context.Audio.Play("lever", 0.4f, 0.5f);
    }

    private void UpdateDecoMode(InputState input, float deltaSeconds)
    {
        if (input.WasPressed(GameAction.Randomize) || input.WasPressed(GameAction.Cancel))
        {
            _decoMode = false;
            return;
        }
        // Cursor bewegen (TileMap-Raster)
        if (input.WasPressed(GameAction.Left)) _decoCursorX = Math.Max(1, _decoCursorX - 1);
        if (input.WasPressed(GameAction.Right)) _decoCursorX = Math.Min(HubWidthTiles - 2, _decoCursorX + 1);
        if (input.WasPressed(GameAction.Up)) _decoCursorY = Math.Max(1, _decoCursorY - 1);
        if (input.WasPressed(GameAction.Down)) _decoCursorY = Math.Min(HubHeightTiles - 2, _decoCursorY + 1);
        // Katalog durchschalten
        if (input.WasPressed(GameAction.AbilityOne))
            _decoSelectionIndex = (_decoSelectionIndex - 1 + _decoCatalog.Count) % _decoCatalog.Count;
        if (input.WasPressed(GameAction.AbilityTwo))
            _decoSelectionIndex = (_decoSelectionIndex + 1) % _decoCatalog.Count;

        if (input.WasPressed(GameAction.Interact)) TryPlaceDeco();
        if (input.WasPressed(GameAction.Dash)) TryRemoveDecoAtCursor();
        if (input.WasPressed(GameAction.Confirm)) ToggleDecoMode();
    }

    private void TryPlaceDeco()
    {
        if (_decoCatalog.Count == 0) return;
        PropDefinition definition = _decoCatalog[_decoSelectionIndex];
        long believers = Context.Progression.Meta.Believers;
        if (believers < DecoCost)
        {
            Announce($"Nicht genug Gläubige ({DecoCost} nötig).");
            Context.Audio.Play("error", 0.5f);
            return;
        }
        // Bereits belegt?
        if (Context.HubDeco.Any(placement => placement.TileX == _decoCursorX && placement.TileY == _decoCursorY))
        {
            Announce("Hier steht schon etwas.");
            Context.Audio.Play("error", 0.5f);
            return;
        }
        Context.Progression.Meta.Believers -= DecoCost;
        Context.HubDeco.Add(new HubDecoPlacement(definition.Id, _decoCursorX, _decoCursorY));
        _deco.Add(CreateDecoProp(definition, _decoCursorX, _decoCursorY));
        Context.SaveHubDeco();
        Context.Progression.SaveMeta();
        Context.Audio.Play("chest", 0.4f, 0.2f);
        Announce($"{definition.Name} platziert (-{DecoCost} Gläubige).");
    }

    private void TryRemoveDecoAtCursor()
    {
        HubDecoPlacement? placement = Context.HubDeco
            .FirstOrDefault(candidate => candidate.TileX == _decoCursorX && candidate.TileY == _decoCursorY);
        if (placement is null)
        {
            Announce("Hier ist nichts zu entfernen.");
            return;
        }
        Context.HubDeco.Remove(placement);
        Prop? prop = _deco.FirstOrDefault(candidate =>
            (int)(candidate.Center.X / TileMap.TileSize) == _decoCursorX &&
            (int)(candidate.BottomCenter.Y / TileMap.TileSize) - 1 == _decoCursorY);
        if (prop is not null) _deco.Remove(prop);
        Context.Progression.Meta.Believers += DecoCost / 2;   // 50% Erstattung
        Context.SaveHubDeco();
        Context.Progression.SaveMeta();
        Context.Audio.Play("crumble", 0.4f);
        Announce($"Entfernt (+{DecoCost / 2} Gläubige zurück).");
    }

    private void Announce(string text) => _announcement = (text, 2.6f);
    private (string Text, float Timer)? _announcement;

    // ------------------------------------------------------------------ Zeichnen
    public override void PrepareDraw(SpriteBatch spriteBatch)
    {
        _lighting.Clear();
        _lighting.Add(_player.Center, 64f, new Color(255, 235, 190));
        foreach (Npc npc in _npcs) _lighting.Add(npc.Center, npc.LightRadius, npc.LightColor);
        foreach (Prop prop in _deco) if (prop.LightRadius > 0f) _lighting.Add(prop.Center, prop.LightRadius, prop.LightColor);
        // Feste Tempel-Beleuchtung (göttlicher Altar)
        _lighting.Add(new Vector2(HubWidthTiles * TileMap.TileSize / 2f, (HubHeightTiles - 3) * TileMap.TileSize), 90f, new Color(255, 220, 160) * 0.8f);
        _lighting.Render(Context.GraphicsDevice, spriteBatch, _camera.Transform);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D tileset = Context.Assets.GetTexture("tiles.limbo");
        Texture2D pixel = Context.Assets.Pixel;

        spriteBatch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(Context.Assets.GetTexture("background.limbo"), Vector2.Zero, new Color(230, 220, 210));
        spriteBatch.End();

        Matrix transform = _camera.Transform;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, transformMatrix: transform);
        _map.Draw(spriteBatch, tileset, _camera.VisibleArea, Color.White, 0f, _time);

        // Missionsbrett + Schrein als eigene Zeichenobjekt (vereinfachte Darstellung)
        DrawBoard(spriteBatch, pixel);
        DrawShrine(spriteBatch, pixel);
        DrawGate(spriteBatch, pixel);
        DrawChest(spriteBatch, pixel);
        foreach (Prop prop in _deco) prop.Draw(spriteBatch);
        foreach (Npc npc in _npcs) npc.Draw(spriteBatch);
        foreach (Companion companion in _companions) companion.Draw(spriteBatch);
        _player.Draw(spriteBatch);
        _effects.Draw(spriteBatch, pixel, Context.Font);
        spriteBatch.End();

        _lighting.Composite(spriteBatch);

        // UI-Ebene (unbeleuchtet)
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, transformMatrix: transform);
        DrawHotspotPrompt(spriteBatch);
        if (_decoMode) DrawDecoOverlay(spriteBatch, pixel);
        spriteBatch.End();

        // Ankündigungen im Bildschirmraum
        spriteBatch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);
        if (_announcement is { } notice)
        {
            Context.Font.DrawCentered(spriteBatch, notice.Text, CirclesGame.VirtualWidth / 2f, 10, Palette.Faith);
        }
        Context.Font.DrawCentered(spriteBatch, "Tempel der Gläubigen", CirclesGame.VirtualWidth / 2f, CirclesGame.VirtualHeight - 12, Palette.Ash);
        spriteBatch.End();
    }

    private void DrawBoard(SpriteBatch spriteBatch, Texture2D pixel)
    {
        float x = 4 * TileMap.TileSize + 2, y = (HubHeightTiles - 6) * TileMap.TileSize;
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, 12, 16), new Color(80, 55, 40));
        spriteBatch.Draw(pixel, new Rectangle((int)x + 2, (int)y + 3, 8, 10), new Color(222, 214, 192));
        spriteBatch.Draw(pixel, new Rectangle((int)x + 3, (int)y + 4, 2, 3), new Color(150, 24, 36));
        spriteBatch.Draw(pixel, new Rectangle((int)x + 6, (int)y + 5, 2, 2), new Color(150, 24, 36));
        spriteBatch.Draw(pixel, new Rectangle((int)x + 4, (int)y + 8, 3, 2), new Color(150, 24, 36));
        LabelStation(spriteBatch, "Bitten", new Vector2(x + 6, y + 18));
    }

    private void DrawShrine(SpriteBatch spriteBatch, Texture2D pixel)
    {
        float x = (HubWidthTiles - 4) * TileMap.TileSize + 2, y = (HubHeightTiles - 6) * TileMap.TileSize;
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, 12, 16), new Color(120, 116, 130));
        spriteBatch.Draw(pixel, new Rectangle((int)x + 3, (int)y + 2, 6, 4), new Color(212, 170, 72));
        // Gesammelte Reliquien als kleine Punkte (max 4 sichtbar)
        int shown = Math.Min(4, Context.Collectibles.Values.Sum());
        for (int index = 0; index < shown; index++)
            spriteBatch.Draw(pixel, new Rectangle((int)x + 2 + index * 3, (int)y + 8, 2, 2), new Color(240, 220, 140));
        LabelStation(spriteBatch, "Schrein", new Vector2(x + 6, y + 18));
    }

    /// <summary>Kleine Stations-Beschriftung (Brett/Schrein), damit man die Hotspots wiederfindet.</summary>
    private void LabelStation(SpriteBatch spriteBatch, string text, Vector2 bottomCenter)
    {
        int width = Context.Font.MeasureWidth(text);
        Context.Font.DrawShadowed(spriteBatch, text, bottomCenter - new Vector2(width / 2f, 0), Palette.Bone * 0.75f);
    }

    /// <summary>Das Höllentor: ein Torbogen mit glimmendem Schlund, der langsam pulsiert.</summary>
    private void DrawGate(SpriteBatch spriteBatch, Texture2D pixel)
    {
        int left = (int)GateSpot.X - 12, bottom = (int)GateSpot.Y + 8;
        var frame = new Rectangle(left, bottom - 34, 24, 34);
        UiDraw.Rect(spriteBatch, pixel, frame, new Color(58, 46, 54));
        UiDraw.Border(spriteBatch, pixel, frame, Palette.Gold * 0.65f);
        // Der Schlund atmet: Helligkeit schwingt mit der Zeit.
        float pulse = 0.55f + 0.45f * MathF.Sin(_time * 1.7f);
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(left + 4, bottom - 28, 16, 26), Palette.Blood * (0.35f + 0.35f * pulse));
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(left + 8, bottom - 20, 8, 18), new Color(255, 160, 90) * (0.3f + 0.4f * pulse));
    }

    /// <summary>Ausrüstungstruhe: schlichte Kiste mit Goldbeschlag.</summary>
    private void DrawChest(SpriteBatch spriteBatch, Texture2D pixel)
    {
        int left = (int)ChestSpot.X - 7, bottom = (int)ChestSpot.Y + 8;
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(left, bottom - 11, 14, 11), new Color(96, 68, 48));
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(left, bottom - 11, 14, 3), new Color(132, 96, 64));
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(left + 6, bottom - 8, 2, 4), Palette.Gold);
    }

    private void DrawHotspotPrompt(SpriteBatch spriteBatch)
    {
        string use = Context.Input.Prompt(GameAction.Interact);
        string? prompt = _hotspot switch
        {
            Hotspot.MissionBoard => $"{use} Bitten der Gläubigen",
            Hotspot.Shrine => $"{use} Schrein der Reliquien",
            Hotspot.Inventory => $"{use} Ausrüstung",
            Hotspot.Gate => $"{use} Höllentor – hinabsteigen",
            Hotspot.Keeper when _hotspotNpc is not null =>
                $"{use} Mit {_hotspotNpc.Definition.Name} sprechen",
            Hotspot.Companion when _hotspotCompanion is not null =>
                $"{use} {PetService.GetPet(Context, _hotspotCompanion.Definition.Id)?.Name ?? _hotspotCompanion.Definition.Name} streicheln/füttern",
            _ => null,
        };
        if (prompt is null) return;
        int width = Context.Font.MeasureWidth(prompt);
        Context.Font.DrawShadowed(spriteBatch, prompt, new Vector2(_player.Center.X - width / 2f, _player.Position.Y - 14), Palette.Faith);
    }

    private void DrawDecoOverlay(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Raster-Cursor
        var cursor = new Rectangle(_decoCursorX * TileMap.TileSize, _decoCursorY * TileMap.TileSize, TileMap.TileSize, TileMap.TileSize);
        UiDraw.Border(spriteBatch, pixel, cursor, Palette.Gold);

        // Katalog-Vorschau unten links (Bildschirmraum wird im nächsten Begin gezeichnet – hier im Welt-Raum)
        if (_decoCatalog.Count > 0)
        {
            PropDefinition selected = _decoCatalog[_decoSelectionIndex];
            Context.Font.DrawShadowed(spriteBatch, $"Deko: {selected.Name} ({_decoSelectionIndex + 1}/{_decoCatalog.Count})",
                new Vector2(8, 8), Palette.Bone);
            InputState input = Context.Input;
            Context.Font.DrawShadowed(spriteBatch,
                $"Richtung: bewegen · {input.Glyph(GameAction.AbilityOne)}/{input.Glyph(GameAction.AbilityTwo)}: wechseln · "
                + $"{input.Glyph(GameAction.Interact)}: platzieren · {input.Glyph(GameAction.Dash)}: entfernen",
                new Vector2(8, 22), Palette.Ash);
            Context.Font.DrawShadowed(spriteBatch,
                $"Kosten: {DecoCost} Gläubige · {input.Glyph(GameAction.Randomize)}: Deko-Modus verlassen",
                new Vector2(8, 36), Palette.Ash);
        }
    }

    public override void OnExit()
    {
        _lighting.Dispose();
    }
}