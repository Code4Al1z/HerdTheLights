using System.Collections.Generic;
using UnityEngine;

public struct ScoreBreakdown
{
    public int basePoints;
    public int maxBasePoints;
    public int sealBonus;
    public int timeBonus;
    public int total;
    public char grade;
    public bool allIn;
}

public class ScoreService : MonoBehaviour
{
    [SerializeField] private float sealBonusFraction = 0.2f;
    [SerializeField] private float timeBonusPerSecond = 1f;
    [SerializeField] private float parTimeFraction = 0.25f;
    [SerializeField] private float gradeS = 0.95f;
    [SerializeField] private float gradeA = 0.8f;
    [SerializeField] private float gradeB = 0.6f;
    [SerializeField] private float gradeC = 0.4f;

    public ScoreBreakdown Evaluate(IReadOnlyList<GlimmerAgent> agents, PenZone pen, bool gateSealed, float timeLeft, float roundDuration)
    {
        ScoreBreakdown result = new ScoreBreakdown();
        int inPen = 0;

        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] == null || agents[i].Archetype == null) continue;

            int multiplier = agents[i].Archetype.Multiplier;
            result.maxBasePoints += multiplier;
            if (!pen.Contains(agents[i].transform.position)) continue;

            result.basePoints += multiplier;
            inPen++;
        }

        result.allIn = agents.Count > 0 && inPen == agents.Count;
        if (result.allIn && gateSealed) result.sealBonus = Mathf.RoundToInt(result.basePoints * sealBonusFraction);
        if (result.allIn && gateSealed && timeLeft > 0f) result.timeBonus = Mathf.FloorToInt(timeLeft * timeBonusPerSecond);

        result.total = result.basePoints + result.sealBonus + result.timeBonus;
        result.grade = GradeFor(result.total, result.maxBasePoints, roundDuration);
        return result;
    }

    private char GradeFor(int total, int maxBasePoints, float roundDuration)
    {
        float par = maxBasePoints * (1f + sealBonusFraction) + timeBonusPerSecond * roundDuration * parTimeFraction;
        if (par <= 0f) return 'D';

        float ratio = total / par;
        if (ratio >= gradeS) return 'S';
        if (ratio >= gradeA) return 'A';
        if (ratio >= gradeB) return 'B';
        if (ratio >= gradeC) return 'C';
        return 'D';
    }
}
