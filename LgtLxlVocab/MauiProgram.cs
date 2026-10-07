namespace LgtLxlVocab;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        CrashReporter.Install();
        try
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            return builder.Build();
        }
        catch (Exception ex)
        {
            CrashReporter.Record(ex, "CreateMauiApp");
            throw;
        }
    }
}
