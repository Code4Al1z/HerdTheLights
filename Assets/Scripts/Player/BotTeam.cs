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
    [SerializeField] private LureController lure;
    [SerializeField] private Material dotMaterial;
    [SerializeField] private float dotScale = 0.6f;
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float wobble = 0.1f;
    [SerializeField] private float retargetInterval = 0.5f;
    [SerializeField] private float commitBuffer = 1f;
    [SerializeField] private int maxPerGlimmer = 3;
    [SerializeField, Range(0.2f, 1f)] private float pushFraction = 0.6f;
    [SerializeField, Range(0f, 2f)] private float headingCorrection = 0.8f;
    [SerializeField] private float penMargin = 0.8f;
    [SerializeField] private float patience = 6f;
    [SerializeField] private float requiredProgress = 1f;
    [SerializeField] private float blacklistTime = 5f;
    [SerializeField, Range(0.3f, 1f)] private float avoidFraction = 0.9f;
    [SerializeField] private float avoidWeight = 2.5f;
    [SerializeField] private float botSpacing = 2f;
    [SerializeField] private float laneWidth = 1.6f;
    [SerializeField, Range(0.1f, 1f)] private float creepSpeed = 0.35f;
    [SerializeField] private bool useLure = true;
    [SerializeField] private int lureMinGlimmers = 2;

    private class Bot
    {
        public Transform dot;
        public Vector3 position;
        public Vector3 goal;
        public Vector3 aim;
        public GlimmerAgent target;
        public float flank;
        public float targetTime;
        public float targetStartDistance;
    }

    private struct Lane
    {
        public Vector3 from;
        public Vector3 to;
        public int owner;
        public GlimmerAgent target;
    }

    private readonly List<Bot> bots = new List<Bot>();
    private readonly List<Lane> lanes = new List<Lane>();
    private readonly HashSet<GlimmerAgent> taken = new HashSet<GlimmerAgent>();
    private readonly Dictionary<GlimmerAgent, float> blacklist = new Dictionary<GlimmerAgent, float>();
    private readonly Dictionary<GlimmerAgent, int> helpers = new Dictionary<GlimmerAgent, int>();
    private bool timeCommit;
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
        lanes.Clear();
        timeCommit = false;
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

        bool commit = playing && ShouldCommit(active);
        BuildLanes(active, playing && !commit);
        if (commit) SetSlotGoals(active);
        else SetHerdGoals(active, playing);
        if (playing && !commit) TryLure();

        for (int i = 0; i < bots.Count; i++)
        {
            bool on = i < active;
            bots[i].dot.gameObject.SetActive(on);
            if (!on) continue;

            Vector3 goal = bots[i].goal;
            Vector3 delta;
            if (commit)
            {
                delta = Vector3.MoveTowards(bots[i].position, mapper.Clamp(goal, 0.3f), moveSpeed * Time.deltaTime) - bots[i].position;
            }
            else
            {
                Vector3 noise = new Vector3(
                    Mathf.PerlinNoise(Time.time * 0.8f, i * 7.1f) - 0.5f,
                    0f,
                    Mathf.PerlinNoise(i * 3.3f, Time.time * 0.8f) - 0.5f) * 2f * wobble;
                goal = mapper.Clamp(goal + noise, 0.3f);
                delta = SteeredDelta(bots[i], i, goal, active);
            }

            Step(bots[i], delta, goal, commit);
            bots[i].dot.position = bots[i].position;
        }
    }

    private Vector3 SteeredDelta(Bot bot, int index, Vector3 goal, int active)
    {
        Vector3 toGoal = goal - bot.position;
        toGoal.y = 0f;
        float distance = toGoal.magnitude;
        Vector3 desired = distance > 0.001f ? toGoal / distance * Mathf.Clamp01(distance / 0.6f) : Vector3.zero;
        Vector3 steer = Vector3.zero;
        float slow = 1f;

        bool inFront = false;
        if (bot.target != null)
        {
            Vector3 toAim = bot.aim - bot.target.transform.position;
            toAim.y = 0f;
            Vector3 fromTarget = bot.position - bot.target.transform.position;
            fromTarget.y = 0f;
            inFront = Vector3.Dot(fromTarget, toAim) > 0f;
        }

        IReadOnlyList<GlimmerAgent> agents = round.Agents;
        for (int a = 0; agents != null && a < agents.Count; a++)
        {
            GlimmerAgent agent = agents[a];
            if (agent == null || agent.Archetype == null || round.IsBanked(agent)) continue;
            if (agent == bot.target && !inFront) continue;

            Vector3 away = bot.position - agent.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            float radius = agent.Archetype.FleeRadius * avoidFraction + 0.3f;
            if (d >= radius || d < 0.001f) continue;

            float strength = (radius - d) / radius;
            away /= d;
            Vector3 swirl = new Vector3(-away.z, 0f, away.x);
            if (Vector3.Dot(swirl, desired) < 0f) swirl = -swirl;
            steer += (away + swirl) * strength * avoidWeight;
            slow = Mathf.Min(slow, Mathf.Lerp(creepSpeed, 1f, d / radius));
        }

        for (int j = 0; j < active && j < bots.Count; j++)
        {
            if (j == index) continue;

            Vector3 away = bot.position - bots[j].position;
            away.y = 0f;
            float d = away.magnitude;
            if (d >= botSpacing || d < 0.001f) continue;

            steer += away / d * ((botSpacing - d) / botSpacing) * avoidWeight;
        }

        for (int l = 0; l < lanes.Count; l++)
        {
            if (lanes[l].owner == index || (bot.target != null && lanes[l].target == bot.target)) continue;

            Vector3 closest = ClosestOnSegment(lanes[l].from, lanes[l].to, bot.position);
            Vector3 away = bot.position - closest;
            away.y = 0f;
            float d = away.magnitude;
            if (d >= laneWidth || d < 0.001f) continue;

            steer += away / d * ((laneWidth - d) / laneWidth) * avoidWeight * 1.5f;
        }

        Vector3 velocity = desired + steer;
        if (velocity.magnitude > 1f) velocity.Normalize();
        return velocity * moveSpeed * slow * Time.deltaTime;
    }

    private void Step(Bot bot, Vector3 delta, Vector3 goal, bool allowGate)
    {
        Vector3 next = mapper.Clamp(bot.position + delta, 0.3f);
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

    private bool ShouldCommit(int active)
    {
        int total = round.GlimmersTotal;
        bool allIn = total > 0 && round.GlimmersInPen == total;
        if (allIn || timeCommit) return true;

        float travel = 0f;
        for (int i = 0; i < active && i < bots.Count; i++)
        {
            travel = Mathf.Max(travel, (bots[i].position - pen.GateCenter).magnitude / moveSpeed);
        }

        if (round.TimeLeft > round.CommitHold + commitBuffer + travel) return false;

        timeCommit = true;
        return true;
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
            float bestCost = float.MaxValue;
            for (int a = 0; a < agents.Count; a++)
            {
                GlimmerAgent agent = agents[a];
                if (!IsValidTarget(agent) || taken.Contains(agent)) continue;

                float cost = (agent.transform.position - bot.position).magnitude;
                for (int l = 0; l < lanes.Count; l++)
                {
                    if (lanes[l].owner == i) continue;
                    Vector3 closest = ClosestOnSegment(lanes[l].from, lanes[l].to, agent.transform.position);
                    if ((closest - agent.transform.position).magnitude < laneWidth) cost += 8f;
                }
                if (cost >= bestCost) continue;
                best = agent;
                bestCost = cost;
            }

            if (best == null) continue;
            taken.Add(best);
            bot.target = best;
            bot.flank = 0f;
            bot.targetTime = Time.time;
            bot.targetStartDistance = DistanceToGate(best);
        }

        helpers.Clear();
        for (int i = 0; i < active && i < bots.Count; i++)
        {
            if (bots[i].target == null) continue;
            helpers.TryGetValue(bots[i].target, out int count);
            helpers[bots[i].target] = count + 1;
        }

        for (int i = 0; i < active && i < bots.Count; i++)
        {
            Bot bot = bots[i];
            if (bot.target != null) continue;

            GlimmerAgent helped = null;
            float helpedCost = float.MaxValue;
            for (int a = 0; a < agents.Count; a++)
            {
                GlimmerAgent agent = agents[a];
                if (!IsValidTarget(agent)) continue;

                helpers.TryGetValue(agent, out int crowd);
                if (crowd >= maxPerGlimmer) continue;

                float cost = (agent.transform.position - bot.position).magnitude * 0.5f - DistanceToGate(agent) + crowd * 5f;
                if (cost >= helpedCost) continue;
                helped = agent;
                helpedCost = cost;
            }

            if (helped == null) continue;
            helpers.TryGetValue(helped, out int existing);
            helpers[helped] = existing + 1;
            bot.target = helped;
            bot.flank = FlankAngle(existing);
            bot.targetTime = Time.time;
            bot.targetStartDistance = DistanceToGate(helped);
        }
    }

    private static float FlankAngle(int existing)
    {
        if (existing <= 0) return 0f;
        return (existing % 2 == 1 ? 1f : -1f) * 35f * ((existing + 1) / 2);
    }

    private void BuildLanes(int active, bool herding)
    {
        lanes.Clear();
        for (int i = 0; i < active && i < bots.Count; i++)
        {
            Bot bot = bots[i];
            if (!herding || !IsValidTarget(bot.target)) continue;

            bot.aim = AimPoint(bot.target.transform.position);
            lanes.Add(new Lane { from = bot.target.transform.position, to = bot.aim, owner = i, target = bot.target });
        }
    }

    private Vector3 AimPoint(Vector3 position)
    {
        Vector3 relative = position - pen.GateCenter;
        float along = Vector3.Dot(relative, pen.GateAxis);
        float depth = Vector3.Dot(relative, pen.GateOutward);
        bool inFunnel = depth > 0.2f && Mathf.Abs(along) <= pen.GateLength * 0.6f + depth * 0.5f;
        return inFunnel ? pen.GateCenter - pen.GateOutward * 1.5f : pen.GateCenter + pen.GateOutward * 3.5f;
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

            bot.goal = ShiftOutOfLanes(PushGoal(target, bot), i, target);
        }
    }

    private Vector3 PushGoal(GlimmerAgent target, Bot bot)
    {
        Vector3 position = target.transform.position;
        Vector3 direction = bot.aim - position;
        direction.y = 0f;
        direction = direction.sqrMagnitude < 0.01f ? -pen.GateOutward : direction.normalized;

        Vector3 velocity = target.Velocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.09f)
        {
            Vector3 heading = velocity.normalized;
            Vector3 error = heading - direction * Vector3.Dot(heading, direction);
            Vector3 corrected = direction - error * headingCorrection;
            if (corrected.sqrMagnitude > 0.01f) direction = corrected.normalized;
        }

        direction = Quaternion.AngleAxis(bot.flank, Vector3.up) * direction;

        float push = Mathf.Max(0.8f, target.Archetype.FleeRadius * pushFraction);
        Vector3 behind = position - direction * push;
        if (!OffMap(behind)) return behind;

        Vector3 side = new Vector3(-direction.z, 0f, direction.x);
        Vector3 left = position + side * push;
        Vector3 right = position - side * push;
        bool leftOk = !OffMap(left);
        bool rightOk = !OffMap(right);
        if (leftOk && rightOk) return (left - bot.position).sqrMagnitude <= (right - bot.position).sqrMagnitude ? left : right;
        if (leftOk) return left;
        if (rightOk) return right;
        return behind;
    }

    private Vector3 ShiftOutOfLanes(Vector3 goal, int index, GlimmerAgent target)
    {
        for (int l = 0; l < lanes.Count; l++)
        {
            if (lanes[l].owner == index || lanes[l].target == target) continue;

            Vector3 closest = ClosestOnSegment(lanes[l].from, lanes[l].to, goal);
            Vector3 offset = goal - closest;
            offset.y = 0f;
            float d = offset.magnitude;
            if (d >= laneWidth) continue;

            Vector3 lane = lanes[l].to - lanes[l].from;
            Vector3 direction = d > 0.01f ? offset / d : new Vector3(-lane.z, 0f, lane.x).normalized;
            goal = closest + direction * laneWidth;
        }
        return goal;
    }

    private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        float length = ab.sqrMagnitude;
        if (length < 0.0001f) return a;

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / length);
        return a + ab * t;
    }

    private bool OffMap(Vector3 point)
    {
        return (mapper.Clamp(point, 0.3f) - point).sqrMagnitude > 0.09f;
    }

    private void TryLure()
    {
        if (!useLure || lure == null || humanPresent || !lure.Ready) return;

        IReadOnlyList<GlimmerAgent> agents = round.Agents;
        if (agents == null) return;

        Vector3 inside = pen.GateCenter - pen.GateOutward * 1.5f;
        Vector3 mouth = pen.GateCenter + pen.GateOutward * 3f;
        int nearInside = 0;
        int nearMouth = 0;
        for (int a = 0; a < agents.Count; a++)
        {
            GlimmerAgent agent = agents[a];
            if (agent == null || agent.Archetype == null || round.IsBanked(agent)) continue;

            Vector3 position = agent.transform.position;
            float depth = Vector3.Dot(position - pen.GateCenter, pen.GateOutward);
            if (depth > 0.2f && (position - inside).magnitude <= lure.Radius) nearInside++;
            if ((position - mouth).magnitude <= lure.Radius) nearMouth++;
        }

        Vector3 point;
        if (nearInside >= lureMinGlimmers) point = inside;
        else if (nearMouth >= lureMinGlimmers + 1) point = mouth;
        else return;

        Vector2 n = mapper.WorldToNormalized(point);
        lure.TryDrop(new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y)));
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