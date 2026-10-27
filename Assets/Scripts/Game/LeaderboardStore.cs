using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LeaderboardStore
{
    [Serializable]
    public class Entry
    {
        public int score;
        public float elapsed;
        public string date;
    }

    [Serializable]
    public class Board
    {
        public int teamSize;
        public int roundIndex;
        public List<Entry> entries = new List<Entry>();
    }

    [Serializable]
    public class Data
    {
        public List<Board> boards = new List<Board>();
    }

    public const int TotalBoard = -1;
    private const int MaxEntries = 10;

    private readonly string path;
    private Data data = new Data();

    public string Path => path;

    public LeaderboardStore(string fileName = "leaderboard.json")
    {
        path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        Load();
    }

    public IReadOnlyList<Entry> GetTop(int teamSize, int roundIndex)
    {
        Board board = FindBoard(teamSize, roundIndex, false);
        return board != null ? board.entries : new List<Entry>();
    }

    public int Submit(int teamSize, int roundIndex, int score, float elapsed)
    {
        Board board = FindBoard(teamSize, roundIndex, true);
        Entry entry = new Entry { score = score, elapsed = elapsed, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        board.entries.Add(entry);
        board.entries.Sort(Compare);

        int index = board.entries.IndexOf(entry);
        if (board.entries.Count > MaxEntries) board.entries.RemoveRange(MaxEntries, board.entries.Count - MaxEntries);

        Save();
        return index >= 0 && index < MaxEntries ? index + 1 : 0;
    }

    public void Clear()
    {
        data = new Data();
        Save();
    }

    private static int Compare(Entry a, Entry b)
    {
        int byScore = b.score.CompareTo(a.score);
        return byScore != 0 ? byScore : a.elapsed.CompareTo(b.elapsed);
    }

    private Board FindBoard(int teamSize, int roundIndex, bool create)
    {
        for (int i = 0; i < data.boards.Count; i++)
        {
            if (data.boards[i].teamSize == teamSize && data.boards[i].roundIndex == roundIndex) return data.boards[i];
        }

        if (!create) return null;
        Board board = new Board { teamSize = teamSize, roundIndex = roundIndex };
        data.boards.Add(board);
        return board;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(path)) return;
            Data loaded = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            if (loaded != null && loaded.boards != null) data = loaded;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read leaderboard: {exception.Message}");
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not save leaderboard: {exception.Message}");
        }
    }
}