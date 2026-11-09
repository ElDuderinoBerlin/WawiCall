namespace WawiCallPro.App.Hosting;

internal static class ApplicationVersion
{
    internal static string Display => Format(typeof(MainForm).Assembly.GetName().Version);
    internal static string Format(Version? version) => version is null ? "?" : $"{version.Major}.{version.Minor}.{version.Revision}";
}
