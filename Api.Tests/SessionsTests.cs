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

    private async Task ExpireDeadline(string id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmycDb>();
        var session = await db.Sessions.SingleAsync(s => s.Id == id);
        session.DeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task round_deadline_auto_reveals_on_expiry()
    {
        var session = await Read<SessionDto>(
            await Client.PostAsJsonAsync("/api/sessions", new { timer = "30s" }));
        var a = await Read<JoinDto>(await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { }));
        var b = await Read<JoinDto>(await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { }));
        await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/play", new { token = a.Token, card = "3" });
        await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/play", new { token = b.Token, card = "5" });

        var running = await Snapshot(session.Id, a.Token);
        Assert.False(running.Revealed);
        Assert.NotNull(running.DeadlineUtc);

        await ExpireDeadline(session.Id);

        var revealed = await Snapshot(session.Id, a.Token);
        Assert.True(revealed.Revealed);
        Assert.Null(revealed.DeadlineUtc);
        Assert.Equal("3", revealed.Players.Single(p => p.Spot == a.Spot).Card);
        Assert.Equal("5", revealed.Result!.Card);
    }

    [Fact]
    public async Task timer_off_leaves_no_deadline_and_reveals_only_manually()
    {
        var session = await Read<SessionDto>(
            await Client.PostAsJsonAsync("/api/sessions", new { timer = "off" }));
        var a = await Read<JoinDto>(await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/join", new { }));
        await Client.PostAsJsonAsync($"/api/sessions/{session.Id}/play", new { token = a.Token, card = "8" });

        var running = await Snapshot(session.Id, a.Token);
        Assert.False(running.Revealed);
        Assert.Null(running.DeadlineUtc);

        await Client.PostAsync($"/api/sessions/{session.Id}/reveal", null);

        var revealed = await Snapshot(session.Id, a.Token);
        Assert.True(revealed.Revealed);
        Assert.Equal("8", revealed.Result!.Card);
    }

    [Fact]
    public async Task post_reveal_vote_change_live_recounts_with_no_timer()
    {
        var (id, a, b) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "3" });
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = b.Token, card = "5" });
        await Client.PostAsync($"/api/sessions/{id}/reveal", null);

        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "8" });

        var snap = await Snapshot(id, a.Token);
        Assert.True(snap.Revealed);
        Assert.Null(snap.DeadlineUtc);
        Assert.Equal("8", snap.Players.Single(p => p.Spot == a.Spot).Card);
        Assert.Equal("8", snap.Result!.Card);
        Assert.Equal(6.5, snap.Result.Mean);
    }

    [Fact]
    public async Task reconfig_takes_effect_next_round()
    {
        var (id, a, _) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "5" });
        var before = await Snapshot(id, a.Token);
        Assert.NotNull(before.DeadlineUtc);

        var config = await Client.PostAsJsonAsync($"/api/sessions/{id}/config",
            new { deck = "1,2,3,5,8", timer = "5m" });
        Assert.True(config.IsSuccessStatusCode);
        var updated = await Read<SessionDto>(config);
        Assert.Equal("1,2,3,5,8", updated.Deck);
        Assert.Equal("5m", updated.Timer);

        var midRound = await Snapshot(id, a.Token);
        Assert.Equal("1,2,3,5,8", midRound.Deck);
        Assert.Equal("5m", midRound.Timer);
        Assert.Equal("5", midRound.YouCard);
        Assert.NotNull(midRound.DeadlineUtc);
        Assert.True(Math.Abs((midRound.DeadlineUtc!.Value - before.DeadlineUtc!.Value).TotalSeconds) < 2);

        await Client.PostAsync($"/api/sessions/{id}/round", null);

        var next = await Snapshot(id, a.Token);
        Assert.False(next.Revealed);
        Assert.Null(next.YouCard);
        Assert.Equal("1,2,3,5,8", next.Deck);
        Assert.NotNull(next.DeadlineUtc);
        Assert.True((next.DeadlineUtc.Value - before.DeadlineUtc.Value).TotalSeconds > 60);
    }

    [Fact]
    public async Task reconfig_rejects_unknown_timer()
    {
        var (id, _, _) = await SeatTwo();
        var bad = await Client.PostAsJsonAsync($"/api/sessions/{id}/config", new { timer = "9m" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task snapshot_resync_returns_full_state_for_token()
    {
        var (id, a, b) = await SeatTwo();
        await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "5" });

        var mine = await Snapshot(id, a.Token);
        Assert.Equal(id, mine.Id);
        Assert.Equal("1,2,3,5,8,13,21", mine.Deck);
        Assert.Equal("2m", mine.Timer);
        Assert.False(mine.Closed);
        Assert.False(mine.Revealed);
        Assert.NotNull(mine.DeadlineUtc);
        Assert.Equal(2, mine.Players.Count);
        Assert.Equal(a.Spot, mine.YouSpot);
        Assert.Equal("5", mine.YouCard);
        Assert.Null(mine.Result);

        var stranger = await Client.GetFromJsonAsync<SnapshotDto>($"/api/sessions/{id}", Json);
        Assert.Null(stranger!.YouSpot);
        Assert.Null(stranger.YouCard);
        Assert.Equal(2, stranger.Players.Count);
        Assert.Equal(b.Spot, stranger.Players.Single(p => p.Spot == b.Spot).Spot);
    }

    [Fact]
    public async Task closed_session_leaves_list_but_stays_readonly_by_link()
    {
        var (id, a, _) = await SeatTwo();

        Assert.True((await Client.PostAsync($"/api/sessions/{id}/close", null)).IsSuccessStatusCode);

        var list = await Client.GetFromJsonAsync<List<SessionDto>>("/api/sessions", Json);
        Assert.DoesNotContain(list!, s => s.Id == id);

        var snap = await Snapshot(id, a.Token);
        Assert.True(snap.Closed);

        var join = await Client.PostAsJsonAsync($"/api/sessions/{id}/join", new { });
        Assert.Equal(HttpStatusCode.Conflict, join.StatusCode);
        Assert.Equal("closed", (await Read<ErrorDto>(join)).Error);

        var play = await Client.PostAsJsonAsync($"/api/sessions/{id}/play", new { token = a.Token, card = "5" });
        Assert.Equal(HttpStatusCode.Conflict, play.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsync($"/api/sessions/{id}/reveal", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsync($"/api/sessions/{id}/round", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await Client.PostAsJsonAsync($"/api/sessions/{id}/config", new { timer = "off" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await Client.PostAsJsonAsync($"/api/sessions/{id}/rename", new { token = a.Token, name = "Zed" })).StatusCode);
    }

    private sealed record SessionDto(string Id, string Deck, string Timer);
    private sealed record JoinDto(Guid Token, string Name, int Spot);
    private sealed record SeatDto(int Spot, string Name, bool Played, string? Card);
    private sealed record ResultDto(string Card, double Mean, List<int> Spots);
    private sealed record SnapshotDto(string Id, string Deck, string Timer, bool Closed, bool Revealed, DateTime? DeadlineUtc, List<SeatDto> Players, int? YouSpot, string? YouCard, ResultDto? Result);
    private sealed record ErrorDto(string Error);
}
