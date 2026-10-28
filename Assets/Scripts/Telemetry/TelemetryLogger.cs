using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class TelemetryLogger : MonoBehaviour
{
    private const string Header = "timestamp,session,mode,teamSize,round,roundName,glimmers,inPen,allIn,sealed,timeLeft,elapsed,sealStart,leaks,basePoints,sealBonus,sweepBonus,timeBonus,score,grade,byType";

    [SerializeField] private RoundController round;
    [SerializeField] private MousePlayerSource players;
    [SerializeField] private string fileName = "telemetry.csv";

    private string path;
    private string session;
    private int leaks;
    private float sealStart;

    public string Path => path;

    private void Awake()
    {
        if (round == null)
        {
            Debug.LogError("TelemetryLogger needs a RoundController.", this);
            enabled = false;
            return;
        }

        path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        session = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        round.PhaseChanged += OnPhaseChanged;
        round.GlimmerLeaked += OnLeaked;
        round.RoundEnded += OnRoundEnded;
        Debug.Log($"Telemetry file: {path}");
    }

    private void OnDestroy()
    {
        if (round == null) return;
        round.PhaseChanged -= OnPhaseChanged;
        round.GlimmerLeaked -= OnLeaked;
        round.RoundEnded -= OnRoundEnded;
    }

    private void OnPhaseChanged(RoundPhase phase)
    {
        if (phase == RoundPhase.Intro)
        {
            leaks = 0;
            sealStart = -1f;
        }
        else if (phase == RoundPhase.Seal)
        {
            sealStart = round.RoundDuration - round.TimeLeft;
        }
    }

    private void OnLeaked(GlimmerAgent agent)
    {
        leaks++;
    }

    private void OnRoundEnded(RoundResult result)
    {
        ScoreBreakdown s = result.score;
        string roundName = round.CurrentRound != null ? round.CurrentRound.DisplayName : string.Empty;
        string row = string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            session,
            Mode(result.teamSize),
            result.teamSize,
            result.roundIndex + 1,
            Csv(roundName),
            result.glimmersTotal,
            result.glimmersInPen,
            s.allIn ? 1 : 0,
            result.gateSealed ? 1 : 0,
            Number(result.timeLeft),
            Number(result.elapsed),
            Number(sealStart),
            leaks,
            s.basePoints,
            s.sealBonus,
            s.sweepBonus,
            s.timeBonus,
            s.total,
            s.grade,
            Csv(TypeText()));
        Append(row);
    }

    private string Mode(int teamSize)
    {
        if (players != null && !players.IncludeHuman) return "bots";
        return teamSize > 1 ? "human+bots" : "human";
    }

    private string TypeText()
    {
        List<string> names = new List<string>();
        Dictionary<string, int> totals = new Dictionary<string, int>();
        Dictionary<string, int> banked = new Dictionary<string, int>();

        IReadOnlyList<GlimmerAgent> agents = round.Agents;
        if (agents == null) return string.Empty;

        for (int i = 0; i < agents.Count; i++)
        {
            GlimmerAgent agent = agents[i];
            if (agent == null || agent.Archetype == null) continue;

            string key = agent.Archetype.DisplayName;
            if (!totals.ContainsKey(key))
            {
                names.Add(key);
                totals[key] = 0;
                banked[key] = 0;
            }
            totals[key]++;
            if (round.IsBanked(agent)) banked[key]++;
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < names.Count; i++)
        {
            parts.Add($"{names[i]} {banked[names[i]]}/{totals[names[i]]}");
        }
        return string.Join(";", parts);
    }

    private void Append(string row)
    {
        try
        {
            bool exists = File.Exists(path);
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                if (!exists) writer.WriteLine(Header);
                writer.WriteLine(row);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not write telemetry: {exception.Message}");
        }
    }

    private static string Number(float value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string Csv(string value)
    {
        if (value.IndexOf(',') < 0 && value.IndexOf('"') < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
