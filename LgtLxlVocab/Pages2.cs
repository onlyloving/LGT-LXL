namespace LgtLxlVocab;

/// <summary>背单词（选择题 + 单词卡 + 例句，先复习后学新词）。</summary>
public class StudyPage : ContentPage
{
    readonly VerticalStackLayout _body = new() { Spacing = 12 };
    readonly VerticalStackLayout _page = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    readonly ProgressBar _bar = new() { ProgressColor = Ui.Accent, BackgroundColor = Ui.CardSoft };
    readonly Label _counter = Ui.Lbl("", 13, Ui.Dim);
    readonly Label _score = Ui.Lbl("", 13, Ui.Dim);
    readonly Label _mode = Ui.Lbl("", 12, Ui.Accent);
    readonly List<Border> _optionRows = new();

    List<Question> _queue = new();
    int _index;
    int _right;
    int _wrong;
    int _newWords;
    bool _answered;
    bool _started;
    Question? _cur;
    bool _curIsReview;

    public StudyPage()
    {
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
        grid.Add(Ui.Header("背单词", this, true, "结束", () => _ = Navigation.PopAsync()), 0, 0);
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
        _queue = AppState.BuildStudySet(AppState.Me, 20);
        _index = 0;
        _right = 0;
        _wrong = 0;
        _newWords = 0;
        if (_queue.Count == 0)
        {
            ShowEmpty();
            return;
        }
        ShowQuestion();
    }

    void UpdateHeader()
    {
        _bar.Progress = _queue.Count == 0 ? 0 : (double)_index / _queue.Count;
        _counter.Text = $"第 {Math.Min(_index + 1, _queue.Count)} / {_queue.Count} 词";
        _score.Text = $"✅ {_right}   ❌ {_wrong}";
    }

    void ShowEmpty()
    {
        _body.Children.Clear();
        _body.Children.Add(Ui.Panel(Ui.Stack(8,
            Ui.Lbl("词库里暂时没有可学的单词", 16, Ui.Fg, true),
            Ui.Lbl("去「单词对战」或者稍后再来看看吧。", 13, Ui.Dim))));
    }

    void ShowQuestion()
    {
        _answered = false;
        _cur = _queue[_index];
        _curIsReview = AppState.Me.Progress.ContainsKey(_cur.Word);
        UpdateHeader();
        _mode.Text = _curIsReview ? "🔁 复习 · 选出生词的中文意思" : "✨ 新词 · 选出生词的中文意思";

        _body.Children.Clear();
        _optionRows.Clear();

        var card = Ui.Panel(Ui.Stack(8,
            Ui.Lbl(_cur.Word, 34, Ui.Fg, true),
            Ui.Lbl(_cur.Phonetic, 14, Ui.Dim)), Ui.Card, 18, 20);
        _body.Children.Add(card);

        for (var i = 0; i < _cur.Options.Count; i++)
        {
            var idx = i;
            var (row, label) = Ui.TapRow($"{Letter(i)}. {_cur.Options[i]}", () => OnOption(idx));
            _optionRows.Add(row);
            _body.Children.Add(row);
        }
    }

    static string Letter(int i) => new[] { "A", "B", "C", "D" }[Math.Clamp(i, 0, 3)];

    async void OnOption(int index)
    {
        if (_answered || _cur == null)
            return;
        _answered = true;
        var correct = index == _cur.Answer;

        for (var i = 0; i < _optionRows.Count && i < _cur.Options.Count; i++)
        {
            var c = i == _cur.Answer ? Ui.Good : i == index ? Ui.Bad : Ui.Dim;
            _optionRows[i].BackgroundColor = (i == _cur.Answer ? Ui.Good : i == index ? Ui.Bad : Ui.CardSoft).WithAlpha(0.22f);
            if (_optionRows[i].Content is Label lab)
                lab.TextColor = c;
        }

        if (correct)
            _right++;
        else
            _wrong++;
        if (!_curIsReview)
            _newWords++;
        UpdateHeader();

        var detail = Ui.Stack(8,
            Ui.Lbl(correct ? "✅ 答对了！" : "❌ 再记一遍～", 16, correct ? Ui.Good : Ui.Bad, true),
            Ui.Lbl(_cur.Translation, 16, Ui.Fg, true));
        if (!string.IsNullOrWhiteSpace(_cur.Example))
        {
            detail.Children.Add(Ui.Lbl(_cur.Example, 13, Ui.Dim));
            if (!string.IsNullOrWhiteSpace(_cur.ExampleCn))
                detail.Children.Add(Ui.Lbl(_cur.ExampleCn, 13, Ui.Dim));
        }
        var nextText = _index + 1 >= _queue.Count ? "完成本轮 ✓" : "下一个 →";
        detail.Children.Add(Ui.Btn(nextText, Ui.Accent, Colors.White, Next, 16));
        _body.Children.Add(Ui.Panel(detail, Ui.Bg2));

        await AppState.ApplyStudyAsync(AppState.Me, _cur, correct, !_curIsReview);
    }

    void Next()
    {
        _index++;
        if (_index >= _queue.Count)
            Finish();
        else
            ShowQuestion();
    }

    void Finish()
    {
        _bar.Progress = 1;
        _counter.Text = $"本轮完成 {_queue.Count} 词";
        _mode.Text = "🎉 本轮结束";
        _body.Children.Clear();
        var p = AppState.Me;
        var box = Ui.Stack(10,
            Ui.Lbl($"太棒了，{p.Name}！", 22, Ui.Fg, true),
            Ui.Lbl($"新词 {_newWords} 个 · 正确 {_right} · 错误 {_wrong}", 15, Ui.Dim),
            Ui.StatRow(
                ("已学", p.LearnedCount.ToString(), Ui.Accent),
                ("掌握", p.MasteredCount.ToString(), Ui.Good),
                ("连续", AppState.Streak(p) + "天", Ui.Accent2)),
            Ui.Btn("再来一组 20 词", Ui.Accent, Colors.White, StartSession, 16),
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
