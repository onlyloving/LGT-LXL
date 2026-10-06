namespace LgtLxlVocab;

/// <summary>对战答题页：支持本机轮流与联机同时答题。</summary>
public class BattlePlayPage : ContentPage
{
    readonly List<Question> _qs;
    readonly string _mode;
    readonly string _opponent;
    readonly int _wordCount;
    readonly LanBattle? _lan;
    readonly CancellationTokenSource _cts = new();

    readonly VerticalStackLayout _body = new() { Spacing = 10 };
    readonly VerticalStackLayout _page = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    readonly ProgressBar _bar = new() { ProgressColor = Ui.Accent2, BackgroundColor = Ui.CardSoft };
    readonly Label _head = Ui.Lbl("", 14, Ui.Fg, true);
    readonly Label _peer = Ui.Lbl("", 12, Ui.Dim);
    readonly List<Border> _options = new();

    int _index;
    int _score;
    int _passScore;
    int _pass;
    int _peerScore;
    bool _answered;
    bool _started;
    bool _peerDone;
    string _player = "";

    public BattlePlayPage(List<Question> questions, string mode, string opponent, int wordCount, LanBattle? lan)
    {
        _qs = questions;
        _mode = mode;
        _opponent = opponent;
        _wordCount = wordCount;
        _lan = lan;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;
        _page.Children.Add(_head);
        _page.Children.Add(_bar);
        _page.Children.Add(_peer);
        _page.Children.Add(_body);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        grid.Add(Ui.Header(_mode == "联机" ? "联机对战" : "本机对战", this, false), 0, 0);
        grid.Add(new ScrollView { Content = _page }, 0, 1);
        Content = grid;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_started)
            return;
        _started = true;
        if (_lan != null)
            _ = ReceiveLoopAsync();
        StartPass(0);
    }

    void StartPass(int pass)
    {
        _pass = pass;
        _index = 0;
        _score = 0;
        _player = pass == 0 ? AppState.Me.Name : _opponent;
        ShowQuestion();
    }

    void ShowQuestion()
    {
        _answered = false;
        _options.Clear();
        _body.Children.Clear();
        var q = _qs[_index];

        _bar.Progress = (double)_index / Math.Max(1, _qs.Count);
        _head.Text = $"{_player} 答题 · 第 {_index + 1} / {_qs.Count} 词 · 当前 {_score} 分";
        _peer.Text = _mode == "联机"
            ? $"对手 {_opponent}：{_peerScore} 分"
            : _pass == 0
                ? $"本机轮流模式，答完后把手机交给 {_opponent}"
                : $"{AppState.CurrentUser} 第一轮得了 {_passScore} 分";

        _body.Children.Add(Ui.Panel(Ui.Stack(6,
            Ui.Lbl(q.Word, 30, Ui.Fg, true),
            Ui.Lbl(q.Phonetic, 13, Ui.Dim)), Ui.Card, 16, 18));

        for (var i = 0; i < q.Options.Count; i++)
        {
            var idx = i;
            var (row, _) = Ui.TapRow($"{Letter(i)}. {q.Options[i]}", () => _ = AnswerAsync(idx));
            _options.Add(row);
            _body.Children.Add(row);
        }
    }

    static string Letter(int i) => new[] { "A", "B", "C", "D" }[Math.Clamp(i, 0, 3)];

    async Task AnswerAsync(int index)
    {
        if (_answered)
            return;
        _answered = true;
        var q = _qs[_index];
        var correct = index == q.Answer;
        for (var i = 0; i < _options.Count && i < q.Options.Count; i++)
        {
            var color = i == q.Answer ? Ui.Good : i == index ? Ui.Bad : Ui.Dim;
            _options[i].BackgroundColor = (i == q.Answer ? Ui.Good : i == index ? Ui.Bad : Ui.CardSoft).WithAlpha(0.24f);
            if (_options[i].Content is Label lab)
                lab.TextColor = color;
        }
        if (correct)
            _score++;
        _head.Text = $"{_player} 答题 · 第 {_index + 1} / {_qs.Count} 词 · 当前 {_score} 分";

        if (_lan != null)
            await _lan.SendAsync(new NetMessage { T = "prog", Index = _index + 1, Score = _score }, _cts.Token);

        await Task.Delay(correct ? 450 : 1000);
        _index++;
        if (_index >= _qs.Count)
            await EndPassAsync();
        else
            ShowQuestion();
    }

    async Task EndPassAsync()
    {
        if (_mode == "联机")
        {
            await FinishOnlineAsync();
            return;
        }
        if (_pass == 0)
        {
            _passScore = _score;
            ShowHandover();
            return;
        }
        await AppState.RecordBattleFor(AppState.CurrentUser, "本机", _opponent, _wordCount, _passScore, _score);
        await AppState.RecordBattleFor(_opponent, "本机", AppState.CurrentUser, _wordCount, _score, _passScore);
        await Navigation.PushAsync(new BattleResultPage(_passScore, _score, _opponent, _wordCount, "本机"));
    }

    void ShowHandover()
    {
        _bar.Progress = 1;
        _body.Children.Clear();
        _options.Clear();
        _body.Children.Add(Ui.Panel(Ui.Stack(10,
            Ui.Lbl("第一轮结束 🎯", 20, Ui.Fg, true),
            Ui.Lbl($"{AppState.CurrentUser} 答对 {_passScore} / {_qs.Count} 题", 15, Ui.Dim),
            Ui.Lbl($"现在把手机交给 {_opponent}，答同一套题，分高者胜。", 13, Ui.Dim),
            Ui.Btn($"{_opponent} 开始答题 →", Ui.Accent2, Colors.White, () => StartPass(1), 17)), Ui.Card, 18, 20));
    }

    async Task FinishOnlineAsync()
    {
        _bar.Progress = 1;
        _body.Children.Clear();
        _options.Clear();
        _body.Children.Add(Ui.Panel(Ui.Stack(8,
            Ui.Lbl("你已答完 🎉", 20, Ui.Fg, true),
            Ui.Lbl($"你的得分：{_score} 分", 16, Ui.Good, true),
            Ui.Lbl($"正在等待 {_opponent} 完成…", 13, Ui.Dim)), Ui.Card));

        if (_lan != null)
            await _lan.SendAsync(new NetMessage { T = "finish", Score = _score }, _cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(180);
        while (!_peerDone && DateTime.UtcNow < deadline)
            await Task.Delay(250);

        await AppState.RecordBattleFor(AppState.CurrentUser, "联机", _opponent, _wordCount, _score, _peerScore);
        await Navigation.PushAsync(new BattleResultPage(_score, _peerScore, _opponent, _wordCount, "联机"));
    }

    async Task ReceiveLoopAsync()
    {
        if (_lan == null)
            return;
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var msg = await _lan.ReceiveAsync(_cts.Token);
                if (msg == null)
                    break;
                if (msg.T == "prog")
                {
                    _peerScore = msg.Score;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (_mode == "联机" && !_answered)
                            _peer.Text = $"对手 {_opponent}：已答 {msg.Index}/{_qs.Count}，{msg.Score} 分";
                    });
                }
                else if (msg.T == "finish")
                {
                    _peerScore = msg.Score;
                    _peerDone = true;
                    break;
                }
            }
        }
        catch
        {
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Navigation.NavigationStack.LastOrDefault() == this)
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
            }
        }
    }
}

/// <summary>对战结果。</summary>
public class BattleResultPage : ContentPage
{
    readonly int _my;
    readonly int _peer;
    readonly string _opponent;
    readonly int _count;
    readonly string _mode;

    public BattleResultPage(int myScore, int peerScore, string opponent, int count, string mode)
    {
        _my = myScore;
        _peer = peerScore;
        _opponent = opponent;
        _count = count;
        _mode = mode;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;

        var win = myScore > peerScore;
        var draw = myScore == peerScore;
        var title = draw ? "平局！" : win ? $"{AppState.CurrentUser} 获胜 🏆" : $"{opponent} 获胜";
        var color = draw ? Ui.Warn : win ? Ui.Good : Ui.Bad;

        var content = Ui.Stack(14,
            Ui.Lbl(title, 26, color, true),
            Ui.Lbl(_mode + "对战 · 共 " + count + " 词", 13, Ui.Dim),
            Ui.StatRow(
                ($"{AppState.CurrentUser}", myScore.ToString(), win ? Ui.Good : Ui.Fg),
                ("VS", "⚔️", Ui.Accent2),
                ($"{opponent}", peerScore.ToString(), !win && !draw ? Ui.Bad : Ui.Fg)),
            Ui.Lbl(win
                ? $"领先 {myScore - peerScore} 分，继续保持！"
                : draw
                    ? "势均力敌，再来一局分胜负！"
                    : $"落后 {peerScore - myScore} 分，下一局赢回来！", 14, Ui.Dim),
            Ui.Btn("再来一局", Ui.Accent, Colors.White, () => _ = Ui.GoHomeAsync(this, new BattleSetupPage()), 16),
            Ui.Btn("返回首页", Ui.CardSoft, Ui.Fg, () => _ = Ui.GoHomeAsync(this), 16));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 60, 20, 30),
                Spacing = 14,
                Children = { Ui.Panel(content, Ui.Card, 20, 22) },
            },
        };
    }
}

/// <summary>历史战绩。</summary>
public class HistoryPage : ContentPage
{
    readonly VerticalStackLayout _root = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };

    public HistoryPage()
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
        grid.Add(Ui.Header("历史战绩", this), 0, 0);
        grid.Add(new ScrollView { Content = _root }, 0, 1);
        Content = grid;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _root.Children.Clear();
        foreach (var name in AppState.UserNames)
            _root.Children.Add(UserSection(AppState.Profiles[name]));
    }

    static View UserSection(UserProfile p)
    {
        var wins = p.Battles.Count(b => b.Result == "胜");
        var loses = p.Battles.Count(b => b.Result == "负");
        var draw = p.Battles.Count(b => b.Result == "平");
        var stack = Ui.Stack(8,
            Ui.Row(10, Ui.Lbl(p.Emoji, 22), Ui.Lbl($"{p.Name} 的战绩", 17, Ui.Fg, true)),
            Ui.Lbl($"共 {p.Battles.Count} 场 · {wins} 胜 {loses} 负 {draw} 平", 13, Ui.Dim));
        if (p.Battles.Count == 0)
        {
            stack.Children.Add(Ui.Lbl("还没有对战记录，快去挑战一局吧！", 13, Ui.Dim));
        }
        else
        {
            foreach (var b in p.Battles.Take(30))
            {
                var color = b.Result == "胜" ? Ui.Good : b.Result == "负" ? Ui.Bad : Ui.Warn;
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                    },
                };
                row.Add(Ui.Stack(3,
                    Ui.Lbl($"{b.Mode} · vs {b.Opponent} · {b.WordCount} 词", 14, Ui.Fg, true),
                    Ui.Lbl(b.Time.ToString("yyyy-MM-dd HH:mm"), 12, Ui.Dim)), 0, 0);
                row.Add(Ui.Stack(3,
                    Ui.Lbl($"{b.MyScore} : {b.OpponentScore}", 15, color, true),
                    Ui.Lbl(b.Result, 12, color)), 1, 0);
                stack.Children.Add(Ui.Panel(row, Ui.CardSoft, 12, 14));
            }
        }
        return Ui.Panel(stack, Ui.Card, 16, 18);
    }
}
