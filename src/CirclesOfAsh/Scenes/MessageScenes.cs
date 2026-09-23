using CirclesOfAsh.Core;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>Allgemeiner Textbildschirm mit Titel, Text und "Weiter"-Aktion. Wiederverwendbar (DRY).</summary>
public sealed class MessageScene : SceneBase
{
    private readonly string _title;
    private readonly string _body;
    private readonly Color _titleColor;
    private readonly Action _onContinue;

    public MessageScene(GameContext context, string title, string body, Color titleColor, Action onContinue) : base(context)
    {
        _title = title;
        _body = body;
        _titleColor = titleColor;
        _onContinue = onContinue;
    }

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Confirm)) _onContinue();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.65f);
        Context.TitleFont.DrawCentered(spriteBatch, _title, centerX, 40, _titleColor);
        var panel = new Rectangle(60, 84, CirclesGame.VirtualWidth - 120, 130);
        UiDraw.Panel(spriteBatch, Context.Assets.Pixel, panel);
        Context.Font.DrawShadowed(spriteBatch, Context.Font.Wrap(_body, panel.Width - 20), new Vector2(panel.Left + 10, panel.Top + 10), Palette.Bone);
        Context.Font.DrawCentered(spriteBatch, "Enter: weiter", centerX, CirclesGame.VirtualHeight - 20, Palette.Ash);
        spriteBatch.End();
    }
}

/// <summary>Permadeath-Bildschirm: Was verloren ging, was bleibt.</summary>
public sealed class GameOverScene : SceneBase
{
    private readonly DeathReport _report;

    public GameOverScene(GameContext context, DeathReport report) : base(context) => _report = report;

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Confirm)) Context.Scenes.Replace(new TitleScene(Context));
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.75f);
        Context.TitleFont.DrawCentered(spriteBatch, "Du bist gefallen", centerX, 50, Palette.Blood);
        Context.Font.DrawCentered(spriteBatch, $"Die Gestalt des {_report.ClassName} ist vergessen. Dein Abstieg endete im {_report.ReachedCircle}. Kreis.", centerX, 100, Palette.Bone);
        Context.Font.DrawCentered(spriteBatch, $"Gläubige: {_report.BelieversBefore}  ›  {_report.BelieversAfter} bleiben dir treu.", centerX, 120, Palette.Faith);
        Context.Font.DrawCentered(spriteBatch, "Deine ewigen Gaben und Begleitseelen bleiben erhalten.", centerX, 140, Palette.Soul);
        Context.Font.DrawCentered(spriteBatch, "Enter: zurück zum Titel", centerX, CirclesGame.VirtualHeight - 20, Palette.Ash);
        spriteBatch.End();
    }
}

/// <summary>Entscheidet anhand des Dungeon-Ergebnisses, welcher Bildschirm als Nächstes kommt.</summary>
public static class ResultSceneFactory
{
    public static IScene Create(GameContext context, DungeonOutcome outcome)
    {
        var lines = new List<string> { $"+{outcome.BelieversGained} Gläubige schließen sich dir an." };
        if (outcome.UnlockedAbilityId is not null)
        {
            var ability = context.Definitions.Abilities.Get(outcome.UnlockedAbilityId);
            lines.Add($"EWIGE GABE: {ability.Name} – {ability.Description} (bleibt auch nach dem Tod)");
        }
        foreach (string companionId in outcome.UnlockedCompanionIds)
            lines.Add($"Neue Begleitseele erwacht: {context.Definitions.Companions.Get(companionId).Name}");
        if (outcome.LiberatedWorldName is not null) lines.Add($"{outcome.LiberatedWorldName} ist befreit!");
        foreach (var mission in outcome.CompletedMissions)
            lines.Add($"Bitte erfüllt: {mission.Title} (+{mission.RewardBelievers} Gläubige)");

        string title = outcome.GameCompleted ? "Das Inferno ist gebrochen"
            : outcome.CircleCompleted ? "Der Kreis ist befreit"
            : "Das Verlies ist geläutert";
        // Lambda wählt das Ziel erst beim Bestätigen -> kein unnötiger Szenenaufbau vorab
        Action next = outcome.GameCompleted
            ? () => context.Scenes.Replace(new TitleScene(context))
            : () => context.Scenes.Replace(new CircleIntroScene(context));
        return new MessageScene(context, title, string.Join("\n\n", lines), Palette.Gold, next);
    }
}
