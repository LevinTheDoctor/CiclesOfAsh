namespace CirclesOfAsh.Core;

/// <summary>Zentrale gotische Farbpalette. Eine Quelle der Wahrheit statt verstreuter Farbwerte (DRY).</summary>
public static class Palette
{
    // "new(10, 8, 14)" = target-typed new: Der Typ (Color) steht links und muss nicht wiederholt werden.
    public static readonly Color Void = new(10, 8, 14);
    public static readonly Color Bone = new(222, 214, 192);
    public static readonly Color Blood = new(150, 24, 36);
    public static readonly Color Ember = new(232, 120, 40);
    public static readonly Color Gold = new(212, 170, 72);
    public static readonly Color Faith = new(240, 220, 140);
    public static readonly Color Mana = new(96, 110, 230);
    public static readonly Color Soul = new(110, 230, 190);
    public static readonly Color Shadow = new(32, 24, 44);
    public static readonly Color Ash = new(120, 112, 128);
    public static readonly Color Violet = new(170, 100, 230);
    public static readonly Color PanelFill = new(18, 14, 26, 235);
}
