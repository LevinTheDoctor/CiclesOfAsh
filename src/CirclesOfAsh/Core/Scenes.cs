using Microsoft.Xna.Framework.Graphics;

namespace CirclesOfAsh.Core;

/// <summary>
/// Eine Szene = ein Bildschirm (Titel, Dungeon, Pause ...). State-Pattern: Der SceneManager hält
/// den aktuellen Zustand, jede Szene kapselt ihr eigenes Verhalten.
/// </summary>
public interface IScene
{
    /// <summary>Overlays (Pause, Level-Up) lassen die Szene darunter sichtbar, aber eingefroren.</summary>
    bool IsOverlay { get; }
    void OnEnter();
    void OnExit();
    void Update(float deltaSeconds);

    /// <summary>
    /// Wird VOR dem Zeichnen in die Leinwand aufgerufen. Hier dürfen Szenen eigene Render-Targets
    /// befüllen (z. B. die Lichtkarte), ohne die Leinwand zu überschreiben.
    /// </summary>
    void PrepareDraw(SpriteBatch spriteBatch);
    void Draw(SpriteBatch spriteBatch);
}

/// <summary>Basisklasse mit leeren Standardimplementierungen (Template-Method-Idee): Szenen überschreiben nur, was sie brauchen.</summary>
public abstract class SceneBase : IScene
{
    protected SceneBase(GameContext context) => Context = context;

    protected GameContext Context { get; }

    public virtual bool IsOverlay => false;
    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    public abstract void Update(float deltaSeconds);
    public virtual void PrepareDraw(SpriteBatch spriteBatch) { }
    public abstract void Draw(SpriteBatch spriteBatch);
}

/// <summary>
/// Stapel (Stack) von Szenen. Änderungen werden in eine Warteschlange gelegt und erst zwischen
/// zwei Updates angewandt -> keine "Collection was modified"-Fehler, wenn eine Szene sich selbst ersetzt.
/// </summary>
public sealed class SceneManager
{
    private readonly List<IScene> _stack = new();
    private readonly Queue<Action> _pendingChanges = new();

    public void Push(IScene scene) => _pendingChanges.Enqueue(() =>
    {
        _stack.Add(scene);
        scene.OnEnter();
    });

    public void Pop() => _pendingChanges.Enqueue(() =>
    {
        if (_stack.Count == 0) return;
        IScene top = _stack[^1];   // "^1" = Index vom Ende: letztes Element
        _stack.RemoveAt(_stack.Count - 1);
        top.OnExit();
    });

    /// <summary>Leert den kompletten Stapel und startet mit einer neuen Szene.</summary>
    public void Replace(IScene scene) => _pendingChanges.Enqueue(() =>
    {
        for (int index = _stack.Count - 1; index >= 0; index--) _stack[index].OnExit();
        _stack.Clear();
        _stack.Add(scene);
        scene.OnEnter();
    });

    public void Update(float deltaSeconds)
    {
        ApplyPendingChanges();
        if (_stack.Count > 0) _stack[^1].Update(deltaSeconds);   // nur die oberste Szene ist aktiv
        ApplyPendingChanges();
    }

    public void PrepareDraw(SpriteBatch spriteBatch)
    {
        for (int index = FirstVisibleIndex(); index < _stack.Count; index++) _stack[index].PrepareDraw(spriteBatch);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        for (int index = FirstVisibleIndex(); index < _stack.Count; index++) _stack[index].Draw(spriteBatch);
    }

    /// <summary>Von oben nach unten laufen, solange Overlays liegen -> ab dort aufwärts ist alles sichtbar.</summary>
    private int FirstVisibleIndex()
    {
        int firstVisible = _stack.Count - 1;
        while (firstVisible > 0 && _stack[firstVisible].IsOverlay) firstVisible--;
        return Math.Max(firstVisible, 0);
    }

    private void ApplyPendingChanges()
    {
        while (_pendingChanges.Count > 0) _pendingChanges.Dequeue().Invoke();
    }
}
