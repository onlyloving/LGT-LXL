using System.Text.Json.Serialization;

namespace LgtLxlVocab;

/// <summary>一条单词数据（内置六级词库中的一项）。</summary>
public class WordEntry
{
    public string Word { get; set; } = "";
    public string Phonetic { get; set; } = "";
    public string Translation { get; set; } = "";
    public string Example { get; set; } = "";
    public string ExampleCn { get; set; } = "";
}

/// <summary>某个用户对某个单词的背诵进度。</summary>
public class WordProgress
{
    public string Word { get; set; } = "";
    public int Box { get; set; }          // 掌握等级 1-6
    public int Right { get; set; }
    public int Wrong { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastReview { get; set; }
    public DateTime NextReview { get; set; }

    [JsonIgnore]
    public bool Mastered => Box >= 4;
}

public class DayStat
{
    public int NewWords { get; set; }
    public int Reviews { get; set; }
    public int Battles { get; set; }
}

public class BattleRecord
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Mode { get; set; } = "";        // 本机 / 联机
    public string Me { get; set; } = "";
    public string Opponent { get; set; } = "";
    public int WordCount { get; set; }
    public int MyScore { get; set; }
    public int OpponentScore { get; set; }
    public string Result { get; set; } = "";      // 胜 / 负 / 平
}

public class UserProfile
{
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "🐱";
    public string Color { get; set; } = "#7C6BFF";
    public int DailyGoal { get; set; } = 20;
    public Dictionary<string, WordProgress> Progress { get; set; } = new();
    public Dictionary<string, DayStat> Days { get; set; } = new();
    public List<BattleRecord> Battles { get; set; } = new();

    [JsonIgnore]
    public int LearnedCount => Progress.Count;

    [JsonIgnore]
    public int MasteredCount
    {
        get
        {
            var n = 0;
            foreach (var p in Progress.Values)
                if (p.Mastered)
                    n++;
            return n;
        }
    }
}

/// <summary>一道选择题（背单词与对战共用）。</summary>
public class Question
{
    public string Word { get; set; } = "";
    public string Phonetic { get; set; } = "";
    public string Translation { get; set; } = "";
    public string Example { get; set; } = "";
    public string ExampleCn { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public int Answer { get; set; }
}

/// <summary>联机对战在网络上传输的消息。</summary>
public class NetMessage
{
    public string T { get; set; } = "";
    public string? Name { get; set; }
    public int Count { get; set; }
    public int Index { get; set; }
    public int Score { get; set; }
    public string? Text { get; set; }
    public List<string>? Learned { get; set; }
    public List<Question>? Words { get; set; }
}
