using System.Runtime.CompilerServices;

/// <summary>
/// QuestPDF 2026.x calls Environment.Exit(0) from its [ModuleInitializer]
/// if License is not set. Since module initializers fire before Program.cs,
/// we hook AssemblyLoad here (also a module initializer, but without any
/// direct QuestPDF type reference) so the license is set the instant
/// QuestPDF.dll is loaded — before its own initializer checks it.
/// </summary>
internal static class QuestPdfInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, e) =>
        {
            if (e.LoadedAssembly.GetName().Name != "QuestPDF") return;

            var settingsType = e.LoadedAssembly.GetType("QuestPDF.Settings");
            var licenseType = e.LoadedAssembly.GetType("QuestPDF.Infrastructure.LicenseType");
            if (settingsType == null || licenseType == null) return;

            var prop = settingsType.GetProperty("License");
            if (prop == null) return;

            prop.SetValue(null, Enum.Parse(licenseType, "Community"));
        };
    }
}