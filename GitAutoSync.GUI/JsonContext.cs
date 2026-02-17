using System.Text.Json.Serialization;
using GitAutoSync.GUI.Models;
using GitAutoSync.GUI.Services;

namespace GitAutoSync.GUI;

[JsonSerializable(typeof(RepositoryDto))]
[JsonSerializable(typeof(List<RepositoryDto>))]
[JsonSerializable(typeof(AddRepositoryRequest))]
[JsonSerializable(typeof(LoadConfigRequest))]
[JsonSerializable(typeof(SaveConfigRequest))]
[JsonSerializable(typeof(DaemonEvent))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonContext : JsonSerializerContext;