using Tomlet.Attributes;

namespace GitAutoSync.Core.Config;

public class Config
{
  [TomlProperty("repo")] public List<RepoConfig> Repos { get; set; } = new();
}