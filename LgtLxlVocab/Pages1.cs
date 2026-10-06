namespace LgtLxlVocab;

/// <summary>启动后的密码锁屏（别名 LGT-LXL）。</summary>
public class UnlockPage : ContentPage
{
    readonly Entry _pwd;
    readonly Label _msg = Ui.Lbl("", 14, Ui.Bad);
    readonly CheckBox _remember = new() { Color = Ui.Accent };
    bool _started;

    public UnlockPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;

        _pwd = new Entry
        {
            IsPassword = true,
            Placeholder = "输入密码",
            PlaceholderColor = Ui.Dim,
            TextColor = Ui.Fg,
            BackgroundColor = Ui.CardSoft,
            Keyboard = Keyboard.Default,
            ReturnType = ReturnType.Go,
        };
        _pwd.Completed += (_, _) => Enter();

        var brand = Ui.Lbl(Ui_Logo(), 40, Ui.Fg, true);
        brand.HorizontalOptions = LayoutOptions.Center;
        var sub = Ui.Lbl("六级单词 · 双人背诵 · 单词对战", 14, Ui.Dim);
        sub.HorizontalOptions = LayoutOptions.Center;

        var lockIcon = Ui.Lbl("🔐", 46);
        lockIcon.HorizontalOptions = LayoutOptions.Center;

        var card = Ui.Panel(Ui.Stack(14,
            _pwd,
            Ui.Row(6, _remember, Ui.Lbl("在这台手机上记住密码", 14, Ui.Dim)),
            Ui.Btn("进 入", Ui.Accent, Colors.White, Enter, 17),
            _msg), Ui.Card, 18, 20);

        var tip = Ui.Panel(Ui.Stack(6,
            Ui.Lbl("离线可用", 14, Ui.Good, true),
            Ui.Lbl("词库、进度、对战记录全部保存在手机本地，没有网络也能正常背单词。", 13, Ui.Dim)), Ui.Bg2, 14, 16);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 60, 24, 30),
                Spacing = 18,
                Children = { lockIcon, brand, sub, Ui.Gap(6), card, tip },
            },
        };
    }

    static string Ui_Logo() => AppState.Brand;

    void Enter()
    {
        if (_pwd.Text == AppState.Password)
        {
            _msg.Text = "";
            Preferences.Default.Set("remember", _remember.IsChecked == true);
            _ = Navigation.PushAsync(new UserSelectPage());
        }
        else
        {
            _msg.Text = "密码不对哦，再想想～";
            _pwd.Text = "";
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started)
            return;
        _started = true;
        await AppState.LoadAsync();
        _remember.IsChecked = Preferences.Default.Get("remember", false);
        if (_remember.IsChecked)
            await Navigation.PushAsync(new UserSelectPage());
    }
}

/// <summary>选择是谁在背单词（两个用户各自独立记录进度）。</summary>
public class UserSelectPage : ContentPage
{
    public UserSelectPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AppState.LoadAsync();

        var stack = new VerticalStackLayout { Padding = new Thickness(20, 40, 20, 30), Spacing = 14 };
        stack.Children.Add(Ui.Lbl(AppState.Brand, 26, Ui.Fg, true));
        stack.Children.Add(Ui.Lbl("今天是谁来背单词？", 15, Ui.Dim));
        stack.Children.Add(Ui.Gap(8));
        foreach (var name in AppState.UserNames)
            stack.Children.Add(Card(AppState.Profiles[name]));
        stack.Children.Add(Ui.Gap(6));
        stack.Children.Add(Ui.Lbl("两个人的进度、连胜、对战成绩互不影响，可以随时切换。", 13, Ui.Dim));

        Content = new ScrollView { Content = stack };
    }

    View Card(UserProfile p)
    {
        var today = AppState.Today(p);
        var head = Ui.Row(12,
            Ui.Lbl(p.Emoji, 34),
            Ui.Stack(4,
                Ui.Lbl(p.Name, 20, Ui.Fg, true),
                Ui.Lbl($"已学 {p.LearnedCount} 词 · 掌握 {p.MasteredCount} 词", 13, Ui.Dim)));

        var stats = Ui.StatRow(
            ("今日新词", today.NewWords.ToString(), Ui.Good),
            ("今日复习", today.Reviews.ToString(), Ui.Warn),
            ("对战", p.Battles.Count.ToString(), Ui.Accent2));

        var content = Ui.Stack(12, head, stats, Ui.Btn($"以 {p.Name} 的身份进入", Color.FromArgb(p.Color), Colors.White,
            () =>
            {
                AppState.SetCurrentUser(p.Name);
                _ = Navigation.PushAsync(new HomePage());
            }));
        return Ui.Panel(content, Ui.Card, 16, 20);
    }
}

/// <summary>首页。</summary>
public class HomePage : ContentPage
{
    readonly VerticalStackLayout _root = new() { Padding = new Thickness(16, 10, 16, 28), Spacing = 12 };

    public HomePage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Bg;
        Content = new ScrollView { Content = _root };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AppState.LoadAsync();
        var p = AppState.Me;
        var today = AppState.Today(p);
        var remaining = Math.Max(0, p.DailyGoal - today.NewWords);

        _root.Children.Clear();

        var top = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        top.Add(Ui.Stack(2,
            Ui.Lbl(AppState.Brand, 18, Ui.Accent, true),
            Ui.Lbl($"{Greeting()}，{p.Name} {p.Emoji}", 22, Ui.Fg, true)), 0, 0);
        var userBtn = Ui.Btn("切换用户", Ui.CardSoft, Ui.Fg, () => _ = Navigation.PopToRootAsync(), 13);
        top.Add(userBtn, 1, 0);
        _root.Children.Add(top);

        _root.Children.Add(Ui.StatRow(
            ("今日新词", today.NewWords.ToString(), Ui.Good),
            ("今日复习", today.Reviews.ToString(), Ui.Warn),
            ("连续天数", AppState.Streak(p) + " 天", Ui.Accent2)));

        var goalText = today.NewWords >= p.DailyGoal
            ? "今日目标已完成 🎉 还能继续加练"
            : $"今日还差 {remaining} 个新词完成目标";
        _root.Children.Add(Ui.Panel(Ui.Stack(8,
            Ui.Row(10,
                Ui.Lbl("今日进度", 15, Ui.Fg, true),
                Ui.Lbl($"{today.NewWords} / {p.DailyGoal}", 13, Ui.Dim)),
            new ProgressBar
            {
                Progress = p.DailyGoal == 0 ? 0 : Math.Min(1, (double)today.NewWords / p.DailyGoal),
                ProgressColor = Ui.Accent,
                BackgroundColor = Ui.CardSoft,
            },
            Ui.Lbl(goalText, 13, Ui.Dim))));

        _root.Children.Add(BigButton("📖  开始背单词", $"已学 {p.LearnedCount} / {AppState.LibraryTotal} 词，点这里学新词或复习", Ui.Accent,
            () => _ = Navigation.PushAsync(new StudyPage())));
        _root.Children.Add(BigButton("⚔️  单词对战", "从两人已学单词中抽 50 或 100 词，一分钟分高下", Ui.Accent2,
            () => _ = Navigation.PushAsync(new BattleSetupPage())));
        _root.Children.Add(BigButton("📊  学习进度", "查看两人的进度对比和已学单词", Color.FromArgb("#3DD6C0"),
            () => _ = Navigation.PushAsync(new ProgressPage())));
        _root.Children.Add(BigButton("🏆  历史战绩", "看看谁才是背单词之王", Ui.Warn,
            () => _ = Navigation.PushAsync(new HistoryPage())));

        var peek = Ui.Stack(6,
            Ui.SectionTitle("两人对比"),
            CompareRow(AppState.Profiles["LGT"]),
            CompareRow(AppState.Profiles["LXL"]));
        _root.Children.Add(Ui.Panel(peek));

        _root.Children.Add(Ui.Panel(Ui.Stack(6,
            Ui.Lbl("📶 联机说明", 14, Ui.Fg, true),
            Ui.Lbl("联机对战通过 Wi-Fi / 手机热点直连，不需要流量。两台手机连同一个热点，一台创建房间，另一台输入显示的 IP 即可。", 12, Ui.Dim)), Ui.Bg2));
    }

    static View CompareRow(UserProfile p)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        row.Add(Ui.Lbl($"{p.Emoji} {p.Name}", 15, Ui.Fg), 0, 0);
        row.Add(Ui.Lbl($"已学 {p.LearnedCount} 词", 13, Ui.Dim), 1, 0);
        return row;
    }

    static View BigButton(string title, string subtitle, Color color, Action onTap)
    {
        var content = Ui.Row(12,
            Ui.Stack(4, Ui.Lbl(title, 18, Ui.Fg, true), Ui.Lbl(subtitle, 12, Ui.Dim)));
        return Ui.TapPanel(content, onTap, color.WithAlpha(0.22f), 16, 18);
    }

    static string Greeting()
    {
        var h = DateTime.Now.Hour;
        if (h < 6)
            return "夜深了";
        if (h < 11)
            return "早上好";
        if (h < 14)
            return "中午好";
        if (h < 18)
            return "下午好";
        return "晚上好";
    }
}
