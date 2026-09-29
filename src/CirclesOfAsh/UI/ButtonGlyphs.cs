using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Localization;

namespace CirclesOfAsh.UI;

/// <summary>
/// Zeichnet Tasten als Bild statt als Buchstaben: die Blätter "glyphs.&lt;familie&gt;" aus
/// tools/assetgen/interface.py (Xbox, PlayStation, Switch, Tastatur).
///
/// Gesucht wird das Einzelbild, das GENAU so heißt wie die Beschriftung aus controllers.json
/// ("A", "RB", "○", "Menü"). Gibt es keins, wird ein leerer Knopf beschriftet – bei Controllern ein
/// runder Frontknopf, bei der Tastatur eine Tastenkappe in passender Breite. So bekommt auch ein
/// unbekannter Controller oder eine lange Taste ("Umschalt") ein lesbares Bild.
///
/// Statisch wie <see cref="UiDraw"/>: reine Zeichenhelfer ohne eigenen Zustand.
/// </summary>
public static class ButtonGlyphs
{
    /// <summary>Höhe jedes Tastenbilds in Pixeln der virtuellen Auflösung.</summary>
    public const int Height = 12;

    private const int FrameWidth = 16;
    /// <summary>So breit sind Rand und Ecke der Tastenkappe links und rechts (3-Slice, siehe Draw).</summary>
    private const int KeycapEdge = 4;
    /// <summary>Luft zwischen Beschriftung und Kappenrand.</summary>
    private const int KeycapPadding = 3;
    /// <summary>Längste Beschriftung, die noch auf einen runden Knopf passt (Zeichen).</summary>
    private const int RoundCaptionLength = 2;

    /// <summary>Breite des Tastenbilds in Pixeln – für Layouts, die Bild und Text nebeneinandersetzen.</summary>
    public static int Measure(GameContext context, string family, string label) =>
        Resolve(context, family, label).Width;

    /// <summary>Zeichnet das Tastenbild mit der linken oberen Ecke bei <paramref name="topLeft"/>.</summary>
    public static void Draw(SpriteBatch spriteBatch, GameContext context, string family, string label,
                            Vector2 topLeft, Color tint)
    {
        Glyph glyph = Resolve(context, family, label);
        var position = new Vector2(MathF.Round(topLeft.X), MathF.Round(topLeft.Y));
        if (glyph.Width == FrameWidth)
        {
            spriteBatch.Draw(glyph.Sheet.Texture, position, glyph.Source, tint);
        }
        else
        {
            // 3-Slice: linke und rechte Kante unverändert, die (einfarbige) Mitte in die Breite ziehen.
            Rectangle source = glyph.Source;
            var left = new Rectangle(source.X, source.Y, KeycapEdge, source.Height);
            var middle = new Rectangle(source.X + KeycapEdge, source.Y, 1, source.Height);
            var right = new Rectangle(source.Right - KeycapEdge, source.Y, KeycapEdge, source.Height);
            int middleWidth = glyph.Width - 2 * KeycapEdge;
            spriteBatch.Draw(glyph.Sheet.Texture, position, left, tint);
            spriteBatch.Draw(glyph.Sheet.Texture,
                new Rectangle((int)position.X + KeycapEdge, (int)position.Y, middleWidth, source.Height), middle, tint);
            spriteBatch.Draw(glyph.Sheet.Texture, position + new Vector2(KeycapEdge + middleWidth, 0), right, tint);
        }

        if (glyph.Caption is null) return;
        BitmapFont font = context.Font;
        float captionX = position.X + (glyph.Width - font.MeasureWidth(glyph.Caption)) / 2f;
        // Die Schrift hat oben zwei leere Pixelzeilen – so sitzt die Beschriftung mittig auf der Kappe.
        font.Draw(spriteBatch, glyph.Caption, new Vector2(captionX, position.Y), Palette.Bone * (tint.A / 255f));
    }

    /// <summary>Was genau gezeichnet wird: Blatt, Ausschnitt, Breite und ggf. Beschriftung darauf.</summary>
    private readonly record struct Glyph(SpriteSheet Sheet, Rectangle Source, int Width, string? Caption);

    private static Glyph Resolve(GameContext context, string family, string label)
    {
        // 1) Eigenes Bild in der Familie ("A", "RB", "○" …)
        string sheetId = $"glyphs.{family}";
        bool hasSheet = context.Assets.HasSpriteSheet(sheetId);
        SpriteSheet? sheet = hasSheet ? context.Assets.GetSpriteSheet(sheetId) : null;
        if (sheet is not null && sheet.HasClip(label))
            return new Glyph(sheet, sheet.GetFrameRectangle(sheet.GetClip(label), 0), FrameWidth, null);

        // 2) Controller ohne passendes Bild: beschrifteter runder Knopf (z. B. Logitech "1" … "10")
        string caption = Loc.T(label);
        if (sheet is not null && sheet.HasClip("round") && caption.Length <= RoundCaptionLength)
            return new Glyph(sheet, sheet.GetFrameRectangle(sheet.GetClip("round"), 0), FrameWidth, caption);

        // 3) Tastatur (und alles Übrige): Tastenkappe, so breit wie die Beschriftung es braucht
        SpriteSheet keys = context.Assets.GetSpriteSheet($"glyphs.{InputState.KeyboardGlyphFamily}");
        int width = Math.Max(FrameWidth, context.Font.MeasureWidth(caption) + 2 * KeycapPadding);
        return new Glyph(keys, keys.GetFrameRectangle(keys.GetClip("key"), 0), width, caption);
    }
}
