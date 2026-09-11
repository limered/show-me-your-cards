using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public class SessionsTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly WebApplicationFactory<Program> _factory;

    public SessionsTests()
    {
        _conn.Open();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<SmycDb>));
            s.Remove(d);
            s.AddDbContext<SmycDb>(o => o.UseSqlite(_conn));
        }));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _conn.Dispose();
    }

    private HttpClient Client => _factory.CreateClient();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<T> Read<T>(HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<T>(Json)
        ?? throw new InvalidOperationException("empty body");

    [Fact]
    public async Task create_returns_defaults_and_lists_as_open()
    {
        var create = await Client.PostAsJsonAsync("/api/sessions", new { });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var session = await Read<SessionDto>(create);
        Assert.Matches("^[a-z0-9]{8}$", session.Id);
        Assert.Equal("1,2,3,5,8,13,21", session.Deck);
        Assert.Equal("2m", session.Timer);

        var list = await Client.GetFromJsonAsync<List<SessionDto>>("/api/sessions", Json);
        Assert.Contains(list!, s => s.Id == session.Id);

        var snapshot = await Client.GetFromJsonAsync<SnapshotDto>($"/api/sessions/{session.Id}", Json);
        Assert.Equal(session.Id, snapshot!.Id);
        Assert.Empty(snapshot.Players);
    }

    [Fact]
    public async Task join_assigns_spot_name_token_and_snapshot_lists_players()
    {
        var session = await Read<SessionDto>(
            await Client.PostAsJsonAsync("/api/sessions", new { }));

        var first = await Read<JoinDto>(
            await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { }));
        var second = await Read<JoinDto>(
            await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { }));

        Assert.Equal(0, first.Spot);
        Assert.Equal(1, second.Spot);
        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Name, second.Name);

        var snapshot = await Client.GetFromJsonAsync<SnapshotDto>(
            $"/api/sessions/{session.Id}?token={second.Token}", Json);
        Assert.Equal(2, snapshot!.Players.Count);
        Assert.Equal(1, snapshot.YouSpot);
    }

    [Fact]
    public async Task thirteenth_join_is_rejected_as_table_full()
    {
        var session = await Read<SessionDto>(
            await Client.PostAsJsonAsync("/api/sessions", new { }));

        for (var i = 0; i < 12; i++)
            Assert.True((await Client.PostAsJsonAsync(
                $"/api/sessions/{session.Id}/join", new { })).IsSuccessStatusCode);

        var full = await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { });
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Equal("table_full", (await Read<ErrorDto>(full)).Error);
    }

    [Fact]
    public async Task join_with_taken_name_gets_a_fresh_auto_name()
    {
        var session = await Read<SessionDto>(
            await Client.PostAsJsonAsync("/api/sessions", new { }));
        var first = await Read<JoinDto>(
            await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { name = "Host" }));
        Assert.Equal("Host", first.Name);

        var second = await Read<JoinDto>(
            await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { name = "Host" }));
        Assert.NotEqual("Host", second.Name);
    }

    [Fact]
    public async Task unknown_session_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync("/api/sessions/nope1234")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.PostAsJsonAsync("/api/sessions/nope1234/join", new { })).StatusCode);
    }

    private sealed record SessionDto(string Id, string Deck, string Timer);
    private sealed record JoinDto(Guid Token, string Name, int Spot);
    private sealed record SeatDto(int Spot, string Name);
    private sealed record SnapshotDto(string Id, string Deck, string Timer, bool Closed, List<SeatDto> Players, int? YouSpot);
    private sealed record ErrorDto(string Error);
}
