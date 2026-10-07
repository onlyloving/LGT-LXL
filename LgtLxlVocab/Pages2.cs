namespace LgtLxlVocab;

/// <summary>
/// 背单词：每个词要过两关才算真正背过。
///   第 1 关：英语 → 中文（看单词选意思）
///   第 2 关：中文 → 英语（看意思选单词）
/// 任何一关答错，这个词稍后都会重新出现，直到答对为止。
/// </summary>
public class StudyPage : ContentPage
{
    class StudyTask
    {
        public WordEntry Word = new();
        public QuizMode Mode = QuizMode.EnToCn;
    }

    const int SessionSize = 20;

    readonly bool _review;
    bool _reviewFromSchedule;

    readonly VerticalStackLayout _body = new() { Spacing = 12 };
    readonly VerticalStackLayout _page = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    readonly ProgressBar _bar = new() { ProgressColor = Ui.Accent, BackgroundColor = Ui.CardSoft };
    readonly Label _counter = Ui.Lbl("", 13, Ui.Dim);
    readonly Label _score = Ui.Lbl("", 13, Ui.Dim);
    readonly Label _mode = Ui.Lbl("", 12, Ui.Accent);
    readonly List<Border> _optionRows = new();

    readonly List<StudyTask> _queue = new();
    readonly HashSet<string> _stage1 = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _passed = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _learnedBefore = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> _misses = new(StringComparer.OrdinalIgnoreCase);

    int _totalWords;
    int _right;
    int _wrong;
    bool _answered;
    bool _started;
    StudyTask? _cur;
    Question? _curQ;

    public StudyPage(bool review = false)
    {
        _review = review;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;

        _page.Children.Add(_mode);
        _page.Children.Add(_bar);
        _page.Children.Add(Ui.Row(12, _counter, _score));
        _page.Children.Add(_body);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        grid.Add(Ui.Header(review ? "复习" : "背单词", this, true, "结束", () => _ = Navigation.PopAsync()), 0, 0);
        grid.Add(new ScrollView { Content = _page }, 0, 1);
        Content = grid;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_started)
            return;
        _started = true;
        StartSession();
    }

    void StartSession()
    {
        var profile = AppState.Me;
        var words = _review
            ? AppState.BuildReviewWords(profile, SessionSize, out _reviewFromSchedule)
            : AppState.BuildStudyWords(profile, SessionSize);
        _queue.Clear();
        _stage1.Clear();
        _passed.Clear();
        _misses.Clear();
        _learnedBefore.Clear();
        foreach (var key in AppState.Me.Progress.Keys)
            _learnedBefore.Add(key);
        foreach (var w in words)
        {
            var box = profile.Progress.TryGetValue(w.Word, out var wp) ? wp.Box : 1;
            if (_review && box >= 3)
            {
                // 已经比较熟的词，复习只考「中文 → 英语」这一关（更难的那一关）
                _stage1.Add(w.Word);
                _queue.Add(new StudyTask { Word = w, Mode = QuizMode.CnToEn });
            }
            else
            {
                _queue.Add(new StudyTask { Word = w, Mode = QuizMode.EnToCn });
            }
        }
        _totalWords = words.Count;
        _right = 0;
        _wrong = 0;
        _cur = null;
        _curQ = null;
        if (_totalWords == 0)
        {
            ShowEmpty();
            return;
        }
        ShowNext();
    }

    void UpdateHeader()
    {
        _bar.Progress = _totalWords == 0 ? 0 : (double)_passed.Count / _totalWords;
        _counter.Text = $"已过关 {_passed.Count} / {_totalWords} 词";
        _score.Text = $"✅ {_right}   ❌ {_wrong}";
    }

    void ShowEmpty()
    {
        _body.Children.Clear();
        _body.Children.Add(Ui.Panel(Ui.Stack(8,
            _review
                ? Ui.Lbl("还没有可复习的单词", 16, Ui.Fg, true)
                : Ui.Lbl("词库里暂时没有可学的单词", 16, Ui.Fg, true),
            _review
                ? Ui.Lbl("先去「背单词」学一些，之后这里就会按 1/2/4/7/15/30 天的节奏提醒你复习。", 13, Ui.Dim)
                : Ui.Lbl("稍后再来看看，或者去「复习」里翻翻已学的词。", 13, Ui.Dim))));
    }

    /// <summary>把任务插回队列（delay 道题之后再考）。</summary>
    void Enqueue(StudyTask task, int delay)
    {
        var idx = Math.Min(Math.Max(delay, 0), _queue.Count);
        _queue.Insert(idx, task);
    }

    void ShowNext()
    {
        if (_queue.Count == 0)
        {
            Finish();
            return;
        }
        _cur = _queue[0];
        _queue.RemoveAt(0);
        ShowQuestion();
    }

    void ShowQuestion()
    {
        _answered = false;
        _optionRows.Clear();
        _body.Children.Clear();
        var w = _cur!.Word;
        _curQ = AppState.MakeQuestion(w, AppState.Words, _cur.Mode);
        var q = _curQ;
        UpdateHeader();
        var prefix = _review
            ? (_reviewFromSchedule ? "🔁 复习（到期词）· " : "🔁 复习（随机抽已学词）· ")
            : "";
        _mode.Text = prefix + (q.IsCnToEn ? "② 中文 → 英语 · 看意思选出对应单词" : "① 英语 → 中文 · 看单词选出正确意思");

        var head = new List<View>();
        if (q.IsCnToEn)
        {
            head.Add(Ui.Lbl(w.Translation, 22, Ui.Fg, true));
            head.Add(Ui.Lbl("这个词的英文是？", 13, Ui.Dim));
        }
        else
        {
            head.Add(Ui.Lbl(w.Word, 34, Ui.Fg, true));
            head.Add(Ui.Lbl(w.Phonetic, 14, Ui.Dim));
        }
        head.Add(Ui.Lbl(Hint(w), 12, Ui.Dim));
        _body.Children.Add(Ui.Panel(Ui.Stack(8, head.ToArray()), Ui.Card, 18, 20));

        for (var i = 0; i < q.Options.Count; i++)
        {
            var idx = i;
            var (row, _) = Ui.TapRow($"{Letter(i)}. {q.Options[i]}", () => OnOption(idx));
            _optionRows.Add(row);
            _body.Children.Add(row);
        }
    }

    string Hint(WordEntry w)
    {
        if (_cur!.Mode == QuizMode.CnToEn)
            return _stage1.Contains(w.Word)
                ? "英语→中文已经过关，这一关答对就算真正背过 ✅"
                : "中文→英语";
        if (_misses.TryGetValue(w.Word, out var n) && n > 0)
            return $"这个词已经错过 {n} 次，答对才能过关 💪";
        return "答对之后还会再考一次「中文 → 英语」";
    }

    static string Letter(int i) => new[] { "A", "B", "C", "D" }[Math.Clamp(i, 0, 3)];

    async void OnOption(int index)
    {
        if (_answered || _cur == null || _curQ == null)
            return;
        _answered = true;
        var q = _curQ;
        var correct = index == q.Answer;
        var word = _cur.Word.Word;

        for (var i = 0; i < _optionRows.Count && i < q.Options.Count; i++)
        {
            var color = i == q.Answer ? Ui.Good : i == index ? Ui.Bad : Ui.Dim;
            _optionRows[i].BackgroundColor = (i == q.Answer ? Ui.Good : i == index ? Ui.Bad : Ui.CardSoft).WithAlpha(0.22f);
            if (_optionRows[i].Content is Label lab)
                lab.TextColor = color;
        }

        string tip;
        if (correct)
        {
            _right++;
            if (_cur.Mode == QuizMode.EnToCn)
            {
                _stage1.Add(word);
                Enqueue(new StudyTask { Word = _cur.Word, Mode = QuizMode.CnToEn }, 2);
                tip = "✅ 第 1 关过了！2 题之后考你「中文 → 英语」";
            }
            else
            {
                _passed.Add(word);
                tip = "🎉 两关都过了，这个词才算真正背过！";
                await AppState.ApplyStudyAsync(AppState.Me, q, true, !_learnedBefore.Contains(word));
            }
        }
        else
        {
            _wrong++;
            _misses[word] = (_misses.TryGetValue(word, out var n) ? n : 0) + 1;
            Enqueue(new StudyTask { Word = _cur.Word, Mode = _cur.Mode }, 3);
            tip = "❌ 答错了，这个词稍后会再出现，直到答对为止";
        }
        UpdateHeader();

        var detail = Ui.Stack(8,
            Ui.Lbl(tip, 15, correct ? Ui.Good : Ui.Bad, true),
            Ui.Lbl(_cur.Word.Word, 22, Ui.Fg, true),
            Ui.Lbl(_cur.Word.Phonetic, 13, Ui.Dim),
            Ui.Lbl(_cur.Word.Translation, 15, Ui.Fg));
        if (!string.IsNullOrWhiteSpace(_cur.Word.Example))
        {
            detail.Children.Add(Ui.Lbl(_cur.Word.Example, 13, Ui.Dim));
            if (!string.IsNullOrWhiteSpace(_cur.Word.ExampleCn))
                detail.Children.Add(Ui.Lbl(_cur.Word.ExampleCn, 13, Ui.Dim));
        }
        var nextText = _queue.Count == 0 && _passed.Count >= _totalWords ? "全部过关 ✓" : "继续 →";
        detail.Children.Add(Ui.Btn(nextText, Ui.Accent, Colors.White, ShowNext, 16));
        _body.Children.Add(Ui.Panel(detail, Ui.Bg2));
    }

    void Finish()
    {
        _bar.Progress = 1;
        _counter.Text = $"全部过关 · {_passed.Count} 词";
        _mode.Text = "🎉 本轮结束";
        _body.Children.Clear();
        var p = AppState.Me;
        var box = Ui.Stack(10,
            Ui.Lbl(_review ? $"复习完成，{p.Name}！" : $"太棒了，{p.Name}！", 22, Ui.Fg, true),
            Ui.Lbl(_review
                ? $"本轮复习了 {_totalWords} 个词，答对的词下次复习时间会往后推，答错的下次会更快出现。"
                : $"本轮 {_totalWords} 个词都过了「英语→中文」和「中文→英语」两关，才算真正背过。", 14, Ui.Dim),
            Ui.StatRow(
                ("过关", _passed.Count.ToString(), Ui.Good),
                ("答对", _right.ToString(), Ui.Accent),
                ("答错", _wrong.ToString(), Ui.Bad)),
            Ui.StatRow(
                ("已学", p.LearnedCount.ToString(), Ui.Accent),
                ("掌握", p.MasteredCount.ToString(), Ui.Good),
                ("连续", AppState.Streak(p) + "天", Ui.Accent2)),
            Ui.Btn(_review ? "再复习一组" : "再来一组 20 词", Ui.Accent, Colors.White, StartSession, 16),
            Ui.Btn("返回首页", Ui.CardSoft, Ui.Fg, () => _ = Navigation.PopAsync(), 16));
        _body.Children.Add(Ui.Panel(box, Ui.Card, 18, 20));
    }
}

/// <summary>学习进度（两人对比 + 已学单词）。</summary>
public class ProgressPage : ContentPage
{
    readonly VerticalStackLayout _root = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };

    public ProgressPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        grid.Add(Ui.Header("学习进度", this), 0, 0);
        grid.Add(new ScrollView { Content = _root }, 0, 1);
        Content = grid;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _root.Children.Clear();
        var lgt = AppState.Profiles["LGT"];
        var lxl = AppState.Profiles["LXL"];
        var max = Math.Max(1, Math.Max(lgt.LearnedCount, lxl.LearnedCount));

        _root.Children.Add(Ui.Panel(Ui.Stack(8,
            Ui.Lbl($"内置六级词库 {AppState.LibraryTotal} 词", 16, Ui.Fg, true),
            Ui.Lbl($"LGT 已学 {lgt.LearnedCount} 词 · LXL 已学 {lxl.LearnedCount} 词", 13, Ui.Dim))));

        foreach (var p in new[] { lgt, lxl })
            _root.Children.Add(UserCard(p, max));

        _root.Children.Add(Ui.Btn("查看已学单词", Ui.Accent, Colors.White,
            () => _ = Navigation.PushAsync(new WordListPage(true)), 16));
        _root.Children.Add(Ui.Btn("浏览全部词库", Ui.CardSoft, Ui.Fg,
            () => _ = Navigation.PushAsync(new WordListPage(false)), 16));
    }

    static View UserCard(UserProfile p, int max)
    {
        var today = AppState.Today(p);
        var wins = p.Battles.Count(b => b.Result == "胜");
        var loses = p.Battles.Count(b => b.Result == "负");
        var draw = p.Battles.Count(b => b.Result == "平");
        var content = Ui.Stack(10,
            Ui.Row(10, Ui.Lbl(p.Emoji, 26), Ui.Lbl(p.Name, 19, Ui.Fg, true)),
            Ui.StatRow(
                ("已学", p.LearnedCount.ToString(), Color.FromArgb(p.Color)),
                ("掌握", p.MasteredCount.ToString(), Ui.Good),
                ("连续", AppState.Streak(p) + "天", Ui.Warn)),
            Ui.StatRow(
                ("今日新词", today.NewWords.ToString(), Ui.Good),
                ("今日复习", today.Reviews.ToString(), Ui.Warn),
                ("战绩", $"{wins}胜{loses}负{draw}平", Ui.Accent2)),
            Bar($"词库覆盖 {(double)p.LearnedCount / AppState.LibraryTotal:P1}", (double)p.LearnedCount / AppState.LibraryTotal),
            Bar($"两人中的领先度 {(double)p.LearnedCount / max:P0}", (double)p.LearnedCount / max));
        return Ui.Panel(content, Ui.Card, 16, 18);
    }

    static View Bar(string caption, double value)
    {
        return Ui.Stack(4,
            Ui.Lbl(caption, 12, Ui.Dim),
            new ProgressBar { Progress = Math.Clamp(value, 0, 1), ProgressColor = Ui.Accent, BackgroundColor = Ui.CardSoft });
    }
}

/// <summary>单词列表（已学 / 全部）。</summary>
public class WordListPage : ContentPage
{
    readonly bool _learnedOnly;
    readonly Entry _search = new()
    {
        Placeholder = "搜索英文或中文…",
        PlaceholderColor = Ui.Dim,
        TextColor = Ui.Fg,
        BackgroundColor = Ui.CardSoft,
    };
    readonly VerticalStackLayout _list = new() { Spacing = 8 };

    public WordListPage(bool learnedOnly)
    {
        _learnedOnly = learnedOnly;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;
        _search.TextChanged += (_, _) => Fill();

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        grid.Add(Ui.Header(learnedOnly ? "已学单词" : "全部单词", this), 0, 0);
        grid.Add(new VerticalStackLayout { Padding = new Thickness(16, 0, 16, 8), Children = { _search } }, 0, 1);
        grid.Add(new ScrollView { Content = new VerticalStackLayout { Padding = new Thickness(16, 0, 16, 28), Children = { _list } } }, 0, 2);
        Content = grid;
        Fill();
    }

    void Fill()
    {
        _list.Children.Clear();
        var key = (_search.Text ?? "").Trim();
        var source = new List<WordEntry>();
        foreach (var w in AppState.Words)
        {
            if (_learnedOnly && !AppState.Me.Progress.ContainsKey(w.Word) && !AppState.Other.Progress.ContainsKey(w.Word))
                continue;
            if (key.Length > 0 &&
                w.Word.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0 &&
                w.Translation.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            source.Add(w);
            if (source.Count >= 200)
                break;
        }
        _list.Children.Add(Ui.Lbl($"共 {source.Count} 条" + (source.Count >= 200 ? "（只显示前 200 条，请用搜索缩小范围）" : ""), 12, Ui.Dim));
        foreach (var w in source)
        {
            var who = new List<string>();
            if (AppState.Profiles["LGT"].Progress.ContainsKey(w.Word))
                who.Add("LGT");
            if (AppState.Profiles["LXL"].Progress.ContainsKey(w.Word))
                who.Add("LXL");
            var tail = who.Count > 0 ? string.Join("/", who) + " 已学" : "未学";
            var content = Ui.Stack(4,
                Ui.Row(10, Ui.Lbl(w.Word, 17, Ui.Fg, true), Ui.Chip(tail, who.Count > 0 ? Ui.Good : Ui.Dim)),
                Ui.Lbl(w.Phonetic, 12, Ui.Dim),
                Ui.Lbl(w.Translation, 13, Ui.Dim));
            _list.Children.Add(Ui.Panel(content, Ui.Card, 12, 14));
        }
    }
}
