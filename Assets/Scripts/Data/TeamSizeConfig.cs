using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TeamSizeConfig", menuName = "Herd the Lights/Team Size Config")]
public class TeamSizeConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public int teamSize;
        public int glimmerCount;
        public int slotCount;
        public float slotSpacing;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>
    {
        new Entry { teamSize = 1, glimmerCount = 10, slotCount = 1, slotSpacing = 2f },
        new Entry { teamSize = 2, glimmerCount = 12, slotCount = 2, slotSpacing = 1.5f },
        new Entry { teamSize = 3, glimmerCount = 16, slotCount = 3, slotSpacing = 1.3f },
        new Entry { teamSize = 4, glimmerCount = 20, slotCount = 4, slotSpacing = 1.2f },
        new Entry { teamSize = 5, glimmerCount = 25, slotCount = 5, slotSpacing = 1.1f },
        new Entry { teamSize = 6, glimmerCount = 30, slotCount = 6, slotSpacing = 1f }
    };

    public Entry Get(int teamSize)
    {
        if (entries.Count == 0)
        {
            return new Entry
            {
                teamSize = teamSize,
                glimmerCount = 12,
                slotCount = Mathf.Max(1, teamSize),
                slotSpacing = 1.2f
            };
        }

        Entry best = entries[0];
        int bestGap = Mathf.Abs(best.teamSize - teamSize);
        for (int i = 1; i < entries.Count; i++)
        {
            int gap = Mathf.Abs(entries[i].teamSize - teamSize);
            if (gap >= bestGap) continue;
            best = entries[i];
            bestGap = gap;
        }
        return best;
    }
}
