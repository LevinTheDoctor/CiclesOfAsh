using CirclesOfAsh.Core;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Schrein der Reliquien: zeigt alle gesammelten Sammelobjekte über alle Läufe
/// (Collectibles-Zähler, persistiert in SQLite). Reine Info-Anzeige + Danke-Text.
/// </summary>
public sealed class ShrineScene : SceneBase
{
    public ShrineScene(GameContext context) : base(context) { }

    public override bool IsOverlay => true;

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Cancel) || Context.Input.WasPressed(GameAction.Confirm))
            Context.Scenes.Pop();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D pixel = Context.Assets.Pixel;
        float centerX = CirclesGame.VirtualWidth / 2f;

        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.65f);
        var panel = new Rectangle(70, 48, CirclesGame.VirtualWidth - 140, 170);
        UiDraw.Panel(spriteBatch, pixel, panel);
        Context.TitleFont.DrawCentered(spriteBatch, "Schrein der Reliquien", centerX, panel.Top + 6, Palette.Gold);

        var counts = Context.Collectibles.Where(pair => pair.Value > 0).ToList();
        float y = panel.Top + 34;
        if (counts.Count == 0)
        {
            Context.Font.DrawCentered(spriteBatch, "Der Schrein wartet auf Gaben aus der Tiefe.", centerX, y, Palette.Ash);
            Context.Font.DrawCentered(spriteBatch, "Gebetsperlen, Seelensplitter und Briefe finden hier ihren Ruheplatz.", centerX, y + 16, Palette.Ash * 0.85f);
        }
        else
        {
            Context.Font.DrawCentered(spriteBatch, "Die Gläubigen bewahren deine Funde:", centerX, y, Palette.Bone);
            y += 16;
            foreach (var (itemId, count) in counts)
            {
                string name = Context.Definitions.Items.Contains(itemId) ? Context.Definitions.Items.Get(itemId).Name : itemId;
                Context.Font.DrawCentered(spriteBatch, $"· {name}  ×{count}", centerX, y, Palette.Faith);
                y += 13;
            }
        }
        Context.Font.DrawCentered(spriteBatch, "Esc: zurück", centerX, panel.Bottom - 16, Palette.Ash);
        spriteBatch.End();
    }
}