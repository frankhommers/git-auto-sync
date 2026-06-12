using GitAutoSync.GUI.Services;

namespace GitAutoSync.Tests;

public class OpenWithCommandTests
{
    [Fact]
    public void TryParse_SimpleCommand_SplitsExecutableAndSubstitutesPath()
    {
        bool ok = OpenWithCommandParser.TryParse("fork {path}", "/tmp/repo", out string fileName, out List<string> arguments);

        Assert.True(ok);
        Assert.Equal("fork", fileName);
        Assert.Equal(new[] { "/tmp/repo" }, arguments);
    }

    [Fact]
    public void TryParse_QuotedExecutableWithSpaces_KeptAsSingleToken()
    {
        bool ok = OpenWithCommandParser.TryParse(
            "\"/Applications/My App.app/Contents/MacOS/app\" \"{path}\"",
            "/Users/test/My Repo",
            out string fileName,
            out List<string> arguments);

        Assert.True(ok);
        Assert.Equal("/Applications/My App.app/Contents/MacOS/app", fileName);
        Assert.Equal(new[] { "/Users/test/My Repo" }, arguments);
    }

    [Fact]
    public void TryParse_PlaceholderInsideLargerToken_IsSubstituted()
    {
        bool ok = OpenWithCommandParser.TryParse("code --folder-uri=file://{path}", "/tmp/repo", out string fileName, out List<string> arguments);

        Assert.True(ok);
        Assert.Equal("code", fileName);
        Assert.Equal(new[] { "--folder-uri=file:///tmp/repo" }, arguments);
    }

    [Fact]
    public void TryParse_MacOsOpenWithAppName_ProducesExpectedArguments()
    {
        bool ok = OpenWithCommandParser.TryParse("open -a Fork \"{path}\"", "/Users/test/repo", out string fileName, out List<string> arguments);

        Assert.True(ok);
        Assert.Equal("open", fileName);
        Assert.Equal(new[] { "-a", "Fork", "/Users/test/repo" }, arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_EmptyOrWhitespaceTemplate_ReturnsFalse(string template)
    {
        bool ok = OpenWithCommandParser.TryParse(template, "/tmp/repo", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void BuildDefaultCommand_MacOsAppBundle_UsesOpenDashA_WithDocumentAndArgs()
    {
        // "{path}" as document works when the app is already running;
        // --args "{path}" passes argv on a fresh launch.
        string command = OpenWithCommandParser.BuildDefaultCommand("/Applications/Fork.app");

        Assert.Equal("open -a \"/Applications/Fork.app\" \"{path}\" --args \"{path}\"", command);
    }

    [Fact]
    public void BuildDefaultCommand_AppBundleWithTrailingSlash_UsesOpenDashA()
    {
        // AppleScript's "POSIX path of" returns directory paths with a trailing slash.
        string command = OpenWithCommandParser.BuildDefaultCommand("/Applications/Fork.app/");

        Assert.Equal("open -a \"/Applications/Fork.app\" \"{path}\" --args \"{path}\"", command);
    }

    [Fact]
    public void DeriveAppName_AppBundleWithTrailingSlash_StripsSlashAndExtension()
    {
        string name = OpenWithCommandParser.DeriveAppName("/Applications/Fork.app/");

        Assert.Equal("Fork", name);
    }

    [Fact]
    public void BuildDefaultCommand_PlainExecutable_QuotesExecutableAndAppendsPath()
    {
        string command = OpenWithCommandParser.BuildDefaultCommand("/usr/local/bin/fork");

        Assert.Equal("\"/usr/local/bin/fork\" \"{path}\"", command);
    }

    [Fact]
    public void DeriveAppName_MacOsAppBundle_StripsExtension()
    {
        string name = OpenWithCommandParser.DeriveAppName("/Applications/Fork.app");

        Assert.Equal("Fork", name);
    }

    [Fact]
    public void DeriveAppName_WindowsExecutable_StripsExtension()
    {
        string name = OpenWithCommandParser.DeriveAppName(@"C:\Program Files\Fork\Fork.exe");

        Assert.Equal("Fork", name);
    }
}
