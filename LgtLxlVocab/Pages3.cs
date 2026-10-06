namespace LgtLxlVocab;

/// <summary>对战设置：抽多少词（50 / 100）、本机轮流还是联机。</summary>
public class BattleSetupPage : ContentPage
{
    readonly VerticalStackLayout _root = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    int _count = 50;
    bool _online;

    public BattleSetupPage()
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
        grid.Add(Ui.Header("单词对战", this), 0, 0);
        grid.Add(new ScrollView { Content = _root }, 0, 1);
        Content = grid;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Build();
    }

    void Build()
    {
        var pool = AppState.LearnedUnion();
        var usable = pool.Count;
        var lgtCount = AppState.Profiles["LGT"].LearnedCount;
        var lxlCount = AppState.Profiles["LXL"].LearnedCount;
        _root.Children.Clear();

        _root.Children.Add(Ui.Panel(Ui.Stack(6,
            Ui.Lbl("两人已学单词", 15, Ui.Fg, true),
            Ui.Lbl($"LGT 已学 {lgtCount} 词 · LXL 已学 {lxlCount} 词 · 去重后可用于抽题 {usable} 词", 13, Ui.Dim),
            Ui.Lbl(usable < 50 ? "⚠️ 词数不足 50，本局会按实际数量出题；先多背一些单词会更好玩。" : "✅ 词量充足，可以开始对战。",
                12, usable < 50 ? Ui.Warn : Ui.Good)), Ui.Card, 16, 18));

        _root.Children.Add(Ui.SectionTitle("每局词数"));
        var countRow = new Grid { ColumnSpacing = 10 };
        countRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        countRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        countRow.Add(Choice("50 词", _count == 50, () => { _count = 50; Build(); }), 0, 0);
        countRow.Add(Choice("100 词", _count == 100, () => { _count = 100; Build(); }), 1, 0);
        _root.Children.Add(countRow);

        _root.Children.Add(Ui.SectionTitle("对战方式"));
        _root.Children.Add(Choice("📱 本机轮流（一台手机，两个人先后答同一套题）", !_online, () => { _online = false; Build(); }));
        _root.Children.Add(Choice("📶 联机对战（两台手机连同一个 Wi-Fi / 热点直连）", _online, () => { _online = true; Build(); }));

        _root.Children.Add(Ui.Gap(4));
        _root.Children.Add(Ui.Btn("开始对战", _online ? Ui.Accent2 : Ui.Accent, Colors.White, Start, 18));

        _root.Children.Add(Ui.Panel(Ui.Stack(6,
            Ui.Lbl("计分规则", 14, Ui.Fg, true),
            Ui.Lbl("每题 1 分，答对得分，答错不得分；总分高者胜，同分为平局。结果会记到两人的历史战绩里。", 12, Ui.Dim)), Ui.Bg2));
    }

    static View Choice(string text, bool selected, Action onTap)
    {
        var label = Ui.Lbl(text, 14, selected ? Colors.White : Ui.Fg, selected);
        var panel = Ui.TapPanel(label, onTap, selected ? Ui.Accent : Ui.CardSoft, 14, 14);
        return panel;
    }

    async void Start()
    {
        var pool = AppState.LearnedUnion();
        if (pool.Count < 5)
        {
            await Navigation.PushAsync(new StudyPage());
            return;
        }
        var want = Math.Min(_count, pool.Count);
        if (!_online)
        {
            var qs = AppState.BuildBattleSet(pool, want, AppState.Words);
            await Navigation.PushAsync(new BattlePlayPage(qs, "本机", AppState.OtherUser, want, null));
        }
        else
        {
            await Navigation.PushAsync(new LanLobbyPage(want));
        }
    }
}

/// <summary>联机房间：一台创建，另一台输入 IP 加入。</summary>
public class LanLobbyPage : ContentPage
{
    readonly int _count;
    readonly Label _status = Ui.Lbl("请选择创建房间或加入房间", 14, Ui.Dim);
    readonly Label _ipInfo = Ui.Lbl("", 13, Ui.Dim);
    readonly Entry _ipEntry = new()
    {
        Placeholder = "输入房主手机上显示的 IP",
        PlaceholderColor = Ui.Dim,
        TextColor = Ui.Fg,
        BackgroundColor = Ui.CardSoft,
        Keyboard = Keyboard.Numeric,
    };
    readonly VerticalStackLayout _root = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    CancellationTokenSource? _cts;
    LanBattle? _lan;
    bool _busy;

    public LanLobbyPage(int count)
    {
        _count = count;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;
        _ipEntry.Text = Preferences.Default.Get("last_ip", "");

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        grid.Add(Ui.Header("联机对战", this), 0, 0);
        grid.Add(new ScrollView { Content = _root }, 0, 1);
        Content = grid;

        _root.Children.Add(Ui.Panel(Ui.Stack(8,
            Ui.Lbl($"本局词数：{count} 词", 15, Ui.Fg, true),
            Ui.Lbl("两台手机连同一个 Wi-Fi（或同一个手机热点），一台点「创建房间」，另一台输入 IP 点「加入房间」。不消耗流量。", 12, Ui.Dim)), Ui.Card));

        _root.Children.Add(Ui.Btn("🏠 创建房间（我是房主）", Ui.Accent, Colors.White, Host, 16));
        _root.Children.Add(Ui.Panel(Ui.Stack(10,
            Ui.Lbl("加入房间", 15, Ui.Fg, true),
            _ipEntry,
            Ui.Btn("🔌 加入", Ui.Accent2, Colors.White, Join, 16)), Ui.Card));
        _root.Children.Add(Ui.Panel(Ui.Stack(6, _status, _ipInfo), Ui.Bg2));
    }

    void SetStatus(string text, Color? color = null)
    {
        _status.Text = text;
        _status.TextColor = color ?? Ui.Dim;
    }

    async void Host()
    {
        if (_busy)
            return;
        _busy = true;
        _cts = new CancellationTokenSource();
        _lan = new LanBattle();
        var ip = LanBattle.LocalIp();
        SetStatus($"等待对方加入…（对方输入 {ip} 后点「加入」）", Ui.Warn);
        _ipInfo.Text = $"本机 IP：{ip}    端口：{LanBattle.Port}\n若一直连不上：确认两台手机在同一个 Wi-Fi / 热点，且房主手机的「个人热点」没有被设备隔离限制。";
        try
        {
            var hello = await _lan.HostAsync(_cts.Token);
            SetStatus($"✅ {_lan.PeerName} 已加入，正在开始…", Ui.Good);
            var pool = new HashSet<string>(AppState.Me.Progress.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var w in hello.Learned ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(w))
                    pool.Add(w);
            var qs = AppState.BuildBattleSet(pool.Where(AppState.ByWord.ContainsKey).ToList(), _count, AppState.Words);
            await _lan.SendAsync(new NetMessage
            {
                T = "start",
                Count = qs.Count,
                Name = AppState.Me.Name,
                Words = qs,
            }, _cts.Token);
            _busy = false;
            await Navigation.PushAsync(new BattlePlayPage(qs, "联机", _lan.PeerName, qs.Count, _lan));
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消", Ui.Dim);
            _busy = false;
        }
        catch (Exception ex)
        {
            SetStatus("创建房间失败：" + ex.Message, Ui.Bad);
            _busy = false;
        }
    }

    async void Join()
    {
        if (_busy)
            return;
        _busy = true;
        _cts = new CancellationTokenSource();
        _lan = new LanBattle();
        var ip = (_ipEntry.Text ?? "").Trim();
        SetStatus($"正在连接 {ip} …", Ui.Warn);
        try
        {
            await _lan.ConnectAsync(ip, _cts.Token);
            Preferences.Default.Set("last_ip", ip);
            SetStatus("已连接，等待房主发题…", Ui.Good);
            await _lan.SendAsync(new NetMessage
            {
                T = "hello",
                Name = AppState.Me.Name,
                Learned = AppState.Me.Progress.Keys.ToList(),
            }, _cts.Token);
            var msg = await _lan.ReceiveAsync(_cts.Token);
            if (msg?.T != "start" || msg.Words == null)
            {
                SetStatus("房主没有发来题目，请重试。", Ui.Bad);
                _busy = false;
                return;
            }
            _lan.PeerName = msg.Name ?? "房主";
            _busy = false;
            await Navigation.PushAsync(new BattlePlayPage(msg.Words, "联机", _lan.PeerName, msg.Count, _lan));
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消", Ui.Dim);
            _busy = false;
        }
        catch (Exception ex)
        {
            SetStatus("连接失败：" + ex.Message + "（请确认 IP、同一个 Wi-Fi，以及房主是否已创建房间）", Ui.Bad);
            _busy = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Navigation.NavigationStack.LastOrDefault() == this)
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }
        }
    }
}
