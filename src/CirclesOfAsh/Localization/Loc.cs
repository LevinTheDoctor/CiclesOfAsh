using System.Globalization;

namespace CirclesOfAsh.Localization;

/// <summary>
/// Überall erreichbarer Zugang zur Übersetzung. Statisch wie <see cref="Core.Log"/>, weil Texte an
/// sehr vielen Stellen entstehen – Szenen, Ansagen der Welt, Definitionen –, die den
/// <see cref="GameContext"/> nicht kennen und nicht kennen sollen.
///
/// Der Schlüssel ist der DEUTSCHE QUELLTEXT (gettext-Prinzip): <c>Loc.T("Neuer Lauf")</c>.
///   * Der Code bleibt lesbar – man sieht, was auf dem Bildschirm steht.
///   * Deutsch steht genau einmal im Projekt (im Code bzw. in Content/Data), nicht doppelt.
///   * Fehlt eine Übersetzung, erscheint der deutsche Text – nie ein leeres Feld oder ein Schlüssel.
/// Welche Texte noch keine Übersetzung haben, meldet <c>tools/LangCheck</c>.
/// </summary>
public static class Loc
{
    /// <summary>
    /// Die aktive Übersetzung. Vorbelegt mit einer leeren Instanz, die den Quelltext zurückgibt –
    /// so laufen auch Werkzeuge ohne Spielstart (SeedSweep) und Tests, ohne etwas laden zu müssen.
    /// </summary>
    public static Localizer Current { get; set; } = new();

    /// <summary>Übersetzt einen festen Text: <c>Loc.T("Optionen")</c>.</summary>
    public static string T(string source) => Current.Translate(source);

    /// <summary>
    /// Übersetzt eine Vorlage und setzt Werte ein: <c>Loc.T("Gläubige: {0}", believers)</c>.
    /// "params" sammelt beliebig viele Argumente in ein Array. Zahlen werden kulturneutral
    /// formatiert (Punkt als Dezimaltrenner), wie überall sonst im Spiel.
    /// </summary>
    public static string T(string source, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Current.Translate(source), args);

    /// <summary>
    /// Markiert einen Quelltext für die Übersetzungsprüfung, OHNE ihn jetzt zu übersetzen
    /// (entspricht gettext_noop). Für Texte in Konstanten und Tabellen, die erst beim Anzeigen
    /// durch <see cref="T(string)"/> laufen – sonst fände das Prüfwerkzeug sie nicht.
    /// </summary>
    public static string N(string source) => source;
}
