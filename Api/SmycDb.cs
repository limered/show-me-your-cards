using Microsoft.EntityFrameworkCore;

public class Session
{
    public string Id { get; set; } = "";
    public string Deck { get; set; } = "";
    public string TimerSetting { get; set; } = "";
    public bool Closed { get; set; }
    public bool Revealed { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public int Round { get; set; } = 1;
    public DateTime? RoundStartedAtUtc { get; set; }
    public List<Player> Players { get; set; } = [];
}

public class Player
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = "";
    public Guid Token { get; set; }
    public string Name { get; set; } = "";
    public string? Card { get; set; }
    public int Spot { get; set; }
    public DateTime LastSeen { get; set; }
    public Session? Session { get; set; }
}

public class SmycDb : DbContext
{
    public SmycDb(DbContextOptions<SmycDb> options) : base(options) { }
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Session>(e =>
        {
            e.ToTable("smyc_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(8);
        });
        b.Entity<Player>(e =>
        {
            e.ToTable("smyc_players");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => new { x.SessionId, x.Spot }).IsUnique();
            e.HasOne(x => x.Session).WithMany(s => s.Players).HasForeignKey(x => x.SessionId);
        });
    }
}
