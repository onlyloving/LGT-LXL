namespace LgtLxlVocab;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var root = new NavigationPage(new UnlockPage())
        {
            BarBackgroundColor = Ui.Bg,
            BarTextColor = Ui.Fg,
        };
        return new Window(root) { Title = AppState.Brand };
    }
}
