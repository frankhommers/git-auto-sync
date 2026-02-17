using System.Text.Json;
using GitAutoSync.GUI;
using GitAutoSync.GUI.Models;
using DaemonModels = GitAutoSync.Daemon.Api.Models;

namespace GitAutoSync.Tests;

public class JsonSerializationTests
{
    [Fact]
    public void GUI_AddRepositoryRequest_SerializesToJson_WithExpectedShape()
    {
        // The GUI serializes AddRepositoryRequest using AppJsonContext (source-generated, camelCase)
        var request = new AddRepositoryRequest("my-repo", "/Users/test/my-repo");

        var json = JsonSerializer.Serialize(request, AppJsonContext.Default.AddRepositoryRequest);

        // Verify it produces valid JSON with expected property names
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("name", out var name), $"JSON should have 'name' property. Actual JSON: {json}");
        Assert.True(root.TryGetProperty("path", out var path), $"JSON should have 'path' property. Actual JSON: {json}");
        Assert.Equal("my-repo", name.GetString());
        Assert.Equal("/Users/test/my-repo", path.GetString());
    }

    [Fact]
    public void Daemon_CanDeserialize_GUI_SerializedAddRepositoryRequest()
    {
        // Serialize from GUI side
        var guiRequest = new AddRepositoryRequest("my-repo", "/Users/test/my-repo");
        var json = JsonSerializer.Serialize(guiRequest, AppJsonContext.Default.AddRepositoryRequest);

        // Deserialize on daemon side (ASP.NET Core defaults: case-insensitive, camelCase)
        var daemonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var daemonRequest = JsonSerializer.Deserialize<DaemonModels.AddRepositoryRequest>(json, daemonOptions);

        Assert.NotNull(daemonRequest);
        Assert.Equal("my-repo", daemonRequest!.Name);
        Assert.Equal("/Users/test/my-repo", daemonRequest.Path);
    }

    [Fact]
    public void GUI_RepositoryDto_DeserializesFromDaemonResponse()
    {
        // Daemon sends back RepositoryInfo serialized with ASP.NET Core defaults (camelCase)
        var daemonJson = """{"id":"abc-123","name":"my-repo","path":"/Users/test/my-repo","isRunning":false,"lastActivity":null,"status":"Stopped"}""";

        var dto = JsonSerializer.Deserialize(daemonJson, AppJsonContext.Default.RepositoryDto);

        Assert.NotNull(dto);
        Assert.Equal("abc-123", dto!.Id);
        Assert.Equal("my-repo", dto.Name);
        Assert.Equal("/Users/test/my-repo", dto.Path);
        Assert.False(dto.IsRunning);
        Assert.Equal("Stopped", dto.Status);
    }

}
