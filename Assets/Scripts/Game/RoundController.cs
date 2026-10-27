using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RoundPhase { Ready, Herd, Seal, Result }

public struct RoundResult
{
    public int glimmersTotal;
    public int glimmersInPen;
    public bool gateSealed;
    public float timeLeft;
    public float elapsed;
}

public class RoundController : MonoBehaviour
{
    [SerializeField] private PenZone pen;
    [SerializeField] private GateSlots gate;
    [SerializeField] private SwarmSpawner swarm;
    [SerializeField] private float roundDuration = 70f;
    [SerializeField] private float sealWindow = 20f;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool showDebug = true;

    private RoundPhase phase = RoundPhase.Ready;
    private float timeLeft;
    private float elapsed;
    private RoundResult result;
    private GUIStyle style;

    public event Action<RoundPhase> PhaseChanged;
    public event Action<RoundResult> RoundEnded;

    public RoundPhase Phase => phase;
    public float TimeLeft => timeLeft;
    public float SealWindow => sealWindow;
    public RoundResult Result => result;

    private void Start()
    {
        if (pen == null || gate == null || swarm == null)
        {
            Debug.LogError("RoundController needs a pen, a gate and a swarm spawner.", this);
            enabled = false;
            return;
        }

        if (autoStart) StartRound();
    }

    public void StartRound()
    {
        swarm.Spawn();
        gate.SetArmed(false);
        timeLeft = roundDuration;
        elapsed = 0f;
        SetPhase(RoundPhase.Herd);
    }

    private void Update()
    {
        if (phase == RoundPhase.Result)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) StartRound();
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

        if (phase == RoundPhase.Seal && ((allIn && gate.AllFilled) || timeLeft <= 0f))
        {
            EndRound(total);
        }
    }

    private void EndRound(int total)
    {
        result = new RoundResult
        {
            glimmersTotal = total,
            glimmersInPen = pen.CountInside(swarm.Agents),
            gateSealed = gate.AllFilled,
            timeLeft = timeLeft,
            elapsed = elapsed
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
        if (!showDebug || swarm == null || pen == null || gate == null) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 22 };

        string text = $"Phase: {phase}\nTime left: {timeLeft:F1}s\nIn pen: {pen.CountInside(swarm.Agents)} / {swarm.Agents.Count}\nSlots filled: {gate.FilledCount} / {gate.SlotCount}";

        LureController lure = swarm.Lure;
        if (lure != null)
            text += lure.Ready ? "\nLure: ready" : $"\nLure: {lure.CooldownRemaining:F1}s";

        if (phase == RoundPhase.Result)
            text += $"\nResult: {result.glimmersInPen} / {result.glimmersTotal} in pen, sealed: {(result.gateSealed ? "yes" : "no")}, {result.timeLeft:F1}s left\nPress R to restart";

        GUI.Label(new Rect(16f, 12f, 1000f, 320f), text, style);
    }
}