using System.Net.Sockets;
using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Networking;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;
using CirclesOfAsh.World;
using Microsoft.Xna.Framework.Input;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Vorraum der Arena: Modus (allein, zu zweit an einem Rechner, online eröffnen oder beitreten),
/// Gegner, Gestalt(en), Geräte und Adresse. Beim Online-Spiel wartet die Lobby auch auf den
/// Mitspieler bzw. verbindet sich – ohne das Spiel anzuhalten (alles wird pro Frame abgefragt).
/// </summary>
public sealed class ArenaLobbyScene : SceneBase
{
    /// <summary>Die Zeilen in Anzeigereihenfolge. Welche davon sichtbar sind, hängt vom Modus ab.</summary>
    private enum Row { Mode, Opponent, FirstFighter, SecondFighter, Devices, Address, Start }

    private const int PanelHeight = 208;
    private const int RowStep = 18;
    private const int MaxAddressLength = 45;

    private readonly List<ArenaOpponent> _opponents;
    private readonly IReadOnlyList<SavedCharacter> _characters;
    private ArenaMode _mode;
    private int _opponentIndex, _firstIndex, _secondIndex;
    private bool _swapDevices;
    private string _address;
    private Row[] _rows = Array.Empty<Row>();
    private int _rowIndex;
    private float _time;

    // Online: genau eine davon ist gesetzt, solange gewartet bzw. verbunden wird.
    private ArenaHostSession? _host;
    private ArenaClientSession? _client;
    private ArenaFighter? _ownFighter;
    private int _hostSeed;
    /// <summary>Hinweiszeilen unter den Einstellungen (Warten, Adresse, Fehler).</summary>
    private readonly List<(string Text, Color Color)> _status = new();

    // Vorschau: nur neu bauen, wenn sich die Auswahl ändert.
    private (int First, int Second, int Opponent) _previewKey = (-1, -1, -1);
    private LayeredSprite? _firstPreview, _secondPreview;
    private AnimationPlayer? _opponentPreview;

    public ArenaLobbyScene(GameContext context) : this(context, ArenaLobbyChoice.Default) { }

    public ArenaLobbyScene(GameContext context, ArenaLobbyChoice choice) : base(context)
    {
        _opponents = context.Arena.UnlockedOpponents.ToList();
        _characters = context.Progression.Characters;
        _mode = choice.Mode;
        // FindIndex liefert -1, wenn nichts passt -> dann der tiefste freigeschaltete Gegner.
        int opponent = _opponents.FindIndex(candidate => candidate.Id == choice.OpponentId);
        _opponentIndex = opponent >= 0 ? opponent : Math.Max(0, _opponents.Count - 1);
        int activeIndex = IndexOfCharacter(context.Progression.ActiveCharacter?.Id ?? 0, 0);
        _firstIndex = IndexOfCharacter(choice.FirstCharacterId, activeIndex);
        _secondIndex = IndexOfCharacter(choice.SecondCharacterId, (_firstIndex + 1) % Math.Max(1, _characters.Count));
        _swapDevices = choice.SwapDevices;
        _address = choice.Address.Length > 0 ? choice.Address : context.Settings.ArenaAddress;
        RebuildRows();
    }

    private int IndexOfCharacter(int characterId, int fallback)
    {
        for (int index = 0; index < _characters.Count; index++)
            if (_characters[index].Id == characterId) return index;
        return Math.Clamp(fallback, 0, Math.Max(0, _characters.Count - 1));
    }

    private Row CurrentRow => _rows[_rowIndex];
    private ArenaOpponent? SelectedOpponent => _opponents.Count == 0 ? null : _opponents[_opponentIndex];
    private SavedCharacter FirstCharacter => _characters[_firstIndex];
    private SavedCharacter SecondCharacter => _characters[_secondIndex];
    private bool IsBusy => _host is not null || _client is not null;

    /// <summary>Was die Lobby beim nächsten Mal wieder so einstellen soll.</summary>
    private ArenaLobbyChoice CurrentChoice => new(_mode, SelectedOpponent?.Id ?? "", FirstCharacter.Id, SecondCharacter.Id,
                                                  _swapDevices, _address.Trim());

    private void RebuildRows()
    {
        var rows = new List<Row> { Row.Mode };
        if (_mode != ArenaMode.OnlineJoin) rows.Add(Row.Opponent);   // beim Beitreten wählt der Gastgeber
        rows.Add(Row.FirstFighter);
        if (_mode == ArenaMode.LocalDuo)
        {
            rows.Add(Row.SecondFighter);
            rows.Add(Row.Devices);
        }
        if (_mode == ArenaMode.OnlineJoin) rows.Add(Row.Address);
        rows.Add(Row.Start);
        Row current = _rows.Length > 0 ? CurrentRow : Row.Mode;
        _rows = rows.ToArray();
        _rowIndex = Math.Max(0, Array.IndexOf(_rows, current));
    }

    public override void OnEnter() => Context.Music.Play("music.title");

    public override void OnExit()
    {
        // Beim Wechsel in den Kampf sind die Felder schon auf null gesetzt (die Szene übernimmt sie).
        _host?.Dispose();
        _client?.Dispose();
    }

    // ------------------------------------------------------------------ Eingabe
    public override void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        UpdatePreviews(deltaSeconds);
        if (_host is not null)
        {
            UpdateHosting();
            return;
        }
        if (_client is not null)
        {
            UpdateJoining();
            return;
        }

        InputState input = Context.Input;
        if (input.WasPressed(GameAction.Cancel))
        {
            Context.Scenes.Replace(new TitleScene(Context));
            return;
        }
        if (CurrentRow == Row.Address)
        {
            UpdateAddressRow(input);
            return;
        }
        if (input.WasPressed(GameAction.Down)) MoveRow(1);
        else if (input.WasPressed(GameAction.Up)) MoveRow(-1);
        int change = input.WasPressed(GameAction.Right) ? 1 : input.WasPressed(GameAction.Left) ? -1 : 0;
        if (change != 0) ChangeValue(change);
        if (!input.WasPressed(GameAction.Confirm)) return;
        if (CurrentRow == Row.Start) Start();
        else MoveRow(1);
    }

    /// <summary>
    /// Im Adressfeld sind W/A/S/D Buchstaben – navigiert wird deshalb nur mit Pfeiltasten und Enter
    /// (rohe Tastenabfrage statt GameAction, wie beim Namen im Charakter-Editor).
    /// </summary>
    private void UpdateAddressRow(InputState input)
    {
        foreach (char character in input.TypedText)
        {
            if (character == '\b')
            {
                if (_address.Length > 0) _address = _address[..^1];   // "^1" = ein Zeichen vom Ende
            }
            else if ((char.IsLetterOrDigit(character) || character is '.' or ':' or '-' or '[' or ']')
                     && _address.Length < MaxAddressLength)
            {
                _address += character;
            }
        }
        if (input.WasKeyPressed(Keys.Down) || input.WasKeyPressed(Keys.Tab) || input.WasPressed(GameAction.Confirm)) MoveRow(1);
        else if (input.WasKeyPressed(Keys.Up)) MoveRow(-1);
    }

    private void MoveRow(int step)
    {
        _rowIndex = CharacterVisuals.Wrap(_rowIndex + step, _rows.Length);
        Context.Audio.Play("pickup", 0.2f, 0.4f);
    }

    private void ChangeValue(int step)
    {
        switch (CurrentRow)
        {
            case Row.Mode:
                // Enum.GetValues<T>().Length = Anzahl der Modi; Wrap klappt am Ende wieder auf den ersten.
                _mode = (ArenaMode)CharacterVisuals.Wrap((int)_mode + step, Enum.GetValues<ArenaMode>().Length);
                _status.Clear();
                RebuildRows();
                break;
            case Row.Opponent:
                _opponentIndex = CharacterVisuals.Wrap(_opponentIndex + step, _opponents.Count);
                break;
            case Row.FirstFighter:
                _firstIndex = CharacterVisuals.Wrap(_firstIndex + step, _characters.Count);
                break;
            case Row.SecondFighter:
                _secondIndex = CharacterVisuals.Wrap(_secondIndex + step, _characters.Count);
                break;
            case Row.Devices:
                _swapDevices = !_swapDevices;   // "!" = Negation: tauscht, wer die Tastatur bekommt
                break;
            default:
                return;
        }
        Context.Audio.Play("pickup", 0.25f, 0.2f);
    }

    // ------------------------------------------------------------------ Start
    private void Start()
    {
        _status.Clear();
        if (SelectedOpponent is null && _mode != ArenaMode.OnlineJoin)
        {
            _status.Add((Loc.T("Kein Gegner verfügbar."), Palette.Blood));
            return;
        }
        switch (_mode)
        {
            case ArenaMode.Solo:
            case ArenaMode.LocalDuo:
                StartLocal();
                break;
            case ArenaMode.OnlineHost:
                StartHosting();
                break;
            case ArenaMode.OnlineJoin:
                StartJoining();
                break;
        }
    }

    private void StartLocal()
    {
        ArenaOpponent opponent = SelectedOpponent!;   // "!": Start() hat geprüft, dass es einen gibt
        ArenaService arena = Context.Arena;
        ArenaMatchSetup setup;
        if (_mode == ArenaMode.Solo)
        {
            setup = new ArenaMatchSetup
            {
                Opponent = opponent,
                Fighters = new[] { arena.CreateFighter(FirstCharacter) },
                Inputs = new IPlayerInput?[] { null },   // null = gemeinsame Eingabe wie im Verlies
                IsLocal = new[] { true },
                Choice = CurrentChoice,
            };
        }
        else
        {
            var (first, second) = AssignDevices();
            if (second.Count == 0)
            {
                _status.Add((Loc.T("Zu zweit braucht Spieler 2 einen Controller."), Palette.Blood));
                return;
            }
            setup = new ArenaMatchSetup
            {
                Opponent = opponent,
                Fighters = new[] { arena.CreateFighter(FirstCharacter), arena.CreateFighter(SecondCharacter) },
                Inputs = new IPlayerInput?[] { new DeviceInput(first), new DeviceInput(second) },
                IsLocal = new[] { true, true },
                Choice = CurrentChoice,
            };
        }
        Context.Scenes.Replace(new ArenaScene(Context, setup));
    }

    /// <summary>
    /// Zu zweit: Spieler 2 bekommt den zuletzt angeschlossenen Controller, Spieler 1 alles andere
    /// (Tastatur und übrige Controller). "Geräte" tauscht die beiden Seiten.
    /// </summary>
    private (IReadOnlyList<InputDeviceId> First, IReadOnlyList<InputDeviceId> Second) AssignDevices()
    {
        List<InputDeviceId> devices = DeviceInput.ConnectedDevices().ToList();
        // Cast auf "InputDeviceId?": So liefert LastOrDefault null statt der Tastatur, wenn kein Controller da ist.
        InputDeviceId? pad = devices.Where(device => device != InputDeviceId.Keyboard).Cast<InputDeviceId?>().LastOrDefault();
        if (pad is null) return (devices, Array.Empty<InputDeviceId>());
        IReadOnlyList<InputDeviceId> padOnly = new[] { pad.Value };
        IReadOnlyList<InputDeviceId> rest = devices.Where(device => device != pad.Value).ToList();
        return _swapDevices ? (padOnly, rest) : (rest, padOnly);
    }

    private static string DeviceName(InputDeviceId device) => device == InputDeviceId.Keyboard
        ? Loc.T("Tastatur")
        : Loc.T("Controller {0}", device - InputDeviceId.Pad1 + 1);

    // ------------------------------------------------------------------ Online eröffnen
    private void StartHosting()
    {
        ArenaOpponent opponent = SelectedOpponent!;
        _ownFighter = Context.Arena.CreateFighter(FirstCharacter);
        _hostSeed = Random.Shared.Next();
        // Die Prüfsumme der Karte reist im Handschlag mit: Der Gast erzeugt aus demselben Seed
        // dasselbe Verlies – oder merkt sofort, dass bei ihm etwas anders ist (Version, Mods).
        DungeonPlan plan = Context.Arena.CreatePlan(opponent, 2, _hostSeed);
        uint checksum = new DungeonGenerator(Context.Definitions).Generate(plan).Map.Checksum();
        try
        {
            _host = new ArenaHostSession(new ArenaWelcome(_ownFighter, opponent.Id, _hostSeed, checksum));
        }
        catch (SocketException exception)
        {
            Log.Warn($"Arena: Port {ArenaProtocol.DefaultPort} nicht verfügbar: {exception.Message}");
            _status.Add((Loc.T("Port {0} ist belegt – läuft das Spiel schon einmal?", ArenaProtocol.DefaultPort), Palette.Blood));
        }
    }

    private void UpdateHosting()
    {
        ArenaHostSession host = _host!;
        if (Context.Input.WasPressed(GameAction.Cancel))
        {
            host.Dispose();
            _host = null;
            _status.Clear();
            return;
        }
        host.PollLobby();
        _status.Clear();
        if (host.Current == ArenaHostSession.State.Ready && host.GuestFighter is { } guest)
        {
            var setup = new ArenaMatchSetup
            {
                Opponent = SelectedOpponent!,
                Fighters = new[] { _ownFighter!, guest },
                Inputs = new IPlayerInput?[] { null, host.GuestInput },
                IsLocal = new[] { true, false },
                Choice = CurrentChoice,
                Seed = _hostSeed,
                Host = host,
            };
            _host = null;   // die Kampfszene übernimmt die Verbindung (und schließt sie am Ende)
            Context.Scenes.Replace(new ArenaScene(Context, setup));
            return;
        }
        bool isShaking = host.Current == ArenaHostSession.State.Handshaking;
        _status.Add((isShaking ? Loc.T("Mitspieler verbindet sich …") : Loc.T("Warte auf Mitspieler …"), Palette.Faith));
        IReadOnlyList<string> addresses = ArenaHostSession.LocalAddresses();
        string shown = addresses.Count == 0 ? "?" : string.Join(" · ", addresses.Take(2));
        _status.Add((Loc.T("Adresse: {0} · Port {1}", shown, host.Port), Palette.Bone));
        if (host.RejectedMismatch)
            _status.Add((Loc.T("Ein Mitspieler mit anderer Spielversion wurde abgewiesen."), Palette.Ember));
        _status.Add((Loc.T("{0}: abbrechen", Context.Input.Glyph(GameAction.Cancel)), Palette.Ash));
    }

    // ------------------------------------------------------------------ Online beitreten
    private void StartJoining()
    {
        string address = _address.Trim();
        if (address.Length == 0)
        {
            _status.Add((Loc.T("Bitte die Adresse des Gastgebers eingeben."), Palette.Blood));
            return;
        }
        Context.Settings.ArenaAddress = address;   // fürs nächste Mal merken
        Context.SaveSettings();
        _ownFighter = Context.Arena.CreateFighter(FirstCharacter);
        _client = new ArenaClientSession(address, _ownFighter);
    }

    private void UpdateJoining()
    {
        ArenaClientSession client = _client!;
        if (Context.Input.WasPressed(GameAction.Cancel))
        {
            client.Dispose();
            _client = null;
            _status.Clear();
            return;
        }
        client.PollLobby();
        _status.Clear();
        switch (client.Current)
        {
            case ArenaClientSession.State.Connecting:
                _status.Add((Loc.T("Verbinde mit {0} …", client.Address), Palette.Faith));
                break;
            case ArenaClientSession.State.Handshaking:
                _status.Add((Loc.T("Verbunden – warte auf den Gastgeber …"), Palette.Faith));
                break;
            case ArenaClientSession.State.Failed:
                EndJoining(Loc.T("Keine Verbindung zu {0}. Läuft dort ein eröffnetes Spiel, ist der Port frei?", client.Address));
                return;
            case ArenaClientSession.State.Rejected:
                EndJoining(client.Rejection == RejectReason.VersionMismatch
                    ? Loc.T("Der Gastgeber hat eine andere Spielversion.")
                    : Loc.T("Der Gastgeber nimmt gerade niemanden an."));
                return;
            case ArenaClientSession.State.Welcomed:
                BeginClientMatch(client);
                return;
        }
        _status.Add((Loc.T("{0}: abbrechen", Context.Input.Glyph(GameAction.Cancel)), Palette.Ash));
    }

    private void EndJoining(string message)
    {
        _client?.Dispose();
        _client = null;
        _status.Clear();
        _status.Add((message, Palette.Blood));
    }

    /// <summary>
    /// Der Gastgeber hat sich vorgestellt: Verlies aus seinem Seed erzeugen, mit seiner Prüfsumme
    /// vergleichen – erst dann "bereit" melden und in den Kampf wechseln.
    /// </summary>
    private void BeginClientMatch(ArenaClientSession client)
    {
        ArenaWelcome welcome = client.Welcome!;
        if (Context.Arena.Find(welcome.OpponentId) is not { } opponent)
        {
            EndJoining(Loc.T("Der Gegner des Gastgebers ist hier unbekannt – andere Spieldaten?"));
            return;
        }
        DungeonPlan plan = Context.Arena.CreatePlan(opponent, 2, welcome.Seed);
        DungeonLayout layout = new DungeonGenerator(Context.Definitions).Generate(plan);
        if (layout.Map.Checksum() != welcome.MapChecksum)
        {
            EndJoining(Loc.T("Die Kampfplätze stimmen nicht überein – gleiche Spielversion und Mods?"));
            return;
        }
        client.SendReady();
        _client = null;   // die Kampfszene übernimmt die Verbindung
        Context.Scenes.Replace(new ArenaClientScene(Context, client, opponent, plan, layout, welcome.HostFighter, _ownFighter!,
                                                    CurrentChoice));
    }

    // ------------------------------------------------------------------ Vorschau
    private void UpdatePreviews(float deltaSeconds)
    {
        var key = (_firstIndex, _secondIndex, _opponentIndex);
        if (key != _previewKey)   // Tupel vergleichen sich Feld für Feld (Wertgleichheit)
        {
            _previewKey = key;
            _firstPreview = CreateFighterPreview(FirstCharacter);
            _secondPreview = CreateFighterPreview(SecondCharacter);
            _opponentPreview = SelectedOpponent is { } opponent
                ? new AnimationPlayer(Context.Assets.GetSpriteSheet(opponent.Enemy.SpriteSheet))
                : null;
        }
        _firstPreview?.Update(deltaSeconds);
        _secondPreview?.Update(deltaSeconds);
        _opponentPreview?.Update(deltaSeconds);
    }

    private LayeredSprite CreateFighterPreview(SavedCharacter character)
    {
        ClassDefinition playerClass = Context.Progression.DisplayClassOf(character);
        RunState? run = Context.Progression.PeekRun(character);
        string? armor = run is null
            ? EquipmentService.StartingArmorSprite(Context.Definitions, playerClass)
            : EquipmentService.ArmorSprite(Context.Definitions, run, Context.Assets);
        LayeredSprite sprite = CharacterVisuals.Create(Context, playerClass, character.Appearance, armor, run?.Underwear ?? 0);
        sprite.Play("idle");
        return sprite;
    }

    // ------------------------------------------------------------------ Darstellung
    public override void Draw(SpriteBatch spriteBatch)
    {
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.6f);
        BitmapFont font = Context.Font;
        Texture2D pixel = Context.Assets.Pixel;
        float centerX = CirclesGame.VirtualWidth / 2f;
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Arena"), centerX, 6, Palette.Gold);

        var settings = new Rectangle(16, 36, 270, PanelHeight);
        UiDraw.Panel(spriteBatch, pixel, settings);
        DrawRows(spriteBatch, font, settings);

        var preview = new Rectangle(settings.Right + 10, 36, CirclesGame.VirtualWidth - settings.Right - 26, PanelHeight);
        UiDraw.Panel(spriteBatch, pixel, preview);
        DrawPreview(spriteBatch, font, pixel, preview);

        string help = IsBusy
            ? Loc.T("{0}: abbrechen", Context.Input.Glyph(GameAction.Cancel))
            : Loc.T("Hoch/Runter Zeile · Links/Rechts ändern · {0} wählen · {1} zurück",
                    Context.Input.Glyph(GameAction.Confirm), Context.Input.Glyph(GameAction.Cancel));
        font.DrawCentered(spriteBatch, help, centerX, CirclesGame.VirtualHeight - 14, Palette.Ash);
        spriteBatch.End();
    }

    private void DrawRows(SpriteBatch spriteBatch, BitmapFont font, Rectangle panel)
    {
        float y = panel.Top + 10;
        foreach (Row row in _rows)
        {
            bool isSelected = row == CurrentRow && !IsBusy;
            Color color = IsBusy ? Palette.Ash * 0.7f : isSelected ? Palette.Gold : Palette.Bone * 0.85f;
            if (row == Row.Start)
            {
                string label = _mode switch
                {
                    ArenaMode.OnlineHost => Loc.T("Spiel eröffnen"),
                    ArenaMode.OnlineJoin => Loc.T("Verbinden"),
                    _ => Loc.T("Kampf beginnen"),
                };
                font.DrawCentered(spriteBatch, isSelected ? $"· {label} ·" : label, panel.Center.X, y + 4, color);
                y += RowStep + 6;
                continue;
            }

            font.DrawShadowed(spriteBatch, RowLabel(row), new Vector2(panel.Left + 10, y), color);
            (string value, Color valueColor) = RowValue(row, isSelected);
            font.DrawShadowed(spriteBatch, value, new Vector2(panel.Left + 92, y), IsBusy ? Palette.Ash : valueColor);
            y += RowStep;
        }

        // Hinweise unter den Zeilen: Warten, Adresse, Fehler – umbrochen auf die Panelbreite.
        foreach (var (text, color) in _status)
        {
            string wrapped = font.Wrap(text, panel.Width - 20);
            font.DrawShadowed(spriteBatch, wrapped, new Vector2(panel.Left + 10, y), color);
            y += font.LineHeight * (wrapped.Count(character => character == '\n') + 1) + 2;
        }
    }

    private string RowLabel(Row row) => row switch
    {
        Row.Mode => Loc.T("Modus"),
        Row.Opponent => Loc.T("Gegner"),
        Row.FirstFighter => _mode == ArenaMode.LocalDuo ? Loc.T("Spieler 1") : Loc.T("Gestalt"),
        Row.SecondFighter => Loc.T("Spieler 2"),
        Row.Devices => Loc.T("Geräte"),
        Row.Address => Loc.T("Adresse"),
        _ => "",
    };

    private (string Text, Color Color) RowValue(Row row, bool isSelected)
    {
        switch (row)
        {
            case Row.Mode:
                string mode = _mode switch
                {
                    ArenaMode.LocalDuo => Loc.T("Zu zweit (ein Rechner)"),
                    ArenaMode.OnlineHost => Loc.T("Online eröffnen"),
                    ArenaMode.OnlineJoin => Loc.T("Online beitreten"),
                    _ => Loc.T("Allein"),
                };
                return ($"‹ {mode} ›", Palette.Faith);
            case Row.Opponent:
                return SelectedOpponent is { } opponent ? ($"‹ {opponent.Enemy.Name} ›", Palette.Faith) : ("–", Palette.Ash);
            case Row.FirstFighter:
                return ($"‹ {FirstCharacter.Name} ›", Palette.Faith);
            case Row.SecondFighter:
                return ($"‹ {SecondCharacter.Name} ›", Palette.Faith);
            case Row.Devices:
                var (first, second) = AssignDevices();
                if (second.Count == 0) return (Loc.T("Kein Controller angeschlossen"), Palette.Blood);
                string firstNames = string.Join(" + ", first.Select(DeviceName));
                string secondNames = string.Join(" + ", second.Select(DeviceName));
                return ($"‹ {firstNames} | {secondNames} ›", Palette.Faith);
            case Row.Address:
                bool cursorVisible = isSelected && _time % 1f < 0.5f;   // blinkender Cursor
                string address = _address.Length == 0 && !isSelected ? Loc.T("(z. B. 192.168.0.12)") : _address;
                return (address + (cursorVisible ? "_" : ""), _address.Length == 0 ? Palette.Ash : Palette.Faith);
            default:
                return ("", Palette.Bone);
        }
    }

    private void DrawPreview(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Rectangle panel)
    {
        if (_mode == ArenaMode.OnlineJoin)
        {
            font.DrawCenteredLines(spriteBatch, font.Wrap(Loc.T("Gegner und Kampfplatz wählt der Gastgeber."), panel.Width - 20),
                                   panel.Center.X, panel.Top + 50, Palette.Bone * 0.85f);
        }
        else if (SelectedOpponent is { } opponent)
        {
            font.DrawCentered(spriteBatch, opponent.Enemy.Name, panel.Center.X, panel.Top + 6, Palette.Ember);
            string place = opponent.IsMiniBoss
                ? Loc.T("Kerkermeister · {0}. Kreis", opponent.CircleIndex + 1)
                : Loc.T("Thronsaal · {0}. Kreis", opponent.CircleIndex + 1);
            font.DrawCentered(spriteBatch, place, panel.Center.X, panel.Top + 18, Palette.Bone * 0.8f);
            font.DrawCentered(spriteBatch, Loc.T("{0} von {1} Gegnern freigeschaltet", _opponents.Count, Context.Arena.Opponents.Count),
                              panel.Center.X, panel.Top + 30, Palette.Ash);
            if (_opponentPreview is { } animation)
            {
                // Ganzzahlige Vergrößerung hält die Pixel scharf: kleine Gegner doppelt, große einfach.
                float scale = animation.Sheet.FrameHeight <= 45 ? 2f : 1f;
                var feet = new Vector2(panel.Center.X, panel.Top + 124);
                InfernoFunnel.DrawEllipse(spriteBatch, pixel, feet, 34f, 6f, Palette.Blood * 0.6f);
                Color tint = ColorUtil.FromHex(opponent.Enemy.Tint, Color.White);
                animation.Draw(spriteBatch, feet, flipHorizontally: true, tint, new Vector2(scale));
            }
        }

        int dividerY = panel.Top + 132;
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(panel.Left + 8, dividerY, panel.Width - 16, 1), Palette.Gold * 0.4f);
        var firstFeet = new Vector2(_mode == ArenaMode.LocalDuo ? panel.Left + 44 : panel.Center.X, panel.Bottom - 8);
        _firstPreview?.Draw(spriteBatch, firstFeet, false, Color.White, new Vector2(2f));
        if (_mode == ArenaMode.LocalDuo)
            _secondPreview?.Draw(spriteBatch, new Vector2(panel.Right - 44, panel.Bottom - 8), true, Color.White, new Vector2(2f));
    }
}
