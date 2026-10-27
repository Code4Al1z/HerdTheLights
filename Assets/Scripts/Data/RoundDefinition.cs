using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Round", menuName = "Herd the Lights/Round")]
public class RoundDefinition : ScriptableObject
{
    [Serializable]
    public struct MixEntry
    {
        public ArchetypeDefinition archetype;
        public float share;
    }

    [field: SerializeField] public string DisplayName { get; private set; } = "Round";
    [field: SerializeField] public float RoundDuration { get; private set; } = 70f;
    [field: SerializeField] public float SealWindow { get; private set; } = 20f;
    [field: SerializeField] public float GateSpacingMultiplier { get; private set; } = 1f;

    [SerializeField] private List<MixEntry> mix = new List<MixEntry>();

    public List<SpawnEntry> BuildSpawnList(int total)
    {
        List<SpawnEntry> result = new List<SpawnEntry>();
        float sum = 0f;
        for (int i = 0; i < mix.Count; i++)
        {
            if (mix[i].archetype != null && mix[i].share > 0f) sum += mix[i].share;
        }
        if (sum <= 0f || total <= 0) return result;

        int[] counts = new int[mix.Count];
        float[] fractions = new float[mix.Count];
        int assigned = 0;
        for (int i = 0; i < mix.Count; i++)
        {
            fractions[i] = -2f;
            if (mix[i].archetype == null || mix[i].share <= 0f) continue;

            float raw = total * mix[i].share / sum;
            counts[i] = Mathf.FloorToInt(raw);
            fractions[i] = raw - counts[i];
            assigned += counts[i];
        }

        int remaining = total - assigned;
        while (remaining > 0)
        {
            int best = -1;
            float bestFraction = -1f;
            for (int i = 0; i < mix.Count; i++)
            {
                if (fractions[i] <= bestFraction) continue;
                best = i;
                bestFraction = fractions[i];
            }
            if (best < 0) break;

            counts[best]++;
            fractions[best] = -2f;
            remaining--;
        }

        for (int i = 0; i < mix.Count; i++)
        {
            if (counts[i] > 0) result.Add(new SpawnEntry { archetype = mix[i].archetype, count = counts[i] });
        }
        return result;
    }
}
