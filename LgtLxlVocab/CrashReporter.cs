using System.Text;

namespace LgtLxlVocab;

/// <summary>
/// 兜底诊断：把启动/运行时的异常记录下来（屏幕 + 文件），
/// 这样即使闪退也能知道到底哪里炸了。
/// </summary>
public static class CrashReporter
{
    static readonly object Gate = new();

    public static string LastReport { get; private set; } = "";
    public static bool HasError => LastReport.Length > 0;

    public static string InternalLogPath
    {
        get
        {
            try
            {
                return Path.Combine(FileSystem.AppDataDirectory, "lgtlxl-crash.log");
            }
            catch
            {
                return "";
            }
        }
    }

    /// <summary>手机上「文件管理 → Android/data/com.lgtlxl.vocab/files」里能看到的位置。</summary>
    public static string ExternalLogPath
    {
        get
        {
            try
            {
                var dir = Platform.AppContext.GetExternalFilesDir(null)?.AbsolutePath;
                return string.IsNullOrEmpty(dir) ? "" : Path.Combine(dir, "lgtlxl-crash.log");
            }
            catch
            {
                return "";
            }
        }
    }

    public static void Install()
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Record(e.ExceptionObject as Exception, "AppDomain");
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Record(e.Exception, "Task");
                e.SetObserved();
            };
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            {
                Record(e.Exception, "Android(Mono)");
                e.Handled = true;      // 先别退出，好让用户看到错误信息
            };
            var previous = Java.Lang.Thread.DefaultUncaughtExceptionHandler;
            Java.Lang.Thread.DefaultUncaughtExceptionHandler = new JavaCrashHandler(previous);
        }
        catch
        {
        }
    }

    public static void Record(Exception? ex, string source)
    {
        if (ex == null)
            return;
        Write(source + ": " + ex.GetType().FullName + ": " + ex.Message + "\n" + ex.StackTrace, source);
    }

    public static void RecordJava(Java.Lang.Throwable? ex, string source)
    {
        if (ex == null)
            return;
        Write(source + ": " + ex.Class?.Name + ": " + ex.Message + "\n" + ex.StackTrace, source);
    }

    static void Write(string text, string source)
    {
        var report = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + text;
        lock (Gate)
        {
            LastReport = report;
            foreach (var path in new[] { ExternalLogPath, InternalLogPath })
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                try
                {
                    File.AppendAllText(path, report + "\n\n", Encoding.UTF8);
                }
                catch
                {
                }
            }
        }
    }

    public static Page BuildErrorPage()
    {
        var page = new ContentPage { BackgroundColor = Ui.Bg };
        var text = Ui.Lbl(LastReport.Length > 0 ? LastReport : "（没有记录到具体错误）", 12, Ui.Fg);
        var body = new VerticalStackLayout
        {
            Padding = new Thickness(16, 20, 16, 28),
            Spacing = 12,
            Children =
            {
                Ui.Lbl("LGT-LXL 启动出错了", 20, Ui.Bad, true),
                Ui.Lbl("把下面这段内容发给我，我就能定位问题：", 14, Ui.Dim),
                Ui.Panel(new ScrollView { Content = text, HeightRequest = 420 }, Ui.Card, 12, 12),
                Ui.Lbl("日志文件：" + ExternalLogPath, 11, Ui.Dim),
                Ui.Btn("重新加载应用", Ui.Accent, Colors.White, () => _ = AppReloadAsync(), 16),
            },
        };
        page.Content = new ScrollView { Content = body };
        NavigationPage.SetHasNavigationBar(page, false);
        return page;
    }

    static async Task AppReloadAsync()
    {
        try
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window != null)
                window.Page = new NavigationPage(new UnlockPage());
        }
        catch (Exception ex)
        {
            Record(ex, "ReloadAsync");
        }
        await Task.CompletedTask;
    }

}

/// <summary>接管 Java 线程的未捕获异常，先记录下来，不立刻杀掉进程。</summary>
public class JavaCrashHandler : Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler
{
    readonly Java.Lang.Thread.IUncaughtExceptionHandler? _previous;

    public JavaCrashHandler(Java.Lang.Thread.IUncaughtExceptionHandler? previous)
    {
        _previous = previous;
    }

    public void UncaughtException(Java.Lang.Thread? t, Java.Lang.Throwable? e)
    {
        CrashReporter.RecordJava(e, "Java");
        // 不调用上一个处理器：先让 App 活着，方便看到错误信息
    }
}
