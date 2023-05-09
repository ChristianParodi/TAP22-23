using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    public class AuctionContext : TapDbContext {
        public DbSet<DbUser> Users { get; set; }
        public DbSet<DbSite> Sites { get; set; }
        public DbSet<DbAuction> Auctions { get; set; }
        public DbSet<DbSession> Sessions { get; set; }
        public DbSet<DbBid> Bids { get; set; }

        private readonly string _connectionString;

        public AuctionContext(string connectionString) : base(new DbContextOptionsBuilder<AuctionContext>().Options) {
            _connectionString = connectionString;
        }

        // taken from
        // https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors?view=sql-server-ver16
        internal enum DbErrorCode {
            DuplicateKeyEntry = 2601,
            ForeignKeyConflict = 547
        };

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            var users = modelBuilder.Entity<DbUser>();
            var sessions = modelBuilder.Entity<DbSession>();
            var auctions = modelBuilder.Entity<DbAuction>();

            // Foreign keys - user
            users
                .HasOne(u => u.Site)
                .WithMany(s => s.Users)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            users
                .HasOne(u => u.Session)
                .WithOne(s => s.User)
                .OnDelete(DeleteBehavior.SetNull);

            users
                .HasMany(u => u.AuctionsBid)
                .WithMany(a => a.Bidders)
                .UsingEntity<DbBid>(
                    j => j
                        .HasOne(t => t.Auction)
                        .WithMany(t => t.Bids)
                        .HasForeignKey(t => t.AuctionId)
                        .OnDelete(DeleteBehavior.ClientCascade)
                        .IsRequired(),
                    j => j
                        .HasOne(t => t.User)
                        .WithMany(t => t.Bids)
                        .HasForeignKey(t => t.UserId)
                        .OnDelete(DeleteBehavior.SetNull),
                    j => {
                        j.Property(a => a.Offer).IsRequired();
                        j.Property(a => a.PlacedAt).IsRequired();
                        j.HasKey(t => t.BidId);
                    });

            // Foreign keys - session
            sessions
                .HasOne(s => s.Site)
                .WithMany(s => s.Sessions)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            sessions
                .HasOne(s => s.User)
                .WithOne(u => u.Session)
                .OnDelete(DeleteBehavior.ClientCascade)
                .IsRequired();

            // Foreign keys - auction
            auctions
                .HasOne(a => a.Seller)
                .WithMany(u => u.AuctionsOwned)
                .HasForeignKey(a => a.SellerId)
                .OnDelete(DeleteBehavior.ClientCascade)
                .IsRequired();

            auctions
                .HasOne(a => a.Site)
                .WithMany(s => s.Auctions)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            auctions
                .HasOne(a => a.Winner)
                .WithMany(u => u.AuctionsWinner)
                .HasForeignKey(a => a.WinnerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options) {
            options.UseSqlServer(_connectionString);
            base.OnConfiguring(options);
        }

        public override int SaveChanges() {
            try {
                return base.SaveChanges();
            } catch (SqlException e) {
                throw new AuctionSiteUnavailableDbException("DB unavailable", e);
            } catch (DbUpdateConcurrencyException e) {
                throw new AuctionSiteConcurrentChangeException("Concurrent change", e);
            } catch (DbUpdateException e) {
                var sqlExc = e.InnerException as SqlException
                             ?? throw new AuctionSiteInvalidOperationException("DB error occurred", e);
                switch (sqlExc.Number) {
                    case (int)DbErrorCode.DuplicateKeyEntry:
                        throw new AuctionSiteNameAlreadyInUseException(null, "Duplicate key entry", e);
                    case (int)DbErrorCode.ForeignKeyConflict:
                        throw new AuctionSiteInvalidOperationException("Foreign key error", e);
                    default:
                        throw new AuctionSiteInvalidOperationException("An SQL error occurred", e);
                }
            }
        }
    }

}
