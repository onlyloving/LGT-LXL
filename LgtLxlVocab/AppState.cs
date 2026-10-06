using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LgtLxlVocab;

/// <summary>
/// 全部本地数据都放在 App 私有目录里，完全离线可用：
///   lgtlxl/profiles.json  -> 两个用户的背诵进度、每日统计、对战记录
///   lgtlxl/words.json     -> 内置六级词库的本地缓存
/// </summary>
public static class AppState
{
    public const string Password = "lgt520lxl";
    public const string Brand = "LGT-LXL";
    public const int LibraryTotal = 3991;

    public static readonly string[] UserNames = { "LGT", "LXL" };

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static readonly int[] ReviewDays = { 0, 1, 2, 4, 7, 15, 30 };

    public static List<WordEntry> Words { get; private set; } = new();
    public static Dictionary<string, WordEntry> ByWord { get; private set; } = new();
    public static Dictionary<string, UserProfile> Profiles { get; private set; } = new();
    public static string CurrentUser { get; private set; } = "LGT";
    public static bool Ready { get; private set; }

    static string Root => Path.Combine(FileSystem.AppDataDirectory, "lgtlxl");
    static string ProfilesFile => Path.Combine(Root, "profiles.json");
    static string WordsFile => Path.Combine(Root, "words.json");

    public static UserProfile Me => Profiles[CurrentUser];
    public static string OtherUser => CurrentUser == "LGT" ? "LXL" : "LGT";
    public static UserProfile Other => Profiles[OtherUser];

    public static void SetCurrentUser(string name)
    {
        if (Profiles.ContainsKey(name))
            CurrentUser = name;
    }

    public static async Task LoadAsync()
    {
        if (Ready)
            return;
        Directory.CreateDirectory(Root);

        if (File.Exists(WordsFile))
        {
            Words = JsonSerializer.Deserialize<List<WordEntry>>(await File.ReadAllTextAsync(WordsFile), Json) ?? new();
        }
        if (Words.Count == 0)
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("cet6_words.json");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            Words = JsonSerializer.Deserialize<List<WordEntry>>(text, Json) ?? new();
            try
            {
                await File.WriteAllTextAsync(WordsFile, JsonSerializer.Serialize(Words, Json));
            }
            catch
            {
                // 缓存失败不影响使用，词库已装在 App 内。
            }
        }
        ByWord = new Dictionary<string, WordEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in Words)
            ByWord[w.Word] = w;

        if (File.Exists(ProfilesFile))
        {
            try
            {
                Profiles = JsonSerializer.Deserialize<Dictionary<string, UserProfile>>(
                    await File.ReadAllTextAsync(ProfilesFile), Json) ?? new();
            }
            catch
            {
                Profiles = new();
            }
        }
        foreach (var name in UserNames)
        {
            if (!Profiles.ContainsKey(name))
                Profiles[name] = new UserProfile
                {
                    Name = name,
                    Emoji = name == "LGT" ? "🐻" : "🐰",
                    Color = name == "LGT" ? "#7C6BFF" : "#FF7AA2",
                };
        }
        Ready = true;
    }

    public static async Task SaveAsync()
    {
        Directory.CreateDirectory(Root);
        var tmp = ProfilesFile + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(Profiles, Json));
        File.Copy(tmp, ProfilesFile, true);
        try
        {
            File.Delete(tmp);
        }
        catch
        {
        }
    }

    // ---------- 时间工具 ----------
    public static string TodayKey => DateTime.Now.ToString("yyyy-MM-dd");

    public static DayStat Today(UserProfile p)
    {
        if (!p.Days.TryGetValue(TodayKey, out var d))
        {
            d = new DayStat();
            p.Days[TodayKey] = d;
        }
        return d;
    }

    public static int Streak(UserProfile p)
    {
        var n = 0;
        var day = DateTime.Now.Date;
        for (var i = 0; i < 400; i++)
        {
            var key = day.ToString("yyyy-MM-dd");
            if (p.Days.TryGetValue(key, out var d) && (d.NewWords > 0 || d.Reviews > 0))
            {
                n++;
                day = day.AddDays(-1);
            }
            else if (i == 0)
            {
                day = day.AddDays(-1);   // 今天还没背，不算断
            }
            else
            {
                break;
            }
        }
        return n;
    }

    // ---------- 出题 ----------
    public static Question MakeQuestion(WordEntry word, IList<WordEntry> pool)
    {
        var opts = new List<string> { word.Translation };
        var tries = 0;
        while (opts.Count < 4 && tries < 600 && pool.Count > 1)
        {
            tries++;
            var cand = pool[Random.Shared.Next(pool.Count)];
            if (string.IsNullOrWhiteSpace(cand.Translation))
                continue;
            if (string.Equals(cand.Word, word.Word, StringComparison.OrdinalIgnoreCase))
                continue;
            if (opts.Contains(cand.Translation))
                continue;
            opts.Add(cand.Translation);
        }
        while (opts.Count < 4)
            opts.Add("（无）");
        var correct = opts[0];
        for (var i = opts.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (opts[i], opts[j]) = (opts[j], opts[i]);
        }
        var answer = opts.IndexOf(correct);
        return new Question
        {
            Word = word.Word,
            Phonetic = word.Phonetic,
            Translation = word.Translation,
            Example = word.Example,
            ExampleCn = word.ExampleCn,
            Options = opts,
            Answer = answer < 0 ? 0 : answer,
        };
    }

    /// <summary>生成一组要背的单词：先复习到期的，再补新词。</summary>
    public static List<Question> BuildStudySet(UserProfile p, int count)
    {
        var now = DateTime.Now;
        var due = p.Progress.Values
            .Where(x => x.NextReview <= now)
            .OrderBy(x => x.NextReview)
            .ThenBy(x => x.Box)
            .Select(x => x.Word)
            .Where(ByWord.ContainsKey)
            .Take(count)
            .ToList();

        var picked = new HashSet<string>(due, StringComparer.OrdinalIgnoreCase);
        if (due.Count < count)
        {
            foreach (var w in Words)
            {
                if (picked.Count >= count)
                    break;
                if (p.Progress.ContainsKey(w.Word))
                    continue;
                picked.Add(w.Word);
                due.Add(w.Word);
            }
        }
        var result = new List<Question>();
        foreach (var key in due)
            result.Add(MakeQuestion(ByWord[key], Words));
        return result;
    }

    /// <summary>从两人已学单词里抽题用于对战。</summary>
    public static List<Question> BuildBattleSet(IEnumerable<string> learned, int count, IList<WordEntry> pool)
    {
        var keys = learned
            .Where(ByWord.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Shuffle(keys);
        var result = new List<Question>();
        foreach (var key in keys.Take(count))
            result.Add(MakeQuestion(ByWord[key], pool));
        return result;
    }

    public static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>两人已学单词的合集（去重，只保留词库里有的）。</summary>
    public static List<string> LearnedUnion()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in UserNames)
            foreach (var key in Profiles[name].Progress.Keys)
                if (ByWord.ContainsKey(key))
                    set.Add(key);
        return set.ToList();
    }

    // ---------- 学习结果写入 ----------
    public static async Task ApplyStudyAsync(UserProfile p, Question q, bool correct, bool isNew)
    {
        var key = q.Word;
        if (!p.Progress.TryGetValue(key, out var wp))
        {
            wp = new WordProgress
            {
                Word = q.Word,
                Box = 1,
                FirstSeen = DateTime.Now,
                NextReview = DateTime.Now.AddDays(1),
            };
            p.Progress[key] = wp;
        }
        if (correct)
        {
            wp.Right++;
            wp.Box = Math.Min(6, Math.Max(1, wp.Box) + 1);
        }
        else
        {
            wp.Wrong++;
            wp.Box = 1;
        }
        wp.LastReview = DateTime.Now;
        wp.NextReview = DateTime.Now.AddDays(ReviewDays[Math.Clamp(wp.Box, 1, 6)]);

        var day = Today(p);
        if (isNew)
            day.NewWords++;
        else
            day.Reviews++;
        await SaveAsync();
    }

    public static async Task RecordBattleAsync(string mode, string opponent, int wordCount, int myScore, int opponentScore)
        => await RecordBattleFor(CurrentUser, mode, opponent, wordCount, myScore, opponentScore);

    /// <summary>把一局对战结果记到指定用户名下。</summary>
    public static async Task RecordBattleFor(string user, string mode, string opponent, int wordCount, int myScore, int opponentScore)
    {
        if (!Profiles.TryGetValue(user, out var p))
            return;
        var result = myScore > opponentScore ? "胜" : myScore < opponentScore ? "负" : "平";
        p.Battles.Insert(0, new BattleRecord
        {
            Time = DateTime.Now,
            Mode = mode,
            Me = p.Name,
            Opponent = opponent,
            WordCount = wordCount,
            MyScore = myScore,
            OpponentScore = opponentScore,
            Result = result,
        });
        if (p.Battles.Count > 200)
            p.Battles.RemoveRange(200, p.Battles.Count - 200);
        Today(p).Battles++;
        await SaveAsync();
    }
}
