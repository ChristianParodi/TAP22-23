using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    [Index(nameof(Username), nameof(SiteId), IsUnique = true)]
    public class DbUser {
        [Key]
        public int UserId { get; set; }
        [MinLength(DomainConstraints.MinUserName)]
        [MaxLength(DomainConstraints.MaxUserName)]
        public string Username { get; set; }
        public string Password { get; set; }
        // Foreign keys
        public int SiteId { get; set; }
        public DbSite Site { get; set; }
        public DbSession? Session { get; set; }
        public ICollection<DbAuction>? AuctionsOwned { get; set; }
        public ICollection<DbAuction>? AuctionsWinner { get; set; }
        // Many to many
        public ICollection<DbAuction>? AuctionsBid { get; set; }
        public List<DbBid>? Bids { get; set; }

        [Timestamp]
        public byte[] Version { get; set; }
    }
}

