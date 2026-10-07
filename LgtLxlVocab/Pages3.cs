namespace LgtLxlVocab;

/// <summary>对战设置：抽多少词（50 / 100）、本机轮流还是联机。</summary>
public class BattleSetupPage : ContentPage
{
    readonly VerticalStackLayout _root = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    readonly VerticalStackLayout _extra = new() { Spacing = 8 };
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

        _root.Children.Add(_extra);

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
        _extra.Children.Clear();
        var pool = AppState.LearnedUnion();
        if (pool.Count < 5)
        {
            _extra.Children.Add(Ui.Panel(Ui.Stack(8,
                Ui.Lbl("两个人学会的单词还不够", 16, Ui.Warn, true),
                Ui.Lbl($"现在两人加起来的已学单词只有 {pool.Count} 个，至少要 5 个才能出题。" +
                       "（进度保存在手机本地，卸载重装会清空，所以先背几个词吧）", 13, Ui.Dim),
                Ui.Btn("📖 先去背单词", Ui.Accent, Colors.White, () => _ = Navigation.PushAsync(new StudyPage()), 16),
                Ui.Btn("🎲 用词库随机词开一局（先试试战功能）", Ui.CardSoft, Ui.Fg, StartWithRandomWords, 15)), Ui.Card));
            return;
        }
        await LaunchAsync(pool);
    }

    void StartWithRandomWords()
    {
        var pool = AppState.Words
            .OrderBy(_ => Random.Shared.Next())
            .Take(300)
            .Select(w => w.Word)
            .ToList();
        _ = LaunchAsync(pool);
    }

    async Task LaunchAsync(List<string> pool)
    {
        if (!_online)
        {
            var want = Math.Min(_count, Math.Max(5, pool.Count));
            var qs = AppState.BuildBattleSet(pool, want, AppState.Words);
            await Navigation.PushAsync(new BattlePlayPage(qs, "本机", AppState.OtherUser, qs.Count, null));
        }
        else
        {
            await Navigation.PushAsync(new LanLobbyPage(_count));
        }
    }
}

/// <summary>联机房间：一台创建，另一台输入 IP 加入。</summary>
public class LanLobbyPage : ContentPage
{
    readonly int _count;
    readonly Label _status = Ui.Lbl("请选择创建房间或加入房间", 14, Ui.Dim);
    readonly Label _ipInfo = Ui.Lbl("", 13, Ui.Dim);
    readonly VerticalStackLayout _found = new() { Spacing = 8 };
    readonly Entry _ipEntry = new()
    {
        Placeholder = "也可以手动输入 IP，例如 192.168.1.5",
        PlaceholderColor = Ui.Dim,
        TextColor = Ui.Fg,
        BackgroundColor = Ui.CardSoft,
        Keyboard = Keyboard.Text,
    };
    readonly VerticalStackLayout _root = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    CancellationTokenSource? _cts;
    CancellationTokenSource? _responder;
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
        _root.Children.Add(Ui.Btn("🔍 自动搜索附近的房主", Color.FromArgb("#3DD6C0"), Colors.White, () => _ = SearchAsync(), 16));
        _found.Children.Add(Ui.Lbl("点上面的按钮搜索，或让房主把 IP 念给你手动输入。", 12, Ui.Dim));
        _root.Children.Add(_found);
        _root.Children.Add(Ui.Panel(Ui.Stack(10,
            Ui.Lbl("加入房间", 15, Ui.Fg, true),
            _ipEntry,
            Ui.Btn("🔌 加入", Ui.Accent2, Colors.White, Join, 16)), Ui.Card));
        _root.Children.Add(Ui.Panel(Ui.Stack(6, _status, _ipInfo), Ui.Bg2));
    }

    async Task SearchAsync()
    {
        _found.Children.Clear();
        _found.Children.Add(Ui.Lbl("正在搜索附近的房主（约 3 秒）…", 13, Ui.Dim));
        var list = await LanDiscovery.DiscoverAsync(3000);
        _found.Children.Clear();
        if (list.Count == 0)
        {
            _found.Children.Add(Ui.Lbl(
                "没有搜到房主。检查一下：① 对方已经点了「创建房间」；② 两台手机连的是同一个 Wi-Fi / 热点；" +
                "③ 热点没开「设备隔离」。也可以让房主把屏幕上的 IP 念给你，手动输入后点「加入」。", 12, Ui.Warn));
            return;
        }
        foreach (var host in list)
        {
            var (row, _) = Ui.TapRow($"📶  {host.Name}    {host.Ip}    点这里加入",
                () => { _ipEntry.Text = host.Ip; Join(); }, Ui.CardSoft, 14);
            _found.Children.Add(row);
        }
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
        SetStatus("正在创建房间…", Ui.Warn);
        _ipInfo.Text = "正在准备监听端口…";
        try
        {
            var hostTask = _lan.HostAsync(_cts.Token, $"{AppState.Me.Name} 的单词房");
            await Task.Delay(200);
            var ips = LanBattle.LocalIps();
            var ipText = ips.Count > 0
                ? string.Join("  /  ", ips.Select(x => x.Ip + "（" + x.Kind + "）"))
                : "（没找到局域网地址，请先连上 Wi-Fi 或热点）";
            _ipInfo.Text = $"本机地址：{ipText}\n端口：{_lan.BoundPort}\n" +
                           "对方点「自动搜索附近的房主」最省事；连不上时，建议改成一台手机开热点、另一台连热点。";
            SetStatus("等待对方加入…", Ui.Warn);
            var hello = await hostTask;
            SetStatus($"✅ {_lan.PeerName} 已加入，正在开始…", Ui.Good);
            var pool = new HashSet<string>(AppState.LearnedUnion(), StringComparer.OrdinalIgnoreCase);
            foreach (var w in hello.Learned ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(w))
                    pool.Add(w);
            var usable = pool.Where(AppState.ByWord.ContainsKey).ToList();
            if (usable.Count < 5)
            {
                // 两人都还没背多少词：用词库随机词开局，方便先把联机跑通
                usable = AppState.Words.OrderBy(_ => Random.Shared.Next()).Take(300).Select(w => w.Word).ToList();
                SetStatus($"两人已学单词较少，本局改用词库随机词", Ui.Warn);
            }
            var qs = AppState.BuildBattleSet(usable, _count, AppState.Words);
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
            StopResponder();
            SetStatus("已取消", Ui.Dim);
            _busy = false;
        }
        catch (Exception ex)
        {
            StopResponder();
            SetStatus("创建房间失败：" + ex.Message, Ui.Bad);
            _busy = false;
        }
    }

    void StopResponder()
    {
        try
        {
            _responder?.Cancel();
            _responder?.Dispose();
        }
        catch
        {
        }
        _responder = null;
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
            StopResponder();
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
