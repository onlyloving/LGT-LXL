namespace LgtLxlVocab;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var root = new NavigationPage(new UnlockPage())
            {
                BarBackgroundColor = Ui.Bg,
                BarTextColor = Ui.Fg,
            };
            return new Window(root) { Title = AppState.Brand };
        }
        catch (Exception ex)
        {
            CrashReporter.Record(ex, "CreateWindow");
            return new Window(CrashReporter.BuildErrorPage()) { Title = AppState.Brand };
        }
    }
}
