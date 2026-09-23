using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.Progression;

/// <summary>
/// Bitten der Gläubigen. Spielsysteme melden Ereignisse (Report), der Service zählt Fortschritt
/// für passende AKTIVE Missionen und vergibt die Belohnung. Fortschritt ist Meta-Fortschritt:
/// er bleibt auch nach dem Tod erhalten.
/// Fortschritt für noch nicht angenommene Bitten wird vorgemerkt: Wer vorher 10 Ghule erlegt
/// und die Bitte dann annimmt, startet nicht bei 0 (häufiger "kaputt"-Eindruck).
/// </summary>
public sealed class MissionService
{
    private readonly DefinitionRegistry _definitions;
    private readonly MetaState _meta;

    public MissionService(DefinitionRegistry definitions, MetaState meta)
    {
        _definitions = definitions;
        _meta = meta;
    }

    public int MaxActive => _definitions.Balance.MaxActiveMissions;

    public IEnumerable<MissionDefinition> Active => WithStatus(MissionStatus.Active);
    public IEnumerable<MissionDefinition> Completed => WithStatus(MissionStatus.Completed);

    /// <summary>Angebotene Bitten: noch nicht angenommen und genug Gläubige.</summary>
    public IEnumerable<MissionDefinition> Available => _definitions.Missions.All
        .Where(mission => !_meta.Missions.ContainsKey(mission.Id) && _meta.Believers >= mission.RequiredBelievers);

    public IEnumerable<MissionDefinition> Locked => _definitions.Missions.All
        .Where(mission => !_meta.Missions.ContainsKey(mission.Id) && _meta.Believers < mission.RequiredBelievers);

    public bool CanAccept => Active.Count() < MaxActive;

    public int ProgressOf(MissionDefinition mission) =>
        _meta.Missions.TryGetValue(mission.Id, out MissionProgress? progress) ? progress.Progress : 0;

    public bool Accept(MissionDefinition mission)
    {
        if (!CanAccept || _meta.Missions.ContainsKey(mission.Id)) return false;
        var progress = new MissionProgress { Status = MissionStatus.Active };
        // Vorgemerkten Fortschritt übernehmen: Vor der Annahme erlegte Gegner/Gesammeltes zählt.
        if (_meta.PendingMissionProgress.TryGetValue(mission.Id, out int pending))
        {
            progress.Progress = Math.Min(mission.Count, pending);
            _meta.PendingMissionProgress.Remove(mission.Id);
        }
        _meta.Missions[mission.Id] = progress;
        return true;
    }

    public void Abandon(MissionDefinition mission)
    {
        if (_meta.Missions.TryGetValue(mission.Id, out MissionProgress? progress) && progress.Status == MissionStatus.Active)
            _meta.Missions.Remove(mission.Id);
    }

    /// <summary>Meldet ein Ereignis. Gibt die dadurch abgeschlossenen Missionen zurück.</summary>
    public List<MissionDefinition> Report(MissionType type, string target, int amount = 1)
    {
        var completed = new List<MissionDefinition>();
        foreach (MissionDefinition mission in Active.ToList())   // ToList: wir ändern den Status während der Schleife
        {
            bool targetMatches = mission.Target == "*" || string.Equals(mission.Target, target, StringComparison.OrdinalIgnoreCase);
            if (mission.Type != type || !targetMatches) continue;

            MissionProgress progress = _meta.Missions[mission.Id];
            progress.Progress = Math.Min(mission.Count, progress.Progress + amount);
            if (progress.Progress < mission.Count) continue;

            progress.Status = MissionStatus.Completed;
            _meta.Believers += mission.RewardBelievers;
            completed.Add(mission);
        }
        PendingReport(type, target, amount);
        return completed;
    }

    /// <summary>Zählt Fortschritt für verfügbare (noch nicht angenommene) Bitten vor. Beim Annehmen übernommen.</summary>
    private void PendingReport(MissionType type, string target, int amount)
    {
        foreach (MissionDefinition mission in Available)
        {
            bool targetMatches = mission.Target == "*" || string.Equals(mission.Target, target, StringComparison.OrdinalIgnoreCase);
            if (mission.Type != type || !targetMatches) continue;
            _meta.PendingMissionProgress[mission.Id] =
                Math.Min(mission.Count, _meta.PendingMissionProgress.GetValueOrDefault(mission.Id) + amount);
        }
    }

    private IEnumerable<MissionDefinition> WithStatus(MissionStatus status) => _definitions.Missions.All
        .Where(mission => _meta.Missions.TryGetValue(mission.Id, out MissionProgress? progress) && progress.Status == status);
}
