using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.Progression;

/// <summary>Baut aus Klasse + Aussehen das Ebenen-Sprite (Körper, Haare, Outfit, Akzent).</summary>
public static class CharacterVisuals
{
    /// <param name="armorSprite">
    /// Sprite der getragenen Rüstung oder null. Liegt über der Kleidung – ohne Rüstung bleibt der
    /// Körper sichtbar, und man erkennt Statur und Taille.
    /// </param>
    public static LayeredSprite Create(GameContext context, ClassDefinition playerClass, CharacterAppearance look,
                                       string? armorSprite = null)
    {
        AppearanceDefinition options = context.Definitions.Appearance;
        var layers = new List<(SpriteSheet Sheet, Color Tint)>();

        // Flügel zuerst: Die Liste wird in dieser Reihenfolge gezeichnet, sie liegen also HINTER
        // der Figur. Akzentfarbe färbt sie ein, damit sie zur Klasse passen.
        if (SpriteOf(options.WingStyles, look.Wings) is { } wingSprite)
            layers.Add((context.Assets.GetSpriteSheet(wingSprite), Pick(options.AccentColors, look.AccentColor)));

        // Körpertyp (Geschlecht und Statur). Ohne definierte Typen gilt der alte Einzelkörper.
        string body = SpriteOf(options.BodyTypes, look.BodyType) ?? options.BodySprite;
        layers.Add((context.Assets.GetSpriteSheet(body), Pick(options.SkinTones, look.SkinTone)));

        // Make-up über den Körper, aber UNTER die Haare – sonst läge Lidschatten über der Stirnlocke.
        if (SpriteOf(options.MakeupStyles, look.Makeup) is { } makeupSprite)
            layers.Add((context.Assets.GetSpriteSheet(makeupSprite), Pick(options.MakeupColors, look.MakeupColor)));

        if (options.HairStyles.Count > 0)
        {
            HairStyleDefinition hair = options.HairStyles[Wrap(look.HairStyle, options.HairStyles.Count)];
            if (!string.IsNullOrEmpty(hair.Sprite))
                layers.Add((context.Assets.GetSpriteSheet(hair.Sprite), Pick(options.HairColors, look.HairColor)));
        }
        if (!string.IsNullOrEmpty(playerClass.OutfitSprite))
            layers.Add((context.Assets.GetSpriteSheet(playerClass.OutfitSprite), Color.White));
        if (!string.IsNullOrEmpty(playerClass.AccentSprite))
            layers.Add((context.Assets.GetSpriteSheet(playerClass.AccentSprite), Pick(options.AccentColors, look.AccentColor)));

        // Rüstung ganz oben: Sie deckt Kleidung und Rumpf ab. Genau deshalb sieht man ohne sie
        // die Figur – Taille, Bauchmuskeln, Statur.
        if (!string.IsNullOrEmpty(armorSprite))
            layers.Add((context.Assets.GetSpriteSheet(armorSprite), Color.White));
        return new LayeredSprite(layers);
    }

    /// <summary>
    /// Sprite der gewählten Option – oder null, wenn die Liste leer ist oder die Option bewusst
    /// keine Ebene hat (leerer Sprite, z. B. "Ohne Make-up").
    /// </summary>
    public static string? SpriteOf(IReadOnlyList<AppearanceOptionDefinition> options, int index)
    {
        if (options.Count == 0) return null;
        string sprite = options[Wrap(index, options.Count)].Sprite;
        return string.IsNullOrEmpty(sprite) ? null : sprite;
    }

    /// <summary>Index in den gültigen Bereich "falten" – auch negative Werte (-1 -> letztes Element).</summary>
    public static int Wrap(int index, int count) => count == 0 ? 0 : ((index % count) + count) % count;

    private static Color Pick(IReadOnlyList<string> colors, int index) =>
        colors.Count == 0 ? Color.White : ColorUtil.FromHex(colors[Wrap(index, colors.Count)], Color.White);
}
