using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Kreis-Übersicht nach Dante: Höllentrichter mit aktueller Tiefe, Lore, Fortschritt der Verliese
/// und ein Menü (hinabsteigen, Inventar, Bitten). Je tiefer der Kreis, desto dunkler der Bildschirm.
/// </summary>
public sealed class CircleIntroScene : SceneBase
{
    private readonly MenuList _menu = new();
    private float _time;

    public CircleIntroScene(GameContext context) : base(context) { }

    // "!" (Null-Forgiving): Diese Szene wird nur mit aktivem Lauf geöffnet
    private RunState Run => Context.Progression.CurrentRun!;

    public override void OnEnter()
    {
        _menu.Add("Hinabsteigen", () => Context.Scenes.Replace(LoadingScene.ForDungeon(Context, Run)));
        _menu.Add("Inventar", () => Context.Scenes.Push(new InventoryScene(Context, Run, player: null)));
        _menu.Add("Bitten der Gläubigen", () => Context.Scenes.Push(new MissionBoardScene(Context)));
        _menu.Add("Zum Titel", () => Context.Scenes.Replace(new TitleScene(Context)));
    }

    public override void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        if (Context.Input.WasPressed(GameAction.Cancel)) Context.Scenes.Replace(new TitleScene(Context));
        else _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        WorldDefinition world = Context.Progression.WorldOf(Run);
        CircleDefinition circle = Context.Progression.CircleOf(Run);
        int dungeons = Context.Definitions.Balance.DungeonsPerCircle;
        Texture2D pixel = Context.Assets.Pixel;
        float darkness = MathF.Min(0.85f, 0.5f + 0.12f * Run.CircleIndex);   // tiefer = dunkler

        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, darkness, circle.Background);

        // Links: Trichter aller Kreise, der aktuelle pulsiert
        InfernoFunnel.Draw(spriteBatch, pixel, new Vector2(96, 74), 170f, 120f, world.Circles.Count, Run.CircleIndex, _time, animate: false);
        Context.Font.DrawCentered(spriteBatch, $"Tiefe {Run.CircleIndex + 1} / {world.Circles.Count}", 96, 212, Palette.Ash);

        // Rechts: Name, Lore, Verliese
        float rightX = 318;
        Context.Font.DrawCentered(spriteBatch, world.Name, rightX, 8, Palette.Ash);
        Context.TitleFont.DrawCentered(spriteBatch, circle.Name, rightX, 20, Palette.Gold);
        var panel = new Rectangle(190, 52, 270, 76);
        UiDraw.Panel(spriteBatch, pixel, panel);
        Context.Font.DrawShadowed(spriteBatch, Context.Font.Wrap(circle.Lore, panel.Width - 16), new Vector2(panel.Left + 8, panel.Top + 6), Palette.Bone);

        float spacing = 40f;
        float startX = rightX - (dungeons - 1) * spacing / 2f;
        for (int index = 0; index < dungeons; index++)
        {
            bool isBoss = index == dungeons - 1;
            bool isDone = index < Run.DungeonIndex;
            bool isCurrent = index == Run.DungeonIndex;
            bool hasPrison = circle.Prison is { } prison && prison.DungeonIndex == index;
            Color color = isDone ? Palette.Ash : isCurrent ? Palette.Faith : Palette.Shadow;
            int size = isBoss ? 12 : 8;
            var marker = new Rectangle((int)(startX + index * spacing) - size / 2, 142 - size / 2, size, size);
            UiDraw.Rect(spriteBatch, pixel, marker, isBoss && !isDone ? Palette.Blood * (isCurrent ? 1f : 0.5f) : color);
            UiDraw.Border(spriteBatch, pixel, marker, hasPrison ? Palette.Violet : Palette.Gold * 0.7f);   // violett = Kerker
            if (index < dungeons - 1)
                UiDraw.Rect(spriteBatch, pixel, new Rectangle(marker.Right + 2, 141, (int)spacing - size - 4, 2), Palette.Ash * 0.5f);
        }

        bool bossNext = Run.DungeonIndex >= dungeons - 1;
        bool prisonNext = circle.Prison is { } nextPrison && nextPrison.DungeonIndex == Run.DungeonIndex;
        string nextName = bossNext ? $"Thronsaal: {Context.Definitions.Enemies.Get(circle.Boss).Name}"
            : prisonNext ? $"Verlies {Run.DungeonIndex + 1} – Gerüchte von Gefangenen …"
            : $"Verlies {Run.DungeonIndex + 1}";
        Context.Font.DrawCentered(spriteBatch, nextName, rightX, 154, bossNext ? Palette.Blood : prisonNext ? Palette.Violet : Palette.Bone);

        string className = Context.Definitions.Classes.Get(Run.ClassId).Name;
        Context.Font.DrawCentered(spriteBatch, $"{Run.Appearance.Name}, {className} · Stufe {Run.Level} · Gläubige {Context.Progression.Meta.Believers}", rightX, 168, Palette.Faith);
        _menu.Draw(spriteBatch, Context.Font, rightX, 188);
        spriteBatch.End();
    }
}
