using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RoundPhase { Ready, Herd, Seal, Result }

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
    [SerializeField] private float commitHold = 3f;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool showDebug = true;

    private RoundPhase phase = RoundPhase.Ready;
    private RoundDefinition round;
    private int roundIndex;
    private float roundDuration;
    private float sealWindow;
    private float timeLeft;
    private float elapsed;
    private float sealedTimer;
    private RoundResult result;
    private LeaderboardStore leaderboard;
    private GUIStyle style;

    public event Action<RoundPhase> PhaseChanged;
    public event Action<RoundResult> RoundEnded;

    public RoundPhase Phase => phase;
    public float TimeLeft => timeLeft;
    public float SealWindow => sealWindow;
    public RoundResult Result => result;
    public int TeamSize => teamSize;
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

    public void SetTeamSize(int size)
    {
        teamSize = Mathf.Max(1, size);
    }

    public void StartRound()
    {
        round = rounds[roundIndex];
        TeamSizeConfig.Entry team = teamConfig.Get(teamSize);

        pen.SetGateWidth(team.slotCount * team.slotSpacing * round.GateSpacingMultiplier);
        gate.SetSlotCount(team.slotCount);
        gate.SetArmed(false);
        swarm.Spawn(round.BuildSpawnList(team.glimmerCount));

        roundDuration = round.RoundDuration;
        sealWindow = round.SealWindow;
        timeLeft = roundDuration;
        elapsed = 0f;
        sealedTimer = 0f;
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

        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        elapsed += Time.deltaTime;

        int total = swarm.Agents.Count;
        bool allIn = total > 0 && pen.CountInside(swarm.Agents) == total;

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

    private void EndRound(int total)
    {
        ScoreBreakdown breakdown = scoreService.Evaluate(swarm.Agents, pen, gate.AllFilled, timeLeft > 0f, Mathf.FloorToInt(timeLeft), Mathf.RoundToInt(roundDuration));
        int rank = leaderboard.Submit(teamSize, breakdown.total, elapsed);

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
            rank = rank
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

        string text = $"Round {roundIndex + 1}: {round.DisplayName} (team of {teamSize})\nPhase: {phase}\nTime left: {timeLeft:F1}s\nIn pen: {pen.CountInside(swarm.Agents)} / {swarm.Agents.Count}\nSlots filled: {gate.FilledCount} / {gate.SlotCount}";

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

            IReadOnlyList<LeaderboardStore.Entry> top = leaderboard.GetTop(teamSize);
            for (int i = 0; i < top.Count && i < 5; i++)
            {
                text += $"\n  {i + 1}. {top[i].score}  ({top[i].elapsed:F0}s)";
            }
            text += "\nR: replay   N: next round";
        }

        GUI.Label(new Rect(16f, 12f, 1000f, 640f), text, style);
    }
}