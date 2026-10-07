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
        try
        {
            Directory.CreateDirectory(Root);
        }
        catch (Exception ex)
        {
            CrashReporter.Record(ex, "AppState.CreateDirectory");
        }

        try
        {
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
        }
        catch (Exception ex)
        {
            // 词库读失败也不能让 App 挂掉，先记录错误。
            CrashReporter.Record(ex, "AppState.LoadWords");
            Words = new List<WordEntry>();
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
    public static Question MakeQuestion(WordEntry word, IList<WordEntry> pool, QuizMode mode = QuizMode.EnToCn)
    {
        // 英语找中文：题干是英文，选项是中文释义
        // 中文找英语：题干是中文释义，选项是英文单词
        var cnToEn = mode == QuizMode.CnToEn;
        string Text(WordEntry w) => cnToEn ? w.Word : w.Translation;

        var opts = new List<string> { Text(word) };
        var tries = 0;
        while (opts.Count < 4 && tries < 600 && pool.Count > 1)
        {
            tries++;
            var cand = pool[Random.Shared.Next(pool.Count)];
            var text = Text(cand);
            if (string.IsNullOrWhiteSpace(text))
                continue;
            if (string.Equals(cand.Word, word.Word, StringComparison.OrdinalIgnoreCase))
                continue;
            if (opts.Contains(text))
                continue;
            opts.Add(text);
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
            Mode = mode,
        };
    }

    /// <summary>挑一组要背的单词：先复习到期的，再补新词。</summary>
    public static List<WordEntry> BuildStudyWords(UserProfile p, int count)
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
        var result = new List<WordEntry>();
        foreach (var key in due)
            result.Add(ByWord[key]);
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

    // ---------- 复习 ----------

    /// <summary>今天有多少词到期需要复习。</summary>
    public static int DueCount(UserProfile p)
    {
        var now = DateTime.Now;
        var n = 0;
        foreach (var wp in p.Progress.Values)
            if (wp.NextReview <= now && ByWord.ContainsKey(wp.Word))
                n++;
        return n;
    }

    /// <summary>复习队列：优先到期的词；一个都没到期就随机复习已学单词。</summary>
    public static List<WordEntry> BuildReviewWords(UserProfile p, int count, out bool fromSchedule)
    {
        var now = DateTime.Now;
        var keys = p.Progress.Values
            .Where(x => x.NextReview <= now)
            .OrderBy(x => x.NextReview)
            .Select(x => x.Word)
            .Where(ByWord.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();
        fromSchedule = keys.Count > 0;
        if (keys.Count == 0)
        {
            keys = p.Progress.Values
                .Select(x => x.Word)
                .Where(ByWord.ContainsKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => Random.Shared.Next())
                .Take(count)
                .ToList();
        }
        return keys.Select(k => ByWord[k]).ToList();
    }

    // ---------- 两台手机之间同步进度 ----------

    public static string ExportProfiles() => JsonSerializer.Serialize(Profiles, Json);

    /// <summary>把对方手机上的进度合并进来，返回更新条数。</summary>
    public static async Task<int> MergeProfilesAsync(string json)
    {
        var changed = 0;
        try
        {
            var incoming = JsonSerializer.Deserialize<Dictionary<string, UserProfile>>(json, Json);
            if (incoming == null)
                return 0;
            foreach (var pair in incoming)
            {
                var name = pair.Key;
                var other = pair.Value;
                if (!Profiles.TryGetValue(name, out var mine))
                {
                    Profiles[name] = other;
                    changed++;
                    continue;
                }
                if (other.Progress == null)
                    continue;

                // 同一个用户的单词进度：以复习时间更近的为准
                foreach (var wp in other.Progress.Values)
                {
                    if (!mine.Progress.TryGetValue(wp.Word, out var cur))
                    {
                        mine.Progress[wp.Word] = wp;
                        changed++;
                        continue;
                    }
                    if (wp.LastReview > cur.LastReview ||
                        (wp.LastReview == cur.LastReview && wp.Box > cur.Box))
                    {
                        mine.Progress[wp.Word] = wp;
                        changed++;
                    }
                }

                // 每日统计取两边较大的
                if (other.Days != null)
                {
                    foreach (var day in other.Days)
                    {
                        if (!mine.Days.TryGetValue(day.Key, out var cur))
                        {
                            mine.Days[day.Key] = day.Value;
                            continue;
                        }
                        cur.NewWords = Math.Max(cur.NewWords, day.Value.NewWords);
                        cur.Reviews = Math.Max(cur.Reviews, day.Value.Reviews);
                        cur.Battles = Math.Max(cur.Battles, day.Value.Battles);
                    }
                }

                // 对战记录去重合并
                if (other.Battles != null)
                {
                    foreach (var b in other.Battles)
                    {
                        var dup = mine.Battles.Any(x =>
                            x.Time == b.Time && x.Mode == b.Mode && x.Opponent == b.Opponent &&
                            x.MyScore == b.MyScore && x.OpponentScore == b.OpponentScore);
                        if (!dup)
                            mine.Battles.Add(b);
                    }
                    mine.Battles.Sort((a, b) => b.Time.CompareTo(a.Time));
                    if (mine.Battles.Count > 200)
                        mine.Battles.RemoveRange(200, mine.Battles.Count - 200);
                }
            }
            await SaveAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Record(ex, "MergeProfiles");
        }
        return changed;
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
