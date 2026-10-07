namespace LgtLxlVocab;

/// <summary>
/// 两台手机同步学习进度：连同一个 Wi-Fi / 热点，直接互传进度并按「最近复习时间」合并。
/// 同步之后，两台手机上都能看到 LGT 和 LXL 两个人最新的进度。
/// </summary>
public class SyncPage : ContentPage
{
    readonly Label _status = Ui.Lbl("选择「发起同步」或「加入同步」", 14, Ui.Dim);
    readonly Label _info = Ui.Lbl("", 13, Ui.Dim);
    readonly VerticalStackLayout _found = new() { Spacing = 8 };
    readonly VerticalStackLayout _root = new() { Spacing = 12, Padding = new Thickness(16, 8, 16, 28) };
    readonly Entry _ipEntry = new()
    {
        Placeholder = "也可以手动输入对方 IP，例如 192.168.1.5",
        PlaceholderColor = Ui.Dim,
        TextColor = Ui.Fg,
        BackgroundColor = Ui.CardSoft,
        Keyboard = Keyboard.Text,
    };

    CancellationTokenSource? _cts;
    CancellationTokenSource? _responder;
    LanBattle? _lan;
    bool _busy;

    public SyncPage()
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
        grid.Add(Ui.Header("同步进度", this), 0, 0);
        grid.Add(new ScrollView { Content = _root }, 0, 1);
        Content = grid;

        _root.Children.Add(Ui.Panel(Ui.Stack(8,
            Ui.Lbl("同步什么？", 15, Ui.Fg, true),
            Ui.Lbl("两台手机上 LGT / LXL 的学习进度、每日统计、对战记录会合并到一起，" +
                   "同一台手机上就能看到两个人的进度了。", 12, Ui.Dim),
            Ui.Lbl("两台手机连同一个 Wi-Fi / 热点即可，不耗流量、不需要服务器。", 12, Ui.Dim)), Ui.Card));

        _root.Children.Add(Ui.Panel(Ui.Stack(6, _info), Ui.Bg2));
        _root.Children.Add(Ui.Btn("📡 我来发起同步（等对方加入）", Ui.Accent, Colors.White, Host, 16));
        _root.Children.Add(Ui.Btn("🔍 搜索附近正在同步的手机", Color.FromArgb("#3DD6C0"), Colors.White, () => _ = SearchAsync(), 16));
        _found.Children.Add(Ui.Lbl("搜索到的设备会显示在这里，点一下即可同步。", 12, Ui.Dim));
        _root.Children.Add(_found);
        _root.Children.Add(Ui.Panel(Ui.Stack(10,
            Ui.Lbl("加入同步", 15, Ui.Fg, true),
            _ipEntry,
            Ui.Btn("🔌 加入", Ui.Accent2, Colors.White, Join, 16)), Ui.Card));
        _root.Children.Add(Ui.Panel(Ui.Stack(6, _status), Ui.Bg2));

        RefreshInfo();
    }

    void RefreshInfo()
    {
        var lgt = AppState.Profiles["LGT"];
        var lxl = AppState.Profiles["LXL"];
        var ips = LanBattle.LocalIps();
        var ipText = ips.Count > 0
            ? string.Join("  /  ", ips.Select(x => x.Ip + "（" + x.Kind + "）"))
            : "（没找到局域网地址，请先连上 Wi-Fi 或热点）";
        _info.Text =
            $"本机当前记录：\n" +
            $"LGT 已学 {lgt.LearnedCount} 词 · 掌握 {lgt.MasteredCount} 词 · 对战 {lgt.Battles.Count} 场\n" +
            $"LXL 已学 {lxl.LearnedCount} 词 · 掌握 {lxl.MasteredCount} 词 · 对战 {lxl.Battles.Count} 场\n" +
            $"本机地址：{ipText}";
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
        SetStatus("正在准备同步…", Ui.Warn);
        try
        {
            var hostTask = _lan.HostAsync(_cts.Token, $"{AppState.Me.Name} 的进度同步");
            await Task.Delay(200);
            RefreshInfo();
            SetStatus($"等待对方加入…（对方可以点搜索，或手动输入本机地址，端口 {_lan.BoundPort}）", Ui.Warn);
            var hello = await hostTask;
            SetStatus($"已连接 {_lan.PeerName}，正在交换进度…", Ui.Good);
            if (!string.IsNullOrWhiteSpace(hello.Text))
            {
                var changed = await AppState.MergeProfilesAsync(hello.Text);
                await _lan.SendAsync(new NetMessage
                {
                    T = "sync",
                    Name = AppState.Me.Name,
                    Text = AppState.ExportProfiles(),
                }, _cts.Token);
                Finish(changed);
            }
            else
            {
                SetStatus("对方没有传来进度，请重试。", Ui.Bad);
            }
        }
        catch (OperationCanceledException)
        {
            StopResponder();
            SetStatus("已取消", Ui.Dim);
        }
        catch (Exception ex)
        {
            StopResponder();
            SetStatus("同步失败：" + ex.Message + "（确认两台手机在同一个 Wi-Fi，且端口没被占用）", Ui.Bad);
        }
        _busy = false;
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
            SetStatus("已连接，正在交换进度…", Ui.Good);
            await _lan.SendAsync(new NetMessage
            {
                T = "sync",
                Name = AppState.Me.Name,
                Text = AppState.ExportProfiles(),
            }, _cts.Token);
            var msg = await _lan.ReceiveAsync(_cts.Token);
            if (msg?.Text is { Length: > 0 } json)
            {
                var changed = await AppState.MergeProfilesAsync(json);
                Finish(changed);
            }
            else
            {
                SetStatus("没有收到对方的进度，请重试。", Ui.Bad);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消", Ui.Dim);
        }
        catch (Exception ex)
        {
            SetStatus("连接失败：" + ex.Message + "（确认 IP、同一个 Wi-Fi，以及对方已经点了「发起同步」）", Ui.Bad);
        }
        _busy = false;
    }

    void Finish(int changed)
    {
        SetStatus($"✅ 同步完成，更新了 {changed} 条记录", Ui.Good);
        RefreshInfo();
        var lgt = AppState.Profiles["LGT"];
        var lxl = AppState.Profiles["LXL"];
        _root.Children.Insert(3, Ui.Panel(Ui.Stack(6,
            Ui.Lbl("同步后的两人进度", 15, Ui.Fg, true),
            Ui.Lbl($"LGT 已学 {lgt.LearnedCount} 词 · 掌握 {lgt.MasteredCount} 词", 13, Ui.Dim),
            Ui.Lbl($"LXL 已学 {lxl.LearnedCount} 词 · 掌握 {lxl.MasteredCount} 词", 13, Ui.Dim),
            Ui.Lbl("回到首页或「学习进度」就能看到两个人最新的对比了。", 12, Ui.Dim)), Ui.Card));
    }

    async Task SearchAsync()
    {
        _found.Children.Clear();
        _found.Children.Add(Ui.Lbl("正在搜索（约 3 秒）…", 13, Ui.Dim));
        var list = await LanDiscovery.DiscoverAsync(3000);
        _found.Children.Clear();
        if (list.Count == 0)
        {
            _found.Children.Add(Ui.Lbl(
                "没有搜到。检查：① 对方已经点了「我来发起同步」；② 两台手机在同一个 Wi-Fi / 热点；③ 热点没开设备隔离。" +
                "也可以让对方把 IP 念给你，手动输入后点「加入」。", 12, Ui.Warn));
            return;
        }
        foreach (var host in list)
        {
            var (row, _) = Ui.TapRow($"📶  {host.Name}    {host.Ip}    点这里同步",
                () => { _ipEntry.Text = host.Ip; Join(); }, Ui.CardSoft, 14);
            _found.Children.Add(row);
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
