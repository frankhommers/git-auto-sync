using System.Text;

namespace GitAutoSync.GUI.Services;

/// <summary>
/// Parses "Open with" command templates such as <c>open -a Fork "{path}"</c>.
/// The template is split into tokens (double quotes group tokens) and the
/// <c>{path}</c> placeholder is substituted after tokenization, so paths with
/// spaces never need extra quoting.
/// </summary>
public static class OpenWithCommandParser
{
  public const string PathPlaceholder = "{path}";

  public static bool TryParse(string template, string repoPath, out string fileName, out List<string> arguments)
  {
    fileName = "";
    arguments = new List<string>();

    List<string> tokens = Tokenize(template);
    if (tokens.Count == 0)
    {
      return false;
    }

    for (int i = 0; i < tokens.Count; i++)
    {
      tokens[i] = tokens[i].Replace(PathPlaceholder, repoPath);
    }

    fileName = tokens[0];
    arguments = tokens.Skip(1).ToList();
    return true;
  }

  /// <summary>
  /// Builds a sensible default command template for a picked application:
  /// macOS .app bundles use <c>open -a</c>, plain executables are invoked directly.
  /// </summary>
  public static string BuildDefaultCommand(string applicationPath)
  {
    string normalized = applicationPath.TrimEnd('/', '\\');

    if (normalized.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
    {
      // "{path}" as document: handled while the app is already running.
      // --args "{path}": passed as argv when the app launches fresh.
      return $"open -a \"{normalized}\" \"{PathPlaceholder}\" --args \"{PathPlaceholder}\"";
    }

    return $"\"{normalized}\" \"{PathPlaceholder}\"";
  }

  /// <summary>Derives a display name from an application path (e.g. "/Applications/Fork.app" -> "Fork").</summary>
  public static string DeriveAppName(string applicationPath)
  {
    string trimmed = applicationPath.TrimEnd('/', '\\');
    int lastSeparator = trimmed.LastIndexOfAny(new[] {'/', '\\'});
    string fileName = lastSeparator >= 0 ? trimmed[(lastSeparator + 1)..] : trimmed;

    int lastDot = fileName.LastIndexOf('.');
    return lastDot > 0 ? fileName[..lastDot] : fileName;
  }

  private static List<string> Tokenize(string template)
  {
    List<string> tokens = new();
    StringBuilder current = new();
    bool inQuotes = false;
    bool tokenStarted = false;

    foreach (char c in template)
    {
      if (c == '"')
      {
        inQuotes = !inQuotes;
        tokenStarted = true;
        continue;
      }

      if (!inQuotes && char.IsWhiteSpace(c))
      {
        if (tokenStarted)
        {
          tokens.Add(current.ToString());
          current.Clear();
          tokenStarted = false;
        }

        continue;
      }

      current.Append(c);
      tokenStarted = true;
    }

    if (tokenStarted)
    {
      tokens.Add(current.ToString());
    }

    return tokens;
  }
}
