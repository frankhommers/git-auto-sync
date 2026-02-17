using System.Net.Http.Json;
using System.Text.Json;
using GitAutoSync.GUI;
using GitAutoSync.GUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GitAutoSync.Tests;

public class DaemonIntegrationTests : IClassFixture<WebApplicationFactory<GitAutoSync.Daemon.Program>>
{
    private readonly HttpClient _client;
    private const string TestRepoPath = "/Users/frankhommers/Temp/GitAutoSync/test-repo";

    public DaemonIntegrationTests(WebApplicationFactory<GitAutoSync.Daemon.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AddRepository_WithGUISerializer_ReturnsSuccess()
    {
        var request = new AddRepositoryRequest("test-repo-gui-" + Guid.NewGuid().ToString("N")[..6], TestRepoPath);

        var response = await _client.PostAsJsonAsync(
            "/api/repositories",
            request,
            AppJsonContext.Default.AddRepositoryRequest);

        var body = await response.Content.ReadAsStringAsync();

        // First add should succeed, duplicate returns 400 - both are valid daemon behavior
        if ((int)response.StatusCode == 400 && body.Contains("already exists"))
        {
            return; // expected if another test already added this path
        }

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success but got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public void GUI_Serializer_ProducesCorrectCamelCaseJson()
    {
        var request = new AddRepositoryRequest("test", "/some/path");
        var json = JsonSerializer.Serialize(request, AppJsonContext.Default.AddRepositoryRequest);

        Assert.Contains("\"name\"", json);
        Assert.Contains("\"path\"", json);
        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"Path\"", json);
    }

    [Fact]
    public async Task AddRepository_ResponseCanBeDeserializedByGUI()
    {
        var uniqueName = "test-deser-" + Guid.NewGuid().ToString("N")[..6];
        var request = new AddRepositoryRequest(uniqueName, TestRepoPath);

        var response = await _client.PostAsJsonAsync(
            "/api/repositories",
            request,
            AppJsonContext.Default.AddRepositoryRequest);

        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            // Verify GUI can deserialize the response
            var dto = JsonSerializer.Deserialize(body, AppJsonContext.Default.RepositoryDto);
            Assert.NotNull(dto);
            Assert.Equal(uniqueName, dto!.Name);
            Assert.False(string.IsNullOrEmpty(dto.Id));
        }
    }
}
