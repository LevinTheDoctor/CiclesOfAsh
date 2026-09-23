using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Props;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

/// <summary>
/// Weltobjekt ohne Kollision: Laternen, Fledermäuse, Hebel, Truhen, Käfige ...
/// Wie bei Gegnern steckt das Verhalten in einer austauschbaren Strategie (<see cref="IPropBehavior"/>).
/// State/Timer/Payload sind frei nutzbare Felder für das jeweilige Behavior.
/// </summary>
public sealed class Prop : Entity
{
    public Prop(PropDefinition definition, SpriteSheet sheet, IPropBehavior behavior, Vector2 bottomCenter,
                RoomNode room, string tag, int index)
    {
        Definition = definition;
        Behavior = behavior;
        Room = room;
        Tag = tag;
        Index = index;
        Animation = new AnimationPlayer(sheet);
        Size = new Point(definition.Width, definition.Height);
        Position = bottomCenter - new Vector2(definition.Width / 2f, definition.Height);
        LightRadius = definition.LightRadius;
        LightColor = ColorUtil.FromHex(definition.LightColor, Color.White);
        CanInteract = definition.Interactable;
    }

    public PropDefinition Definition { get; }
    public IPropBehavior Behavior { get; }
    public RoomNode Room { get; }
    public string Tag { get; }
    public int Index { get; }
    public AnimationPlayer Animation { get; }
    public int State { get; set; }
    public float Timer { get; set; }
    public float LightRadius { get; set; }
    public Color LightColor { get; set; }
    public Color Tint { get; set; } = Color.White;
    public bool CanInteract { get; set; }
    public bool IsFlipped { get; set; }
    /// <summary>Zusatzdaten, z. B. die Runenfolge auf der Wandtafel.</summary>
    public IReadOnlyList<int> Payload { get; set; } = Array.Empty<int>();

    public override void Update(DungeonWorld world, float deltaSeconds)
    {
        Animation.Update(deltaSeconds);
        Behavior.Update(this, world, deltaSeconds);
    }

    public override void Draw(SpriteBatch spriteBatch) => Behavior.Draw(spriteBatch, this);

    /// <summary>Standarddarstellung: aktuelle Animation, unten mittig verankert.</summary>
    public void DrawSprite(SpriteBatch spriteBatch) => Animation.Draw(spriteBatch, BottomCenter, IsFlipped, Tint);
}
