using System.ComponentModel.DataAnnotations;

namespace Parodi {
    public class DbAuction {
        [Key]
        public int AuctionId { get; set; }
        public string Description { get; set; }
        public DateTime EndsOn { get; set; }
        [Range(0 + double.Epsilon, double.MaxValue)]
        public double CurrentPrice { get; set; }
        [Range(0 + double.Epsilon, double.MaxValue)]
        public double MaxOffer { get; set; }
        // Foreign keys
        public int SellerId { get; set; }
        public DbUser Seller { get; set; }
        public int? WinnerId { get; set; }
        public DbUser? Winner { get; set; }
        public int SiteId { get; set; }
        public DbSite Site { get; set; }
        // Many to many
        public ICollection<DbUser>? Bidders { get; set; }
        public List<DbBid>? Bids { get; set; }

        [Timestamp]
        public byte[] Version { get; set; }
    }
}

