using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    [Index(nameof(Name), IsUnique = true)]
    public class DbSite {
        [Key]
        public int SiteId { get; set; }
        [MinLength(DomainConstraints.MinSiteName)]
        [MaxLength(DomainConstraints.MaxSiteName)]
        public string Name { get; set; }
        [Range(0 + double.Epsilon, double.MaxValue)]
        public double MinimumBidIncrement { get; set; }
        [Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { get; set; }
        [Range(0, int.MaxValue)]
        public int SessionExpirationInSeconds { get; set; }
        // Navigation properties
        public ICollection<DbUser>? Users { get; set; }
        public ICollection<DbAuction>? Auctions { get; set; }
        public ICollection<DbSession>? Sessions { get; set; }

        [Timestamp]
        public byte[] Version { get; set; }
    }
}

