using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Parodi;

[Index(nameof(AuctionId), nameof(UserId), nameof(PlacedAt), IsUnique = true)]
public class DbBid {

    [Key]
    public int BidId { get; set; }
    [Range(0 + double.Epsilon, double.MaxValue)]
    public double Offer { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.Now;
    // Foreign keys
    public int AuctionId { get; set; }
    public DbAuction Auction { get; set; }
    public int? UserId { get; set; }
    public DbUser? User { get; set; }

    [Timestamp]
    public byte[] Version { get; set; }
}