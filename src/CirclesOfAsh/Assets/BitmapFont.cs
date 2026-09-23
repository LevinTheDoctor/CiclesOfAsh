using System.Text;

namespace CirclesOfAsh.Assets;

/// <summary>
/// Pixel-Font aus einem Glyphen-Raster (PNG) mit individueller Zeichenbreite pro Buchstabe.
/// Ersetzt MonoGames SpriteFont, weil dieser die Content-Pipeline bräuchte (nicht austauschbar zur Laufzeit).
/// </summary>
public sealed class BitmapFont
{
    private const int Columns = 16;
    private readonly Texture2D _texture;
    private readonly int _cellWidth;
    private readonly int _cellHeight;
    private readonly Dictionary<char, (int Index, int Advance)> _glyphs = new();
    private readonly int _fallbackAdvance;

    public BitmapFont(Texture2D texture, FontEntry entry)
    {
        _texture = texture;
        _cellWidth = entry.CellWidth;
        _cellHeight = entry.CellHeight;
        LineHeight = entry.LineHeight > 0 ? entry.LineHeight : entry.CellHeight;
        for (int index = 0; index < entry.Charset.Length; index++)
        {
            int advance = index < entry.Advances.Length ? entry.Advances[index] : _cellWidth;
            _glyphs.TryAdd(entry.Charset[index], (index, advance));
        }
        _fallbackAdvance = _glyphs.TryGetValue(' ', out var space) ? space.Advance : _cellWidth / 2;
    }

    public int LineHeight { get; }

    public int MeasureWidth(string text, int scale = 1)
    {
        int widest = 0;
        foreach (string line in text.Split('\n'))
        {
            // Sum mit Lambda: Breite aller Zeichen addieren, unbekannte Zeichen bekommen die Leerzeichenbreite
            int width = line.Sum(character => _glyphs.TryGetValue(character, out var glyph) ? glyph.Advance : _fallbackAdvance);
            widest = Math.Max(widest, width);
        }
        return widest * scale;
    }

    public void Draw(SpriteBatch spriteBatch, string text, Vector2 position, Color color, int scale = 1)
    {
        float x = position.X;
        float y = position.Y;
        foreach (char character in text)
        {
            if (character == '\n')
            {
                x = position.X;
                y += LineHeight * scale;
                continue;
            }
            if (!_glyphs.TryGetValue(character, out var glyph))
            {
                x += _fallbackAdvance * scale;
                continue;
            }
            var source = new Rectangle(glyph.Index % Columns * _cellWidth, glyph.Index / Columns * _cellHeight, _cellWidth, _cellHeight);
            spriteBatch.Draw(_texture, new Vector2(MathF.Round(x), MathF.Round(y)), source, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            x += glyph.Advance * scale;
        }
    }

    /// <summary>Text mit 1-Pixel-Schatten -> auf unruhigem Hintergrund lesbar.</summary>
    public void DrawShadowed(SpriteBatch spriteBatch, string text, Vector2 position, Color color, int scale = 1)
    {
        Draw(spriteBatch, text, position + new Vector2(scale, scale), Color.Black * 0.8f, scale);
        Draw(spriteBatch, text, position, color, scale);
    }

    public void DrawCentered(SpriteBatch spriteBatch, string text, float centerX, float y, Color color, int scale = 1) =>
        DrawShadowed(spriteBatch, text, new Vector2(centerX - MeasureWidth(text, scale) / 2f, y), color, scale);

    /// <summary>Mehrzeiligen Text zeilenweise zentrieren (DrawCentered zentriert nur den Block als Ganzes).</summary>
    public void DrawCenteredLines(SpriteBatch spriteBatch, string text, float centerX, float y, Color color)
    {
        foreach (string line in text.Split('\n'))
        {
            DrawCentered(spriteBatch, line, centerX, y, color);
            y += LineHeight;
        }
    }

    /// <summary>Bricht Text an Wortgrenzen um, sodass keine Zeile breiter als maxWidth Pixel ist.</summary>
    public string Wrap(string text, int maxWidth)
    {
        var result = new StringBuilder();   // StringBuilder: effizientes Zusammensetzen vieler Strings
        foreach (string paragraph in text.Split('\n'))
        {
            var line = new StringBuilder();
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : $"{line} {word}";
                if (MeasureWidth(candidate) > maxWidth && line.Length > 0)
                {
                    result.AppendLine(line.ToString());
                    line.Clear().Append(word);
                }
                else
                {
                    line.Clear().Append(candidate);
                }
            }
            result.AppendLine(line.ToString());
        }
        return result.ToString().TrimEnd().Replace("\r", "");
    }
}
