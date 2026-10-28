using System.Collections.Generic;
using UnityEngine;

public class BotTeam : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private RoundController round;
    [SerializeField] private PenZone pen;
    [SerializeField] private GateSlots gate;
    [SerializeField] private PlayfieldMapper mapper;
    [SerializeField] private Material dotMaterial;
    [SerializeField] private float dotScale = 0.6f;
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float wobble = 0.15f;
    [SerializeField] private float retargetInterval = 0.5f;
    [SerializeField] private float sealLead = 10f;
    [SerializeField, Range(0.2f, 1f)] private float pushFraction = 0.6f;
    [SerializeField] private float penMargin = 0.8f;
    [SerializeField] private float patience = 6f;
    [SerializeField] private float requiredProgress = 1f;
    [SerializeField] private float blacklistTime = 5f;

    private class Bot
    {
        public Transform dot;
        public Vector3 position;
        public Vector3 goal;
        public GlimmerAgent target;
        public float targetTime;
        public float targetStartDistance;
    }

    private readonly List<Bot> bots = new List<Bot>();
    private readonly HashSet<GlimmerAgent> taken = new HashSet<GlimmerAgent>();
    private readonly Dictionary<GlimmerAgent, float> blacklist = new Dictionary<GlimmerAgent, float>();
    private bool humanPresent;
    private Vector3 humanWorld;
    private float retargetTimer;

    public int Count
    {
        get
        {
            int teamSize = round != null ? round.TeamSize : 1;
            return Mathf.Max(0, teamSize - (humanPresent ? 1 : 0));
        }
    }

    public void SetHuman(bool present, Vector3 world)
    {
        humanPresent = present;
        humanWorld = world;
    }

    public Vector2 GetNormalizedPosition(int index)
    {
        if (mapper == null || index < 0 || index >= bots.Count) return new Vector2(0.5f, 0.5f);

        Vector2 n = mapper.WorldToNormalized(bots[index].position);
        return new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y));
    }

    private void Awake()
    {
        if (round == null || pen == null || gate == null || mapper == null)
        {
            Debug.LogError("BotTeam needs a round, pen, gate and playfield mapper.", this);
            enabled = false;
            return;
        }

        round.PhaseChanged += OnPhaseChanged;
    }

    private void OnDestroy()
    {
        if (round != null) round.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(RoundPhase phase)
    {
        if (phase != RoundPhase.Intro) return;

        EnsureBots();
        blacklist.Clear();
        for (int i = 0; i < bots.Count; i++)
        {
            bots[i].position = StartPosition(i);
            bots[i].goal = bots[i].position;
            bots[i].target = null;
        }
    }

    private void Update()
    {
        EnsureBots();

        RoundPhase phase = round.Phase;
        bool playing = phase == RoundPhase.Herd || phase == RoundPhase.Seal;
        int active = Count;

        if (playing)
        {
            retargetTimer -= Time.deltaTime;
            if (retargetTimer <= 0f)
            {
                retargetTimer = retargetInterval;
                Assign(active);
            }
        }

        bool commit = playing && ShouldCommit();
        if (commit) SetSlotGoals(active);
        else SetHerdGoals(active, playing);

        for (int i = 0; i < bots.Count; i++)
        {
            bool on = i < active;
            bots[i].dot.gameObject.SetActive(on);
            if (!on) continue;

            Vector3 noise = new Vector3(
                Mathf.PerlinNoise(Time.time * 0.8f, i * 7.1f) - 0.5f,
                0f,
                Mathf.PerlinNoise(i * 3.3f, Time.time * 0.8f) - 0.5f) * 2f * wobble;
            Vector3 goal = mapper.Clamp(bots[i].goal + noise, 0.3f);
            Step(bots[i], goal, commit);
            bots[i].dot.position = bots[i].position;
        }
    }

    private void Step(Bot bot, Vector3 goal, bool allowGate)
    {
        Vector3 next = Vector3.MoveTowards(bot.position, goal, moveSpeed * Time.deltaTime);
        next.y = mapper.Center.y;

        if (!allowGate && !pen.Contains(bot.position, penMargin) && pen.Contains(next, penMargin))
        {
            Vector3 xOnly = new Vector3(next.x, next.y, bot.position.z);
            Vector3 zOnly = new Vector3(bot.position.x, next.y, next.z);
            bool xOk = !pen.Contains(xOnly, penMargin);
            bool zOk = !pen.Contains(zOnly, penMargin);
            if (xOk && zOk) next = (goal - xOnly).sqrMagnitude <= (goal - zOnly).sqrMagnitude ? xOnly : zOnly;
            else if (xOk) next = xOnly;
            else if (zOk) next = zOnly;
            else next = bot.position;
        }

        bot.position = next;
    }

    private bool ShouldCommit()
    {
        int total = round.GlimmersTotal;
        bool allIn = total > 0 && round.GlimmersInPen == total;
        return allIn || round.TimeLeft <= sealLead;
    }

    private bool IsValidTarget(GlimmerAgent agent)
    {
        if (agent == null || agent.Archetype == null || round.IsBanked(agent)) return false;
        return !(blacklist.TryGetValue(agent, out float until) && Time.time < until);
    }

    private float DistanceToGate(GlimmerAgent agent)
    {
        Vector3 offset = agent.transform.position - pen.GateCenter;
        offset.y = 0f;
        return offset.magnitude;
    }

    private void Assign(int active)
    {
        taken.Clear();
        IReadOnlyList<GlimmerAgent> agents = round.Agents;

        for (int i = 0; i < bots.Count; i++)
        {
            Bot bot = bots[i];
            if (i >= active)
            {
                bot.target = null;
                continue;
            }
            if (bot.target == null) continue;

            if (!IsValidTarget(bot.target))
            {
                bot.target = null;
                continue;
            }

            if (Time.time - bot.targetTime > patience)
            {
                float distance = DistanceToGate(bot.target);
                if (bot.targetStartDistance - distance < requiredProgress)
                {
                    blacklist[bot.target] = Time.time + blacklistTime;
                    bot.target = null;
                    continue;
                }
                bot.targetTime = Time.time;
                bot.targetStartDistance = distance;
            }

            taken.Add(bot.target);
        }

        if (agents == null) return;

        for (int i = 0; i < active && i < bots.Count; i++)
        {
            Bot bot = bots[i];
            if (bot.target != null) continue;

            GlimmerAgent best = null;
            float bestDistance = float.MaxValue;
            for (int a = 0; a < agents.Count; a++)
            {
                GlimmerAgent agent = agents[a];
                if (!IsValidTarget(agent) || taken.Contains(agent)) continue;

                float distance = (agent.transform.position - bot.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = agent;
                bestDistance = distance;
            }

            if (best == null) continue;
            taken.Add(best);
            bot.target = best;
            bot.targetTime = Time.time;
            bot.targetStartDistance = DistanceToGate(best);
        }
    }

    private void SetHerdGoals(int active, bool playing)
    {
        for (int i = 0; i < active && i < bots.Count; i++)
        {
            Bot bot = bots[i];
            GlimmerAgent target = bot.target;
            if (!playing || !IsValidTarget(target))
            {
                bot.goal = WaitingSpot(i);
                continue;
            }

            bot.goal = PushGoal(target, bot.position);
        }
    }

    private Vector3 PushGoal(GlimmerAgent target, Vector3 botPosition)
    {
        Vector3 position = target.transform.position;
        Vector3 relative = position - pen.GateCenter;
        float along = Vector3.Dot(relative, pen.GateAxis);
        float depth = Vector3.Dot(relative, pen.GateOutward);
        bool inFunnel = depth > 0.2f && Mathf.Abs(along) <= pen.GateLength * 0.6f + depth * 0.5f;
        Vector3 aim = inFunnel ? pen.GateCenter - pen.GateOutward * 1.5f : pen.GateCenter + pen.GateOutward * 3.5f;

        Vector3 direction = aim - position;
        direction.y = 0f;
        direction = direction.sqrMagnitude < 0.01f ? -pen.GateOutward : direction.normalized;

        float push = Mathf.Max(0.8f, target.Archetype.FleeRadius * pushFraction);
        Vector3 behind = position - direction * push;
        if (!OffMap(behind)) return behind;

        Vector3 side = new Vector3(-direction.z, 0f, direction.x);
        Vector3 left = position + side * push;
        Vector3 right = position - side * push;
        bool leftOk = !OffMap(left);
        bool rightOk = !OffMap(right);
        if (leftOk && rightOk) return (left - botPosition).sqrMagnitude <= (right - botPosition).sqrMagnitude ? left : right;
        if (leftOk) return left;
        if (rightOk) return right;
        return behind;
    }

    private bool OffMap(Vector3 point)
    {
        return (mapper.Clamp(point, 0.3f) - point).sqrMagnitude > 0.09f;
    }

    private void SetSlotGoals(int active)
    {
        int slots = Mathf.Max(1, gate.SlotCount);
        float width = pen.GateLength / slots;
        int humanSlot = -1;
        if (humanPresent)
        {
            float along = Vector3.Dot(humanWorld - pen.GateCenter, pen.GateAxis);
            humanSlot = Mathf.Clamp(Mathf.FloorToInt((along + pen.GateLength * 0.5f) / width), 0, slots - 1);
        }

        List<int> free = new List<int>();
        for (int s = 0; s < slots; s++)
        {
            if (s != humanSlot) free.Add(s);
        }

        List<int> order = new List<int>();
        for (int i = 0; i < active && i < bots.Count; i++)
        {
            order.Add(i);
        }
        order.Sort((a, b) => Vector3.Dot(bots[a].position - pen.GateCenter, pen.GateAxis).CompareTo(Vector3.Dot(bots[b].position - pen.GateCenter, pen.GateAxis)));

        for (int k = 0; k < order.Count; k++)
        {
            Bot bot = bots[order[k]];
            if (k >= free.Count)
            {
                bot.goal = WaitingSpot(order[k]);
                continue;
            }

            float offset = (free[k] + 0.5f) * width - pen.GateLength * 0.5f;
            bot.goal = pen.GateCenter + pen.GateAxis * offset + pen.GateOutward * 0.4f;
        }
    }

    private Vector3 WaitingSpot(int index)
    {
        float side = index % 2 == 0 ? 1f : -1f;
        float lateral = pen.GateLength * 0.5f + 3f + (index / 2) * 1.2f;
        return pen.GateCenter + pen.GateOutward * 3f + pen.GateAxis * (side * lateral);
    }

    private Vector3 StartPosition(int index)
    {
        int count = Mathf.Max(1, Count);
        float x = Mathf.Lerp(-0.3f, 0.3f, (index + 0.5f) / count) * mapper.Size.x;
        Vector3 start = new Vector3(mapper.Center.x + x, mapper.Center.y, mapper.Center.z - mapper.Size.y * 0.35f);
        if (pen.Contains(start, penMargin)) start.z = pen.Center.z - pen.Size.y * 0.5f - penMargin - 0.2f;
        return mapper.Clamp(start, 0.3f);
    }

    private void EnsureBots()
    {
        int needed = Count;
        while (bots.Count < needed)
        {
            bots.Add(CreateBot(bots.Count));
        }
    }

    private Bot CreateBot(int index)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.name = $"Bot {index + 1}";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * dotScale;

        Renderer renderer = sphere.GetComponent<Renderer>();
        if (dotMaterial != null) renderer.sharedMaterial = dotMaterial;
        Color color = Color.HSVToRGB((index * 0.17f + 0.08f) % 1f, 0.7f, 1f);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, color);
        block.SetColor(EmissionColorId, color * 2f);
        renderer.SetPropertyBlock(block);

        Bot bot = new Bot { dot = sphere.transform, position = StartPosition(index) };
        bot.goal = bot.position;
        return bot;
    }
}