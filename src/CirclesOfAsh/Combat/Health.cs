namespace CirclesOfAsh.Combat;

/// <summary>Wer steht auf welcher Seite? Verhindert "Friendly Fire" bei Projektilen.</summary>
public enum Faction { Player, Enemy }

/// <summary>Lebenspunkte + kurze Unverwundbarkeit nach Treffern (klassische "i-frames").</summary>
public sealed class Health
{
    public Health(float max)
    {
        Max = max;
        Current = max;
    }

    public float Max { get; private set; }
    public float Current { get; private set; }
    public float InvulnerableSeconds { get; private set; }
    public bool IsDead => Current <= 0f;
    public bool IsInvulnerable => InvulnerableSeconds > 0f;
    public float Ratio => Max <= 0f ? 0f : Current / Max;

    public void Update(float deltaSeconds) => InvulnerableSeconds = MathF.Max(0f, InvulnerableSeconds - deltaSeconds);

    /// <summary>Gibt den tatsächlich verursachten Schaden zurück (0, wenn unverwundbar oder bereits tot).</summary>
    public float TakeDamage(float amount, float invulnerabilityAfterHit)
    {
        if (IsDead || IsInvulnerable || amount <= 0f) return 0f;
        float applied = MathF.Min(Current, amount);
        Current -= applied;
        InvulnerableSeconds = invulnerabilityAfterHit;
        return applied;
    }

    /// <summary>Kurze Unverwundbarkeit ohne Schaden (z. B. während eines Dashs).</summary>
    public void GrantInvulnerability(float seconds) => InvulnerableSeconds = MathF.Max(InvulnerableSeconds, seconds);

    public void Heal(float amount) => Current = MathF.Min(Max, Current + MathF.Max(0f, amount));

    /// <summary>Neues Maximum; das prozentuale Verhältnis bleibt erhalten (Upgrade soll nicht "leer" wirken).</summary>
    public void SetMax(float newMax)
    {
        float ratio = Ratio;
        Max = MathF.Max(1f, newMax);
        Current = MathF.Max(1f, Max * ratio);
    }
}
