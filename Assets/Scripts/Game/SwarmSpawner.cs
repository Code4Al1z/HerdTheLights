using System;
using System.Collections.Generic;
using UnityEngine;

public class SwarmSpawner : MonoBehaviour
{
    [Serializable]
    public struct SpawnEntry
    {
        public ArchetypeDefinition archetype;
        public int count;
    }

    [SerializeField] private GlimmerAgent glimmerPrefab;
    [SerializeField] private PlayfieldMapper mapper;
    [SerializeField] private MonoBehaviour playerSourceBehaviour;
    [SerializeField] private PenZone pen;
    [SerializeField] private LureController lure;
    [SerializeField] private List<SpawnEntry> entries = new List<SpawnEntry>();
    [SerializeField] private int maxSwarm = 40;
    [SerializeField] private float spawnMargin = 2f;
    [SerializeField] private float penClearance = 1f;
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<GlimmerAgent> agents = new List<GlimmerAgent>();

    public IReadOnlyList<GlimmerAgent> Agents => agents;
    public PenZone Pen => pen;
    public LureController Lure => lure;

    private void Start()
    {
        if (spawnOnStart) Spawn();
    }

    public void Spawn()
    {
        Spawn(entries);
    }

    public void Spawn(IReadOnlyList<SpawnEntry> list)
    {
        Clear();
        if (glimmerPrefab == null || mapper == null)
        {
            Debug.LogWarning("SwarmSpawner needs a glimmer prefab and a playfield mapper.");
            return;
        }

        IPlayerSource source = playerSourceBehaviour as IPlayerSource;
        if (source == null) Debug.LogWarning("SwarmSpawner has no player source, so glimmers will not flee.", this);
        Vector2 half = mapper.Size * 0.5f - new Vector2(spawnMargin, spawnMargin);

        for (int e = 0; e < list.Count; e++)
        {
            SpawnEntry entry = list[e];
            if (entry.archetype == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                if (agents.Count >= maxSwarm) return;

                Vector3 position = PickSpawnPoint(half);
                GlimmerAgent agent = Instantiate(glimmerPrefab, position, Quaternion.identity, transform);
                agent.Initialize(entry.archetype, mapper, source, this);
                agents.Add(agent);
            }
        }
    }

    public void Clear()
    {
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null) Destroy(agents[i].gameObject);
        }
        agents.Clear();
    }

    private Vector3 PickSpawnPoint(Vector2 half)
    {
        Vector3 point = mapper.Center;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            point = mapper.Center + new Vector3(
                UnityEngine.Random.Range(-half.x, half.x),
                0f,
                UnityEngine.Random.Range(-half.y, half.y));
            if (pen == null || !pen.Contains(point, penClearance)) break;
        }
        return point;
    }
}