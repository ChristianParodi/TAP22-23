using System.ComponentModel.DataAnnotations;

namespace Parodi {
    public class DbSession {
        [Key]
        public int SessionId { get; set; }
        public DateTime ValidUntil { get; set; }
        // Foreign keys
        public int UserId { get; set; }
        public DbUser User { get; set; }
        public int SiteId { get; set; }
        public DbSite Site { get; set; }
        //public ICollection<DbAuction>? AuctionsBid { get; set; }
        // Many to many
        //public List<DbBid>? Bids { get; set; }

        [Timestamp]
        public byte[] Version { get; set; }
    }
}

