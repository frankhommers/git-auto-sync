using GitAutoSync.GUI.Models;
using GitAutoSync.GUI.Services;

namespace GitAutoSync.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Parse_OldDictionaryFormat_MigratesThemeMode()
    {
        string oldJson = """{"themeMode":"Dark"}""";

        AppSettings settings = AppSettingsStore.Parse(oldJson);

        Assert.Equal("Dark", settings.ThemeMode);
        Assert.Empty(settings.OpenWithApps);
    }

    [Fact]
    public void Parse_NewFormat_LoadsOpenWithApps()
    {
        string json = """
            {"themeMode":"Light","openWithApps":[
                {"name":"Fork","command":"open -a Fork \"{path}\""},
                {"name":"VS Code","command":"code \"{path}\""}
            ]}
            """;

        AppSettings settings = AppSettingsStore.Parse(json);

        Assert.Equal("Light", settings.ThemeMode);
        Assert.Equal(2, settings.OpenWithApps.Count);
        Assert.Equal("Fork", settings.OpenWithApps[0].Name);
        Assert.Equal("open -a Fork \"{path}\"", settings.OpenWithApps[0].Command);
    }

    [Fact]
    public void Parse_MoreThanFiveApps_TruncatesToFive()
    {
        string json = """
            {"openWithApps":[
                {"name":"A","command":"a {path}"},
                {"name":"B","command":"b {path}"},
                {"name":"C","command":"c {path}"},
                {"name":"D","command":"d {path}"},
                {"name":"E","command":"e {path}"},
                {"name":"F","command":"f {path}"}
            ]}
            """;

        AppSettings settings = AppSettingsStore.Parse(json);

        Assert.Equal(AppSettingsStore.MaxOpenWithApps, settings.OpenWithApps.Count);
        Assert.Equal("E", settings.OpenWithApps[^1].Name);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsDefaults()
    {
        AppSettings settings = AppSettingsStore.Parse("not json at all");

        Assert.Null(settings.ThemeMode);
        Assert.Empty(settings.OpenWithApps);
    }

    [Fact]
    public void SerializeThenParse_RoundTripsAllProperties()
    {
        AppSettings original = new()
        {
            ThemeMode = "Dark",
            OpenWithApps =
            {
                new OpenWithApp { Name = "Fork", Command = "open -a Fork \"{path}\"" },
            },
        };

        string json = AppSettingsStore.Serialize(original);
        AppSettings parsed = AppSettingsStore.Parse(json);

        Assert.Equal("Dark", parsed.ThemeMode);
        Assert.Single(parsed.OpenWithApps);
        Assert.Equal("Fork", parsed.OpenWithApps[0].Name);
        Assert.Equal("open -a Fork \"{path}\"", parsed.OpenWithApps[0].Command);
    }
}
