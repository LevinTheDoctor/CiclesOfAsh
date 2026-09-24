using CirclesOfAsh.Abilities;
using CirclesOfAsh.Companions;
using CirclesOfAsh.Enemies;
using CirclesOfAsh.Props;
using CirclesOfAsh.Puzzles;

namespace CirclesOfAsh.Modding;

/// <summary>
/// Zentrale Registry (Factory-Pattern): verbindet die String-Schlüssel aus den JSON-Daten mit
/// C#-Klassen. Gespeichert werden Fabrikfunktionen (Func&lt;T&gt;), keine Instanzen, weil jede
/// Fähigkeit/jeder Gegner eigenen Zustand braucht.
///
/// ERWEITERN = eine Zeile in <see cref="CreateWithBuiltIns"/> + JSON-Eintrag. Kein anderer Code ändert sich
/// (Open/Closed-Prinzip: offen für Erweiterung, geschlossen für Änderung).
/// </summary>
public sealed class BehaviorRegistry
{
    private readonly Dictionary<string, Func<IAbilityBehavior>> _abilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<IEnemyBrain>> _enemyBrains = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<IBossAttack>> _bossAttacks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<ICompanionBehavior>> _companions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<IPropBehavior>> _props = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<IPuzzle>> _puzzles = new(StringComparer.OrdinalIgnoreCase);

    public static BehaviorRegistry CreateWithBuiltIns()
    {
        var registry = new BehaviorRegistry();

        // "() => new X()" = Lambda ohne Parameter, das bei jedem Aufruf eine NEUE Instanz liefert
        registry.RegisterAbility("projectile", () => new ProjectileAbility());
        registry.RegisterAbility("melee_arc", () => new MeleeArcAbility());
        registry.RegisterAbility("nova", () => new NovaAbility());
        registry.RegisterAbility("orbit", () => new OrbitAbility());
        registry.RegisterAbility("stealth", () => new StealthAbility());
        registry.RegisterAbility("dash", () => new DashAbility());
        registry.RegisterAbility("air_jump", () => new AirJumpAbility());
        registry.RegisterAbility("glide", () => new GlideAbility());

        registry.RegisterEnemyBrain("walker", () => new WalkerBrain());
        registry.RegisterEnemyBrain("flyer", () => new FlyerBrain());
        registry.RegisterEnemyBrain("caster", () => new CasterBrain());
        registry.RegisterEnemyBrain("boss", () => new BossBrain());
        registry.RegisterEnemyBrain("charger", () => new ChargerBrain());
        registry.RegisterEnemyBrain("swarmer", () => new SwarmerBrain());
        registry.RegisterEnemyBrain("ambusher", () => new AmbusherBrain());

        registry.RegisterBossAttack("charge", () => new ChargeAttack());
        registry.RegisterBossAttack("projectile_ring", () => new ProjectileRingAttack());
        registry.RegisterBossAttack("summon", () => new SummonAttack());
        registry.RegisterBossAttack("slam", () => new SlamAttack());

        registry.RegisterCompanion("attacker", () => new AttackerCompanion());
        registry.RegisterCompanion("healer", () => new HealerCompanion());
        registry.RegisterCompanion("mana", () => new ManaCompanion());

        registry.RegisterProp("static", () => new StaticProp());
        registry.RegisterProp("bats", () => new BatProp());
        registry.RegisterProp("lever", () => new LeverProp());
        registry.RegisterProp("rune_pillar", () => new RunePillarProp());
        registry.RegisterProp("mural", () => new MuralProp());
        registry.RegisterProp("brazier", () => new BrazierProp());
        registry.RegisterProp("chest", () => new ChestProp());
        registry.RegisterProp("cage", () => new CageProp());
        registry.RegisterProp("breakable", () => new BreakableProp());
        registry.RegisterProp("critter", () => new CritterProp());
        registry.RegisterProp("pressure_plate", () => new PressurePlateProp());
        registry.RegisterProp("push_block", () => new PushBlockProp());
        registry.RegisterProp("mirror", () => new MirrorProp());

        registry.RegisterPuzzle("levers", () => new LeverPuzzle());
        registry.RegisterPuzzle("rune_order", () => new RuneOrderPuzzle());
        registry.RegisterPuzzle("braziers", () => new BrazierPuzzle());
        registry.RegisterPuzzle("weights", () => new WeightPuzzle());
        registry.RegisterPuzzle("mirrors", () => new MirrorPuzzle());
        return registry;
    }

    public void RegisterAbility(string key, Func<IAbilityBehavior> factory) => _abilities[key] = factory;
    public void RegisterEnemyBrain(string key, Func<IEnemyBrain> factory) => _enemyBrains[key] = factory;
    public void RegisterBossAttack(string key, Func<IBossAttack> factory) => _bossAttacks[key] = factory;
    public void RegisterCompanion(string key, Func<ICompanionBehavior> factory) => _companions[key] = factory;
    public void RegisterProp(string key, Func<IPropBehavior> factory) => _props[key] = factory;
    public void RegisterPuzzle(string key, Func<IPuzzle> factory) => _puzzles[key] = factory;

    public bool HasAbility(string key) => _abilities.ContainsKey(key);
    public bool HasEnemyBrain(string key) => _enemyBrains.ContainsKey(key);
    public bool HasBossAttack(string key) => _bossAttacks.ContainsKey(key);
    public bool HasCompanion(string key) => _companions.ContainsKey(key);
    public bool HasProp(string key) => _props.ContainsKey(key);
    public bool HasPuzzle(string key) => _puzzles.ContainsKey(key);

    public IAbilityBehavior CreateAbility(string key) => Create(_abilities, key, "Fähigkeit");
    public IEnemyBrain CreateEnemyBrain(string key) => Create(_enemyBrains, key, "Gegner-Brain");
    public IBossAttack CreateBossAttack(string key) => Create(_bossAttacks, key, "Boss-Angriff");
    public ICompanionBehavior CreateCompanion(string key) => Create(_companions, key, "Begleiter");
    public IPropBehavior CreateProp(string key) => Create(_props, key, "Prop");
    public IPuzzle CreatePuzzle(string key) => Create(_puzzles, key, "Rätsel");

    // Generische Hilfsmethode: eine Implementierung für alle Registries (DRY)
    private static T Create<T>(Dictionary<string, Func<T>> factories, string key, string kind) =>
        factories.TryGetValue(key, out Func<T>? factory)
            ? factory()
            : throw new KeyNotFoundException($"{kind} '{key}' ist nicht registriert. Bekannt: {string.Join(", ", factories.Keys)}");
}
