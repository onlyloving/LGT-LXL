using Microsoft.Maui.Controls.Shapes;

namespace LgtLxlVocab;

/// <summary>统一配色 + 少量控件工厂，让页面代码保持简洁。</summary>
public static class Ui
{
    public static readonly Color Bg = Color.FromArgb("#0E1020");
    public static readonly Color Bg2 = Color.FromArgb("#161A38");
    public static readonly Color Card = Color.FromArgb("#1D2246");
    public static readonly Color CardSoft = Color.FromArgb("#262C5C");
    public static readonly Color Accent = Color.FromArgb("#7C6BFF");
    public static readonly Color Accent2 = Color.FromArgb("#FF7AA2");
    public static readonly Color Fg = Color.FromArgb("#F3F4FF");
    public static readonly Color Dim = Color.FromArgb("#9AA0C9");
    public static readonly Color Good = Color.FromArgb("#37D67A");
    public static readonly Color Bad = Color.FromArgb("#FF5C7A");
    public static readonly Color Warn = Color.FromArgb("#FFC65C");

    public static Label Lbl(string text, double size = 16, Color? color = null, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        TextColor = color ?? Fg,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        LineBreakMode = LineBreakMode.WordWrap,
    };

    public static Button Btn(string text, Color bg, Color fg, Action? click = null, double size = 16)
    {
        var b = new Button
        {
            Text = text,
            BackgroundColor = bg,
            TextColor = fg,
            FontSize = size,
            CornerRadius = 14,
            Padding = new Thickness(14, 12),
            LineBreakMode = LineBreakMode.WordWrap,
        };
        if (click != null)
            b.Clicked += (_, _) => click();
        return b;
    }

    public static Border Panel(View content, Color? bg = null, double pad = 16, double radius = 18, Color? stroke = null) => new()
    {
        Content = content,
        BackgroundColor = bg ?? Card,
        Padding = pad,
        StrokeThickness = stroke == null ? 0 : 1,
        Stroke = stroke ?? Colors.Transparent,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
    };

    public static Border Chip(string text, Color color) => new()
    {
        Content = Lbl(text, 12, color, true),
        BackgroundColor = color.WithAlpha(0.16f),
        Padding = new Thickness(10, 4),
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
    };

    /// <summary>可点击的整行（用于选项、列表项）。</summary>
    public static (Border row, Label label) TapRow(string text, Action onTap, Color? bg = null, double size = 16)
    {
        var label = Lbl(text, size);
        var row = Panel(label, bg ?? CardSoft, 14, 14);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onTap();
        row.GestureRecognizers.Add(tap);
        return (row, label);
    }

    /// <summary>可点击的卡片（整块内容）。</summary>
    public static Border TapPanel(View content, Action onTap, Color? bg = null, double pad = 14, double radius = 16)
    {
        var panel = Panel(content, bg ?? Card, pad, radius);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onTap();
        panel.GestureRecognizers.Add(tap);
        return panel;
    }

    public static Label SectionTitle(string text) => Lbl(text, 17, Fg, true);

    public static View Gap(double h) => new BoxView { Color = Colors.Transparent, HeightRequest = h };

    public static Grid Header(string title, Page page, bool back = true, string? right = null, Action? onRight = null)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            Padding = new Thickness(14, 10),
            ColumnSpacing = 10,
        };
        if (back)
        {
            var backBtn = Btn("← 返回", Bg2, Fg, () => page.Navigation.PopAsync(), 14);
            grid.Add(backBtn, 0);
        }
        grid.Add(Lbl(title, 17, Fg, true), 1);
        if (right != null)
        {
            var rb = Btn(right, CardSoft, Fg, onRight, 14);
            grid.Add(rb, 2);
        }
        return grid;
    }

    public static ScrollView Scroll(View content, double pad = 16) => new()
    {
        Content = new VerticalStackLayout { Padding = new Thickness(pad, 8, pad, 28), Spacing = 12, Children = { content } },
    };

    public static VerticalStackLayout Stack(double spacing = 12, params View[] views)
    {
        var s = new VerticalStackLayout { Spacing = spacing };
        foreach (var v in views)
            s.Children.Add(v);
        return s;
    }

    public static HorizontalStackLayout Row(double spacing = 10, params View[] views)
    {
        var s = new HorizontalStackLayout { Spacing = spacing };
        foreach (var v in views)
            s.Children.Add(v);
        return s;
    }

    public static Grid StatRow(params (string caption, string value, Color color)[] stats)
    {
        var g = new Grid { ColumnSpacing = 10 };
        for (var i = 0; i < stats.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var s = stats[i];
            var box = Stack(4, Lbl(s.value, 22, s.color, true), Lbl(s.caption, 12, Dim));
            var panel = Panel(box, Card, 12, 14);
            g.Add(panel, i);
        }
        return g;
    }

    /// <summary>回到首页（并可选地再打开一个新页面）。</summary>
    public static async Task GoHomeAsync(Page page, Page? push = null)
    {
        var stack = page.Navigation.NavigationStack.ToList();
        var homeIndex = stack.FindIndex(p => p is HomePage);
        if (homeIndex >= 0)
        {
            for (var i = stack.Count - 2; i > homeIndex; i--)
            {
                try
                {
                    page.Navigation.RemovePage(stack[i]);
                }
                catch
                {
                }
            }
            await page.Navigation.PopAsync();
        }
        else
        {
            await page.Navigation.PopToRootAsync();
        }
        if (push != null)
            await page.Navigation.PushAsync(push);
    }
}
