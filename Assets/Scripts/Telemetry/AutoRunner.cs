using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public class AutoRunner : MonoBehaviour
{
    private struct Job
    {
        public int team;
        public int roundIndex;
    }

    [SerializeField] private RoundController round;
    [SerializeField] private MousePlayerSource players;
    [SerializeField] private int minTeam = 2;
    [SerializeField] private int maxTeam = 6;
    [SerializeField] private int runsPerConfig = 3;
    [SerializeField] private int firstRound = 1;
    [SerializeField] private float timeScale = 6f;
    [SerializeField] private bool runOnStart;

    private readonly List<Job> jobs = new List<Job>();
    private readonly List<RoundResult> results = new List<RoundResult>();
    private bool running;
    private bool resultCounted;
    private float resultWait;
    private int jobIndex;
    private int originalTeam;

    private void Awake()
    {
        if (round == null || players == null)
        {
            Debug.LogError("AutoRunner needs a RoundController and a MousePlayerSource.", this);
            enabled = false;
            return;
        }

        round.RoundEnded += OnRoundEnded;
    }

    private void Start()
    {
        if (runOnStart) Begin();
    }

    private void OnDestroy()
    {
        if (round != null) round.RoundEnded -= OnRoundEnded;
        if (running) Time.timeScale = 1f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
        {
            if (running) Finish(false);
            else Begin();
        }

        if (!running || round.Phase != RoundPhase.Result || !resultCounted) return;

        resultWait += Time.unscaledDeltaTime;
        if (resultWait < 0.3f) return;

        NextJob();
    }

    private void Begin()
    {
        jobs.Clear();
        results.Clear();
        for (int team = minTeam; team <= maxTeam; team++)
        {
            for (int r = Mathf.Clamp(firstRound - 1, 0, round.RoundCount - 1); r < round.RoundCount; r++)
            {
                for (int run = 0; run < runsPerConfig; run++)
                {
                    jobs.Add(new Job { team = team, roundIndex = r });
                }
            }
        }

        originalTeam = round.TeamSize;
        players.SetIncludeHuman(false);
        round.RecordScores = false;
        Time.timeScale = timeScale;
        jobIndex = 0;
        running = true;
        Debug.Log($"AutoRunner started: {jobs.Count} rounds at {timeScale}x. Press F9 to stop.");
        StartJob();
    }

    private void NextJob()
    {
        jobIndex++;
        if (jobIndex >= jobs.Count)
        {
            Finish(true);
            return;
        }
        StartJob();
    }

    private void StartJob()
    {
        resultCounted = false;
        resultWait = 0f;
        Job job = jobs[jobIndex];
        round.SetTeamSize(job.team);
        round.StartRound(job.roundIndex);
    }

    private void OnRoundEnded(RoundResult result)
    {
        if (!running) return;

        results.Add(result);
        resultCounted = true;
        if ((results.Count % 10) == 0) Debug.Log($"AutoRunner: {results.Count} / {jobs.Count} rounds done");
    }

    private void Finish(bool completed)
    {
        running = false;
        Time.timeScale = 1f;
        players.SetIncludeHuman(true);
        round.RecordScores = true;
        round.SetTeamSize(originalTeam);
        Debug.Log(completed ? "AutoRunner finished." : "AutoRunner stopped.");
        WriteSummary();
    }

    private void WriteSummary()
    {
        if (results.Count == 0) return;

        SortedDictionary<int, List<RoundResult>> groups = new SortedDictionary<int, List<RoundResult>>();
        for (int i = 0; i < results.Count; i++)
        {
            int key = results[i].teamSize * 100 + results[i].roundIndex;
            if (!groups.ContainsKey(key)) groups[key] = new List<RoundResult>();
            groups[key].Add(results[i]);
        }

        StringBuilder text = new StringBuilder();
        text.AppendLine("teamSize,round,runs,avgScore,avgInPenPercent,stdInPen,minInPen,maxInPen,allInPercent,sealedPercent,avgElapsed");
        foreach (KeyValuePair<int, List<RoundResult>> group in groups)
        {
            List<RoundResult> list = group.Value;
            float score = 0f;
            float inPen = 0f;
            float elapsed = 0f;
            float minPen = float.MaxValue;
            float maxPen = float.MinValue;
            float[] pens = new float[list.Count];
            int allIn = 0;
            int sealedCount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                score += list[i].score.total;
                pens[i] = list[i].glimmersTotal > 0 ? 100f * list[i].glimmersInPen / list[i].glimmersTotal : 0f;
                inPen += pens[i];
                minPen = Mathf.Min(minPen, pens[i]);
                maxPen = Mathf.Max(maxPen, pens[i]);
                elapsed += list[i].elapsed;
                if (list[i].score.allIn) allIn++;
                if (list[i].gateSealed) sealedCount++;
            }

            int n = list.Count;
            float meanPen = inPen / n;
            float variance = 0f;
            for (int i = 0; i < n; i++)
            {
                variance += (pens[i] - meanPen) * (pens[i] - meanPen);
            }
            float stdPen = Mathf.Sqrt(variance / n);
            text.AppendLine(string.Join(",",
                list[0].teamSize,
                list[0].roundIndex + 1,
                n,
                Fixed(score / n),
                Fixed(meanPen),
                Fixed(stdPen),
                Fixed(minPen),
                Fixed(maxPen),
                Fixed(100f * allIn / n),
                Fixed(100f * sealedCount / n),
                Fixed(elapsed / n)));
        }

        string path = Path.Combine(Application.persistentDataPath, "telemetry_summary.csv");
        try
        {
            File.WriteAllText(path, text.ToString());
            Debug.Log($"Summary written: {path}\n{text}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Could not write summary: {exception.Message}");
        }
    }

    private static string Fixed(float value)
    {
        return value.ToString("F1", CultureInfo.InvariantCulture);
    }
}