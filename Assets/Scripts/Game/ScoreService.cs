using System.Collections.Generic;
using UnityEngine;

public struct ScoreBreakdown
{
    public int basePoints;
    public int maxBasePoints;
    public int sealBonus;
    public int sweepBonus;
    public int timeBonus;
    public int total;
    public char grade;
    public bool allIn;
}

public class ScoreService : MonoBehaviour
{
    [SerializeField] private int sealBonusPercent = 20;
    [SerializeField] private int sweepBonusPercent = 20;
    [SerializeField] private int timeBonusPerSecond = 1;
    [SerializeField] private int parTimePercent = 25;
    [SerializeField] private int gradeS = 95;
    [SerializeField] private int gradeA = 80;
    [SerializeField] private int gradeB = 60;
    [SerializeField] private int gradeC = 40;

    public ScoreBreakdown Evaluate(IReadOnlyList<GlimmerAgent> agents, PenZone pen, bool gateSealed, bool endedEarly, int secondsLeft, int roundSeconds)
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
        if (gateSealed) result.sealBonus = Percent(result.basePoints, sealBonusPercent);
        if (gateSealed && result.allIn) result.sweepBonus = Percent(result.basePoints, sweepBonusPercent);
        if (gateSealed && endedEarly) result.timeBonus = Mathf.Max(0, secondsLeft) * timeBonusPerSecond;

        result.total = result.basePoints + result.sealBonus + result.sweepBonus + result.timeBonus;
        result.grade = GradeFor(result.total, result.maxBasePoints, roundSeconds);
        return result;
    }

    private static int Percent(int value, int percent)
    {
        return (value * percent + 50) / 100;
    }

    private char GradeFor(int total, int maxBasePoints, int roundSeconds)
    {
        int par = Percent(maxBasePoints, 100 + sealBonusPercent + sweepBonusPercent) + timeBonusPerSecond * roundSeconds * parTimePercent / 100;
        if (par <= 0) return 'D';

        int ratio = total * 100 / par;
        if (ratio >= gradeS) return 'S';
        if (ratio >= gradeA) return 'A';
        if (ratio >= gradeB) return 'B';
        if (ratio >= gradeC) return 'C';
        return 'D';
    }
}