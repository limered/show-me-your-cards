using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (dbUrl is { Length: > 0 })
    builder.Services.AddDbContext<SmycDb>(o => o.UseNpgsql(ToNpgsql(dbUrl)));
else
    builder.Services.AddDbContext<SmycDb>(o => o.UseSqlite("Data Source=app.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmycDb>();
    if (db.Database.IsNpgsql())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/sessions", async (SmycDb db, CreateSession? body) =>
{
    var id = GameSetup.NewSessionId();
    while (await db.Sessions.AnyAsync(s => s.Id == id))
        id = GameSetup.NewSessionId();
    var session = new Session
    {
        Id = id,
        Deck = GameSetup.ResolveDeck(body?.Deck),
        TimerSetting = GameSetup.ResolveTimer(body?.Timer),
    };
    db.Sessions.Add(session);
    await db.SaveChangesAsync();
    return Results.Created($"/api/sessions/{id}",
        new SessionDto(session.Id, session.Deck, session.TimerSetting));
});

app.MapGet("/api/sessions", async (SmycDb db) =>
    await db.Sessions.Where(s => !s.Closed)
        .Select(s => new SessionDto(s.Id, s.Deck, s.TimerSetting, s.Players.Count))
        .ToListAsync());

app.MapGet("/api/sessions/{id}", async (SmycDb db, string id, Guid? token) =>
{
    var session = await db.Sessions.Include(s => s.Players).SingleOrDefaultAsync(s => s.Id == id);
    if (session is null)
        return Results.NotFound();
    var me = token is { } t ? session.Players.SingleOrDefault(p => p.Token == t) : null;
    var seats = session.Players.OrderBy(p => p.Spot)
        .Select(p => new SeatDto(p.Spot, p.Name, p.Card is not null,
            session.Revealed ? p.Card : null))
        .ToList();
    var result = session.Revealed
        ? GameSetup.Result(session.Deck, session.Players.Select(p => p.Card).OfType<string>())
        : null;
    ResultDto? resultDto = result is { } r
        ? new ResultDto(r.Card, r.Mean,
            session.Players.Where(p => p.Card == r.Card).Select(p => p.Spot).OrderBy(s => s).ToList())
        : null;
    return Results.Ok(new SnapshotDto(session.Id, session.Deck, session.TimerSetting,
        session.Closed, session.Revealed, seats, me?.Spot, me?.Card, resultDto));
});

app.MapPost("/api/sessions/{id}/play", async (SmycDb db, string id, PlayRequest body) =>
{
    var player = await db.Players.SingleOrDefaultAsync(p => p.SessionId == id && p.Token == body.Token);
    if (player is null)
        return Results.NotFound();
    var card = body.Card?.Trim();
    player.Card = player.Card == card ? null : string.IsNullOrEmpty(card) ? null : card;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/api/sessions/{id}/rename", async (SmycDb db, string id, RenameRequest body) =>
{
    var session = await db.Sessions.Include(s => s.Players).SingleOrDefaultAsync(s => s.Id == id);
    var player = session?.Players.SingleOrDefault(p => p.Token == body.Token);
    if (session is null || player is null)
        return Results.NotFound();
    var taken = session.Players.Where(p => p.Token != body.Token).Select(p => p.Name).ToList();
    var wanted = body.Name?.Trim();
    player.Name = string.IsNullOrEmpty(wanted) || taken.Contains(wanted)
        ? GameSetup.PickName(taken)
        : wanted;
    await db.SaveChangesAsync();
    return Results.Ok(new SeatDto(player.Spot, player.Name, player.Card is not null, null));
});

app.MapPost("/api/sessions/{id}/reveal", async (SmycDb db, string id) =>
{
    var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == id);
    if (session is null)
        return Results.NotFound();
    session.Revealed = true;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/api/sessions/{id}/round", async (SmycDb db, string id) =>
{
    var session = await db.Sessions.Include(s => s.Players).SingleOrDefaultAsync(s => s.Id == id);
    if (session is null)
        return Results.NotFound();
    session.Revealed = false;
    foreach (var p in session.Players)
        p.Card = null;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/api/sessions/{id}/join", async (SmycDb db, string id, JoinRequest? body) =>
{
    var session = await db.Sessions.Include(s => s.Players).SingleOrDefaultAsync(s => s.Id == id);
    if (session is null)
        return Results.NotFound();
    if (session.Closed)
        return Results.Conflict(new ErrorDto("closed"));
    if (session.Players.Count >= GameSetup.TableSize)
        return Results.Conflict(new ErrorDto("table_full"));
    var taken = session.Players.Select(p => p.Name).ToList();
    var wanted = body?.Name?.Trim();
    var name = string.IsNullOrEmpty(wanted) || taken.Contains(wanted)
        ? GameSetup.PickName(taken)
        : wanted;
    var spot = Enumerable.Range(0, GameSetup.TableSize).First(i => session.Players.All(p => p.Spot != i));
    var player = new Player
    {
        Id = Guid.NewGuid(),
        SessionId = id,
        Token = Guid.NewGuid(),
        Name = name,
        Spot = spot,
        LastSeen = DateTime.UtcNow,
    };
    db.Players.Add(player);
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        return Results.Conflict(new ErrorDto("table_full"));
    }
    return Results.Ok(new JoinDto(player.Token, player.Name, player.Spot));
});

app.MapFallbackToFile("index.html");
app.Run();

static string ToNpgsql(string url)
{
    var uri = new Uri(url);
    var credentials = uri.UserInfo.Split(':', 2);
    var port = uri.Port == -1 ? 5432 : uri.Port;
    var password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "";
    return $"Host={uri.Host};Port={port};Username={Uri.UnescapeDataString(credentials[0])};Password={password};" +
           $"Database={uri.AbsolutePath.Trim('/')};SSL Mode=Require;Trust Server Certificate=true";
}

public partial class Program;

record CreateSession(string? Deck, string? Timer);
record JoinRequest(string? Name);
record PlayRequest(Guid Token, string? Card);
record RenameRequest(Guid Token, string? Name);
record SessionDto(string Id, string Deck, string Timer, int Players = 0);
record SeatDto(int Spot, string Name, bool Played, string? Card);
record ResultDto(string Card, double Mean, List<int> Spots);
record SnapshotDto(string Id, string Deck, string Timer, bool Closed, bool Revealed, List<SeatDto> Players, int? YouSpot, string? YouCard, ResultDto? Result);
record JoinDto(Guid Token, string Name, int Spot);
record ErrorDto(string Error);
