namespace UEFontTool;

public static class AppBranding
{
    public static Icon CreateIcon()
    {
        using var stream=typeof(AppBranding).Assembly.GetManifestResourceStream("UEFontTool.Resources.app.ico")!;
        return new Icon(stream);
    }
    public static Bitmap CreateLogo()
    {
        using var stream=typeof(AppBranding).Assembly.GetManifestResourceStream("UEFontTool.Resources.icon.png")!;
        using var source=Image.FromStream(stream);
        return new Bitmap(source);
    }
}
