using UnityEngine;

public class GlimmerAgent : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const float HardMargin = 0.2f;
    private const float WallPush = 2f;

    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private MeshFilter meshFilter;

    private ArchetypeDefinition archetype;
    private PlayfieldMapper mapper;
    private IPlayerSource source;
    private SwarmSpawner swarm;
    private MaterialPropertyBlock block;
    private Vector3 velocity;
    private float wanderAngle;
    private float panicTimer;

    public ArchetypeDefinition Archetype => archetype;
    public Vector3 Velocity => velocity;
    public float PanicTimer => panicTimer;
    public bool Panicking => panicTimer > 0f;

    private void Awake()
    {
        if (glowRenderer == null) glowRenderer = GetComponentInChildren<Renderer>();
        if (meshFilter == null) meshFilter = GetComponentInChildren<MeshFilter>();
    }

    public void Initialize(ArchetypeDefinition definition, PlayfieldMapper fieldMapper, IPlayerSource playerSource, SwarmSpawner owner)
    {
        archetype = definition;
        mapper = fieldMapper;
        source = playerSource;
        swarm = owner;
        wanderAngle = Random.value * Mathf.PI * 2f;
        ApplyLook();
    }

    private void ApplyLook()
    {
        transform.localScale = Vector3.one * archetype.Scale;
        if (meshFilter != null && archetype.Mesh != null) meshFilter.sharedMesh = archetype.Mesh;
        if (glowRenderer == null) return;

        if (block == null) block = new MaterialPropertyBlock();
        glowRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, archetype.Color);
        block.SetColor(EmissionColorId, archetype.Color * archetype.EmissionIntensity);
        glowRenderer.SetPropertyBlock(block);
    }

    private void Update()
    {
        if (archetype == null || mapper == null || swarm == null) return;

        float dt = Time.deltaTime;
        panicTimer = Mathf.Max(0f, panicTimer - dt);
        Vector3 position = transform.position;

        Vector3 flee = ComputeFlee(position);
        Vector3 lure = ComputeLure(position);
        ComputeFlock(position, out Vector3 cohesion, out Vector3 separation, out Vector3 panicAway);

        wanderAngle += (Random.value - 0.5f) * 2f * archetype.WanderJitter * dt;
        Vector3 wander = new Vector3(Mathf.Cos(wanderAngle), 0f, Mathf.Sin(wanderAngle));
        bool lured = lure.sqrMagnitude > 0f;
        Vector3 edge = archetype.EdgeDriftWeight > 0f && !lured ? mapper.NearestEdgeDirection(position) : Vector3.zero;
        Vector3 wall = mapper.InwardPush(position, archetype.WallMargin);

        float boost = Panicking ? archetype.PanicSpeedBoost : 1f;
        float panicScale = archetype.PanicDuration > 0f ? Mathf.Clamp01(panicTimer / archetype.PanicDuration) : 0f;

        Vector3 cruise = cohesion * archetype.CohesionWeight
            + separation * archetype.SeparationWeight
            + wander * archetype.WanderWeight
            + edge * archetype.EdgeDriftWeight
            + wall * WallPush;

        Vector3 desired = flee * (archetype.FleeStrength * archetype.FleeSpeed * boost)
            + panicAway * (archetype.FleeSpeed * boost * panicScale)
            + cruise * archetype.CruiseSpeed
            + lure;

        desired = Vector3.ClampMagnitude(desired, archetype.MaxSpeed * boost);
        velocity = Vector3.MoveTowards(velocity, desired, archetype.Acceleration * dt);

        Vector3 next = mapper.Clamp(position + velocity * dt, HardMargin);
        Vector3 blocked = Vector3.zero;
        if (swarm.Pen != null) next = swarm.Pen.Constrain(position, next, out blocked);
        if (blocked != Vector3.zero) velocity -= blocked * Vector3.Dot(velocity, blocked);

        transform.position = next;
    }

    private Vector3 ComputeFlee(Vector3 position)
    {
        Vector3 sum = Vector3.zero;
        if (source == null) return sum;

        for (int i = 0; i < source.Count; i++)
        {
            Vector3 playerPosition = mapper.NormalizedToWorld(source.GetNormalizedPosition(i));
            Vector3 offset = position - playerPosition;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance >= archetype.FleeRadius) continue;

            Vector3 direction = distance > 0.0001f ? offset / distance : Vector3.right;
            sum += direction * (1f - distance / archetype.FleeRadius);
            if (archetype.PanicDuration > 0f) panicTimer = Mathf.Max(panicTimer, archetype.PanicDuration);
        }

        return sum;
    }

    private Vector3 ComputeLure(Vector3 position)
    {
        LureController lure = swarm.Lure;
        if (lure == null || !lure.Active || archetype.LureSpeed <= 0f) return Vector3.zero;

        Vector3 offset = lure.Position - position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance > lure.Radius || distance < 0.0001f) return Vector3.zero;

        float arrive = Mathf.Clamp01(distance - archetype.SeparationRadius);
        return offset / distance * (arrive * archetype.LureSpeed);
    }

    private void ComputeFlock(Vector3 position, out Vector3 cohesion, out Vector3 separation, out Vector3 panicAway)
    {
        Vector3 centroid = Vector3.zero;
        int neighbours = 0;
        separation = Vector3.zero;
        panicAway = Vector3.zero;
        bool spreadsPanic = archetype.PanicSpreadRadius > 0f;

        var agents = swarm.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            GlimmerAgent other = agents[i];
            if (other == this) continue;

            Vector3 offset = position - other.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < 0.0001f) continue;

            if (distance < archetype.NeighborRadius)
            {
                centroid += other.transform.position;
                neighbours++;
            }

            if (distance < archetype.SeparationRadius)
                separation += offset / distance * (1f - distance / archetype.SeparationRadius);

            if (spreadsPanic && other.Panicking && distance < archetype.PanicSpreadRadius)
            {
                float inherited = other.PanicTimer * archetype.PanicSpreadDecay;
                if (inherited > panicTimer) panicTimer = inherited;
                panicAway += offset / distance;
            }
        }

        cohesion = Vector3.zero;
        if (neighbours > 0)
        {
            Vector3 toCentre = centroid / neighbours - position;
            toCentre.y = 0f;
            cohesion = Vector3.ClampMagnitude(toCentre / archetype.NeighborRadius, 1f);
        }

        if (panicAway.sqrMagnitude > 0f) panicAway.Normalize();
    }
}