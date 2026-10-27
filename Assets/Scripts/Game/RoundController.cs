using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RoundPhase { Ready, Intro, Herd, Seal, Result }

public struct RoundResult
{
    public int teamSize;
    public int roundIndex;
    public int glimmersTotal;
    public int glimmersInPen;
    public bool gateSealed;
    public float timeLeft;
    public float elapsed;
    public ScoreBreakdown score;
    public int rank;
    public int roundCount;
    public int runTotal;
    public float runElapsed;
    public bool runComplete;
    public int runRank;
}

public class RoundController : MonoBehaviour
{
    [SerializeField] private PenZone pen;
    [SerializeField] private GateSlots gate;
    [SerializeField] private SwarmSpawner swarm;
    [SerializeField] private ScoreService scoreService;
    [SerializeField] private TeamSizeConfig teamConfig;
    [SerializeField] private List<RoundDefinition> rounds = new List<RoundDefinition>();
    [SerializeField] private int teamSize = 1;
    [SerializeField] private float introDuration = 3f;
    [SerializeField] private float commitHold = 3f;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool showDebug = true;

    private RoundPhase phase = RoundPhase.Ready;
    private RoundDefinition round;
    private TeamSizeConfig.Entry currentTeam;
    private int roundIndex;
    private float roundDuration;
    private float sealWindow;
    private float introTimeLeft;
    private float timeLeft;
    private float elapsed;
    private float sealedTimer;
    private ScoreBreakdown liveScore;
    private RoundResult result;
    private LeaderboardStore leaderboard;
    private int[] runScores = new int[0];
    private float[] runElapsed = new float[0];
    private bool[] runPlayed = new bool[0];
    private readonly HashSet<GlimmerAgent> banked = new HashSet<GlimmerAgent>();
    private GUIStyle style;
    private GUIStyle countdownStyle;

    public event Action<RoundPhase> PhaseChanged;
    public event Action<RoundResult> RoundEnded;
    public event Action<GlimmerAgent> GlimmerBanked;
    public event Action<GlimmerAgent> GlimmerLeaked;

    public RoundPhase Phase => phase;
    public RoundDefinition CurrentRound => round;
    public int RoundNumber => roundIndex + 1;
    public int RoundCount => rounds.Count;
    public bool IsLastRound => roundIndex == rounds.Count - 1;
    public int TeamSize => teamSize;
    public float RoundDuration => roundDuration;
    public float SealWindow => sealWindow;
    public float TimeLeft => timeLeft;
    public float IntroTimeLeft => Mathf.Max(0f, introTimeLeft);
    public float CommitHold => commitHold;
    public float SealedProgress => commitHold > 0f ? Mathf.Clamp01(sealedTimer / commitHold) : 0f;
    public bool GateSealed => gate != null && gate.AllFilled;
    public int GlimmersInPen => banked.Count;
    public IReadOnlyList<GlimmerAgent> Agents => swarm != null ? swarm.Agents : null;
    public int GlimmersTotal => swarm != null ? swarm.Agents.Count : 0;
    public ScoreBreakdown LiveScore => liveScore;
    public RoundResult Result => result;
    public LeaderboardStore Leaderboard => leaderboard;

    private void Awake()
    {
        leaderboard = new LeaderboardStore();
        Debug.Log($"Leaderboard file: {leaderboard.Path}");
    }

    private void Start()
    {
        bool missing = pen == null || gate == null || swarm == null || scoreService == null || teamConfig == null;
        if (missing || rounds.Count == 0 || rounds[0] == null)
        {
            Debug.LogError("RoundController needs a pen, gate, swarm, score service, team size config and at least one round.", this);
            enabled = false;
            return;
        }

        if (autoStart) StartRound();
    }

    public bool IsBanked(GlimmerAgent agent)
    {
        return agent != null && banked.Contains(agent);
    }

    public void SetTeamSize(int size)
    {
        teamSize = Mathf.Max(1, size);
    }

    public void StartRound()
    {
        round = rounds[roundIndex];
        if (roundIndex == 0 || runScores.Length != rounds.Count) ResetRun();
        currentTeam = teamConfig.Get(teamSize);

        swarm.Clear();
        banked.Clear();
        liveScore = new ScoreBreakdown();

        pen.SetGateWidth(currentTeam.slotCount * currentTeam.slotSpacing * round.GateSpacingMultiplier);
        gate.SetSlotCount(currentTeam.slotCount);
        gate.SetArmed(false);

        roundDuration = round.RoundDuration;
        sealWindow = round.SealWindow;
        timeLeft = roundDuration;
        elapsed = 0f;
        sealedTimer = 0f;
        introTimeLeft = introDuration;
        SetPhase(RoundPhase.Intro);
    }

    private void ResetRun()
    {
        runScores = new int[rounds.Count];
        runElapsed = new float[rounds.Count];
        runPlayed = new bool[rounds.Count];
    }

    private void BeginHerd()
    {
        swarm.Spawn(round.BuildSpawnList(currentTeam.glimmerCount));
        SetPhase(RoundPhase.Herd);
    }

    private void Update()
    {
        if (phase == RoundPhase.Result)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.rKey.wasPressedThisFrame) StartRound();
            if (keyboard.nKey.wasPressedThisFrame)
            {
                roundIndex = (roundIndex + 1) % rounds.Count;
                StartRound();
            }
            return;
        }

        if (phase == RoundPhase.Ready) return;

        if (phase == RoundPhase.Intro)
        {
            introTimeLeft -= Time.deltaTime;
            if (introTimeLeft <= 0f) BeginHerd();
            return;
        }

        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        elapsed += Time.deltaTime;

        int total = swarm.Agents.Count;
        TrackBanking();
        bool allIn = total > 0 && banked.Count == total;
        liveScore = scoreService.Evaluate(swarm.Agents, pen, gate.AllFilled, true, Mathf.FloorToInt(timeLeft), Mathf.RoundToInt(roundDuration));

        if (phase == RoundPhase.Herd && (allIn || timeLeft <= sealWindow))
        {
            gate.SetArmed(true);
            SetPhase(RoundPhase.Seal);
        }

        if (phase == RoundPhase.Seal)
        {
            sealedTimer = gate.AllFilled ? sealedTimer + Time.deltaTime : 0f;
            bool committed = gate.AllFilled && (allIn || sealedTimer >= commitHold);
            if (committed || timeLeft <= 0f) EndRound(total);
        }
    }

    private void TrackBanking()
    {
        IReadOnlyList<GlimmerAgent> agents = swarm.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            GlimmerAgent agent = agents[i];
            if (agent == null) continue;

            bool inside = pen.Contains(agent.transform.position);
            bool known = banked.Contains(agent);
            if (inside && !known)
            {
                banked.Add(agent);
                GlimmerBanked?.Invoke(agent);
            }
            else if (!inside && known)
            {
                banked.Remove(agent);
                GlimmerLeaked?.Invoke(agent);
            }
        }
    }

    private void EndRound(int total)
    {
        ScoreBreakdown breakdown = scoreService.Evaluate(swarm.Agents, pen, gate.AllFilled, timeLeft > 0f, Mathf.FloorToInt(timeLeft), Mathf.RoundToInt(roundDuration));
        int rank = leaderboard.Submit(teamSize, roundIndex, breakdown.total, elapsed);

        runScores[roundIndex] = breakdown.total;
        runElapsed[roundIndex] = elapsed;
        runPlayed[roundIndex] = true;
        int runTotal = 0;
        float runTime = 0f;
        bool runComplete = true;
        for (int i = 0; i < runScores.Length; i++)
        {
            runTotal += runScores[i];
            runTime += runElapsed[i];
            if (!runPlayed[i]) runComplete = false;
        }
        int runRank = runComplete ? leaderboard.Submit(teamSize, LeaderboardStore.TotalBoard, runTotal, runTime) : 0;

        liveScore = breakdown;
        result = new RoundResult
        {
            teamSize = teamSize,
            roundIndex = roundIndex,
            glimmersTotal = total,
            glimmersInPen = pen.CountInside(swarm.Agents),
            gateSealed = gate.AllFilled,
            timeLeft = timeLeft,
            elapsed = elapsed,
            score = breakdown,
            rank = rank,
            roundCount = rounds.Count,
            runTotal = runTotal,
            runElapsed = runTime,
            runComplete = runComplete,
            runRank = runRank
        };
        SetPhase(RoundPhase.Result);
        RoundEnded?.Invoke(result);
    }

    private void SetPhase(RoundPhase next)
    {
        phase = next;
        PhaseChanged?.Invoke(next);
    }

    private void OnGUI()
    {
        if (!showDebug || swarm == null || pen == null || gate == null || round == null) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        if (countdownStyle == null)
        {
            countdownStyle = new GUIStyle(GUI.skin.label) { fontSize = 180, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            countdownStyle.normal.textColor = Color.white;
        }

        if (phase == RoundPhase.Intro)
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), Mathf.CeilToInt(introTimeLeft).ToString(), countdownStyle);

        string text = $"Round {RoundNumber}: {round.DisplayName} (team of {teamSize})\nPhase: {phase}";
        if (phase == RoundPhase.Intro) text += $"\nStarting in {Mathf.CeilToInt(introTimeLeft)}";
        text += $"\nTime left: {timeLeft:F1}s\nIn pen: {GlimmersInPen} / {GlimmersTotal}\nScore now: {liveScore.total}\nSlots filled: {gate.FilledCount} / {gate.SlotCount}";

        if (phase == RoundPhase.Seal && gate.AllFilled)
            text += $"\nSealed, holding {Mathf.Min(sealedTimer, commitHold):F1} / {commitHold:F0}s";

        LureController lure = swarm.Lure;
        if (lure != null)
            text += lure.Ready ? "\nLure: ready" : $"\nLure: {lure.CooldownRemaining:F1}s";

        if (phase == RoundPhase.Result)
        {
            ScoreBreakdown s = result.score;
            text += $"\nScore: {s.total} (grade {s.grade}) = {s.basePoints} + seal {s.sealBonus} + sweep {s.sweepBonus} + time {s.timeBonus}";
            text += $"\nAll in: {(s.allIn ? "yes" : "no")}, gate sealed: {(result.gateSealed ? "yes" : "no")} (seal needs a closed gate, sweep needs everything in)";
            text += result.rank > 0 ? $"\nRank: #{result.rank}" : "\nRank: outside the top 10";

            text += result.runComplete ? $"\nFinal total: {result.runTotal}" : $"\nRun total so far: {result.runTotal}";
            if (result.runComplete) text += result.runRank > 0 ? $"  (final rank #{result.runRank})" : "  (outside the final top 10)";

            IReadOnlyList<LeaderboardStore.Entry> top = leaderboard.GetTop(teamSize, roundIndex);
            for (int i = 0; i < top.Count && i < 5; i++)
            {
                text += $"\n  {i + 1}. {top[i].score}  ({top[i].elapsed:F0}s)";
            }
            text += IsLastRound ? "\nR: replay round   N: new run" : "\nR: replay round   N: next round";
        }

        GUI.Label(new Rect(16f, 12f, 1000f, 700f), text, style);
    }
}