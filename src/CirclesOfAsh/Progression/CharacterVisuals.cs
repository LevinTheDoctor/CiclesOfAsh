using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.Progression;

/// <summary>Baut aus Klasse + Aussehen das Ebenen-Sprite (Körper, Haare, Outfit, Akzent).</summary>
public static class CharacterVisuals
{
    public static LayeredSprite Create(GameContext context, ClassDefinition playerClass, CharacterAppearance look)
    {
        AppearanceDefinition options = context.Definitions.Appearance;
        var layers = new List<(SpriteSheet Sheet, Color Tint)>
        {
            (context.Assets.GetSpriteSheet(options.BodySprite), Pick(options.SkinTones, look.SkinTone)),
        };

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
        return new LayeredSprite(layers);
    }

    /// <summary>Index in den gültigen Bereich "falten" – auch negative Werte (-1 -> letztes Element).</summary>
    public static int Wrap(int index, int count) => count == 0 ? 0 : ((index % count) + count) % count;

    private static Color Pick(IReadOnlyList<string> colors, int index) =>
        colors.Count == 0 ? Color.White : ColorUtil.FromHex(colors[Wrap(index, colors.Count)], Color.White);
}
