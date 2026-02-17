using Tomlet.Attributes;

namespace GitAutoSync.Core.Config;

public class RepoConfig
{
  [TomlProperty("path")] public string? Path { get; set; }
  [TomlProperty("name")] public string? Name { get; set; }
  [TomlProperty("hosts")] public List<string> Hosts { get; set; } = new();
}