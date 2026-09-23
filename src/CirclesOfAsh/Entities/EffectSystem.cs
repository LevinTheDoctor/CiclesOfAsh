using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;

namespace CirclesOfAsh.Entities;

/// <summary>Rein visuelle Effekte ohne Spiellogik: Partikel, Schadenszahlen, einmalige Sprite-Animationen.</summary>
public sealed class EffectSystem
{
    private const int MaxParticles = 800;

    // Verschachtelte private Klassen: nur für das EffectSystem relevant -> nach außen unsichtbar
    private sealed class Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Life;
        public float MaxLife;
        public float Gravity;
    }

    private sealed class FloatingText
    {
        public Vector2 Position;
        public string Text = "";
        public Color Color;
        public float Life;
    }

    private sealed record SpriteEffect(AnimationPlayer Animation, Vector2 BottomCenter, bool Flip);

    private readonly List<Particle> _particles = new();
    private readonly List<FloatingText> _texts = new();
    private readonly List<SpriteEffect> _sprites = new();
    private readonly Random _random;

    private float _ambientAccumulator;

    public EffectSystem(Random random) => _random = random;

    /// <summary>
    /// Umgebungspartikel je Kreis: Staub im Limbus, Asche in der Gier, Glut im Zorn.
    /// Akkumulator-Muster: Bruchteile von Partikeln pro Frame werden aufsummiert -> gleichmäßige Rate.
    /// </summary>
    public void Ambient(string kind, Rectangle area, float deltaSeconds)
    {
        (float rate, Color color) = kind switch   // Tupel-Dekonstruktion aus einem switch-Ausdruck
        {
            "dust" => (6f, new Color(170, 165, 185) * 0.6f),
            "ash" => (14f, new Color(140, 130, 140) * 0.8f),
            "embers" => (12f, Palette.Ember),
            "drips" => (3f, new Color(120, 160, 200)),
            _ => (0f, Color.Transparent),
        };
        _ambientAccumulator += rate * deltaSeconds;
        while (_ambientAccumulator >= 1f && _particles.Count < MaxParticles)
        {
            _ambientAccumulator -= 1f;
            float x = area.Left + _random.NextSingle() * area.Width;
            (Vector2 position, Vector2 velocity, float gravity, float life) = kind switch
            {
                "embers" => (new Vector2(x, area.Bottom), new Vector2(_random.NextSingle() * 20f - 10f, -25f - _random.NextSingle() * 30f), -8f, 5f),
                "ash" => (new Vector2(x, area.Top), new Vector2(_random.NextSingle() * 16f - 4f, 18f + _random.NextSingle() * 18f), 0f, 9f),
                "drips" => (new Vector2(x, area.Top), Vector2.Zero, 320f, 1.2f),
                _ => (new Vector2(x, area.Top + _random.NextSingle() * area.Height), new Vector2(_random.NextSingle() * 10f - 5f, _random.NextSingle() * 6f - 3f), 0f, 4f),
            };
            _particles.Add(new Particle { Position = position, Velocity = velocity, Color = color, Life = life, MaxLife = life, Gravity = gravity });
        }
    }

    public void Burst(Vector2 position, Color color, int count, float speed, float lifetime = 0.5f, float gravity = 250f)
    {
        for (int index = 0; index < count && _particles.Count < MaxParticles; index++)
        {
            float life = lifetime * (0.5f + _random.NextSingle() * 0.5f);
            // Objekt-Initialisierer { ... }: Felder direkt beim Erzeugen setzen
            _particles.Add(new Particle
            {
                Position = position,
                Velocity = MathUtil.RandomDirection(_random) * speed * (0.3f + _random.NextSingle()),
                Color = color,
                Life = life,
                MaxLife = life,
                Gravity = gravity,
            });
        }
    }

    public void Ring(Vector2 center, float radius, Color color, int count = 28)
    {
        for (int index = 0; index < count && _particles.Count < MaxParticles; index++)
        {
            float angle = index / (float)count * MathHelper.TwoPi;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _particles.Add(new Particle
            {
                Position = center + direction * radius * 0.3f,
                Velocity = direction * radius * 2.2f,
                Color = color,
                Life = 0.35f,
                MaxLife = 0.35f,
            });
        }
    }

    public void Text(Vector2 position, string text, Color color) =>
        _texts.Add(new FloatingText { Position = position, Text = text, Color = color, Life = 0.8f });

    public void PlaySprite(SpriteSheet sheet, Vector2 bottomCenter, bool flip)
    {
        var animation = new AnimationPlayer(sheet);
        _sprites.Add(new SpriteEffect(animation, bottomCenter, flip));
    }

    public void Update(float deltaSeconds)
    {
        foreach (Particle particle in _particles)
        {
            particle.Life -= deltaSeconds;
            particle.Velocity.Y += particle.Gravity * deltaSeconds;
            particle.Position += particle.Velocity * deltaSeconds;
        }
        _particles.RemoveAll(particle => particle.Life <= 0f);   // RemoveAll mit Prädikat (Lambda, das bool liefert)

        foreach (FloatingText text in _texts)
        {
            text.Life -= deltaSeconds;
            text.Position.Y -= 20f * deltaSeconds;
        }
        _texts.RemoveAll(text => text.Life <= 0f);

        foreach (SpriteEffect sprite in _sprites) sprite.Animation.Update(deltaSeconds);
        _sprites.RemoveAll(sprite => sprite.Animation.IsFinished);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, BitmapFont font)
    {
        foreach (SpriteEffect sprite in _sprites) sprite.Animation.Draw(spriteBatch, sprite.BottomCenter, sprite.Flip, Color.White);
        foreach (Particle particle in _particles)
        {
            float fade = particle.Life / particle.MaxLife;
            spriteBatch.Draw(pixel, new Rectangle((int)particle.Position.X, (int)particle.Position.Y, 2, 2), particle.Color * fade);
        }
        foreach (FloatingText text in _texts)
        {
            int width = font.MeasureWidth(text.Text);
            font.DrawShadowed(spriteBatch, text.Text, new Vector2(text.Position.X - width / 2f, text.Position.Y), text.Color * MathF.Min(1f, text.Life * 3f));
        }
    }
}
