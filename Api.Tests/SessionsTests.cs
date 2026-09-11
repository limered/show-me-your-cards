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

    private async Task<(string Id, JoinDto A, JoinDto B)> SeatTwo(string? deck = null)
    {
        var session = await Read<SessionDto>(
            await Client.PostAsJsonAsync("/api/sessions", new { deck }));
        var a = await Read<JoinDto>(await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { }));
        var b = await Read<JoinDto>(await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { }));
        return (session.Id, a, b);
    }

    private Task<SnapshotDto> Snapshot(string id, Guid token) =>
        Client.GetFromJsonAsync<SnapshotDto>($"/api/sessions/{id}?token={token}", Json)!;

    [Fact]
    public async Task played_cards_stay_hidden_until_reveal()
    {
        var (id, a, b) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "5" });

        var beforeReveal = await Snapshot(id, b.Token);
        var seatA = beforeReveal.Players.Single(p => p.Spot == a.Spot);
        Assert.True(seatA.Played);
        Assert.Null(seatA.Card);
        Assert.False(beforeReveal.Revealed);

        var mine = await Snapshot(id, a.Token);
        Assert.Equal("5", mine.YouCard);
    }

    [Fact]
    public async Task reveal_shows_cards_and_averages_up_to_next_deck_card()
    {
        var (id, a, b) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "3" });
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = b.Token, card = "5" });
        await Client.PostAsync($"/api/sessions/{id}/reveal", null);

        var snap = await Snapshot(id, a.Token);
        Assert.True(snap.Revealed);
        Assert.Equal("3", snap.Players.Single(p => p.Spot == a.Spot).Card);
        Assert.Equal("5", snap.Result!.Card);
        Assert.Equal(4.0, snap.Result.Mean);
        Assert.Equal(new[] { b.Spot }, snap.Result.Spots);
    }

    [Fact]
    public async Task non_numeric_cards_are_excluded_and_empty_has_no_result()
    {
        var (id, a, b) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "?" });
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = b.Token, card = "coffee" });
        await Client.PostAsync($"/api/sessions/{id}/reveal", null);

        var snap = await Snapshot(id, a.Token);
        Assert.Null(snap.Result);
    }

    [Fact]
    public async Task new_round_clears_cards_and_reveal()
    {
        var (id, a, b) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "5" });
        await Client.PostAsync($"/api/sessions/{id}/reveal", null);
        await Client.PostAsync($"/api/sessions/{id}/round", null);

        var snap = await Snapshot(id, a.Token);
        Assert.False(snap.Revealed);
        Assert.All(snap.Players, p => Assert.False(p.Played));
        Assert.Null(snap.YouCard);
    }

    [Fact]
    public async Task replaying_same_card_unvotes()
    {
        var (id, a, _) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "5" });
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "5" });

        var snap = await Snapshot(id, a.Token);
        Assert.Null(snap.YouCard);
    }

    [Fact]
    public async Task rename_takes_effect_and_collision_gets_fresh_name()
    {
        var (id, a, b) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/rename", new { token = a.Token, name = "Ada" });

        var snap = await Snapshot(id, a.Token);
        Assert.Equal("Ada", snap.Players.Single(p => p.Spot == a.Spot).Name);

        await Client.PostAsJsonAsync($"/api/sessions/{id}/rename", new { token = b.Token, name = "Ada" });
        var snap2 = await Snapshot(id, b.Token);
        Assert.NotEqual("Ada", snap2.Players.Single(p => p.Spot == b.Spot).Name);
    }

    [Fact]
    public async Task token_reclaims_same_seat_on_reconnect()
    {
        var (id, a, _) = await SeatTwo();
        var reconnect = await Snapshot(id, a.Token);
        Assert.Equal(a.Spot, reconnect.YouSpot);
    }

    private sealed record SessionDto(string Id, string Deck, string Timer);
    private sealed record JoinDto(Guid Token, string Name, int Spot);
    private sealed record SeatDto(int Spot, string Name, bool Played, string? Card);
    private sealed record ResultDto(string Card, double Mean, List<int> Spots);
    private sealed record SnapshotDto(string Id, string Deck, string Timer, bool Closed, bool Revealed, List<SeatDto> Players, int? YouSpot, string? YouCard, ResultDto? Result);
    private sealed record ErrorDto(string Error);
}
