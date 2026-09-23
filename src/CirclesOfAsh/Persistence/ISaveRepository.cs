using CirclesOfAsh.Progression;

namespace CirclesOfAsh.Persistence;

/// <summary>
/// Repository-Pattern: Die Spiellogik kennt nur diese Schnittstelle, nicht SQLite.
/// Alternative Speicherformen (JSON-Datei, Cloud) = neue Klasse, die das Interface implementiert,
/// und eine Zeile in GameContext.Create ändern.
/// </summary>
public interface ISaveRepository
{
    MetaState LoadMeta();
    void SaveMeta(MetaState meta);
    RunState? LoadRun();
    void SaveRun(RunState run);
    void DeleteRun();
}
