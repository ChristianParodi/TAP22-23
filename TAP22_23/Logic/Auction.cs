using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    public class Auction : IAuction {
        public int Id { get; }
        public IUser Seller { get; }
        public string Description { get; }
        public DateTime EndsOn { get; }
        public Site Site { get; }

        private readonly string _connectionString;

        public Auction(int id, IUser seller, string description, DateTime endsOn, Site site) {
            Id = id;
            Seller = seller;
            Description = description;
            EndsOn = endsOn;
            Site = site;
            _connectionString = site.ConnectionString;
        }

        public bool Bid(ISession session, double offer) {
            ValidateBidParams(session, offer);

            using (var c = new AuctionContext(_connectionString)) {
                var auction = c.Auctions
                                  .Include(a => a.Seller)
                                  .Include(a => a.Bids)
                                  .Include(a => a.Winner)
                                  .FirstOrDefault(a => a.AuctionId == Id && a.SiteId == Site.Id)
                              ?? throw new AuctionSiteInvalidOperationException("The auction does not exists on the site");

                var bidderSession = c.Sessions
                                        .Include(s => s.User)
                                        .FirstOrDefault(s => s.SessionId.ToString().Equals(session.Id) && s.SiteId == Site.Id)
                                    ?? throw new AuctionSiteArgumentException(
                                        "The session does not exists on the site");

                var bidderUser = bidderSession.User;
                var seller = auction.Seller;
                var winner = auction.Winner;
                var bids = auction.Bids ?? new List<DbBid>();

                if (seller.SiteId != bidderUser.SiteId)
                    throw new AuctionSiteArgumentException("The user is a user of another site");

                // Reset session expiration
                (session as Session)!.ResetExpiration();
                c.SaveChanges();

                // Check if the bidder is the current winner and the offer is lower than the minimum
                var isWinning = winner != null && winner.Equals(bidderUser);
                var isFirstBid = bids.Count == 0;
                if (isWinning && offer < auction.CurrentPrice + Site.MinimumBidIncrement)
                    return false;
                if (!isWinning && offer < auction.CurrentPrice)
                    return false;
                if (!isWinning && !isFirstBid && offer < auction.CurrentPrice + Site.MinimumBidIncrement)
                    return false;

                // Check that the bid is greater than the last bid if the user already placed one
                var userBids = bids.Where(b => b.UserId != null && b.UserId == bidderUser.UserId && b.AuctionId == Id).ToList();
                if (userBids.Any()) {
                    var lastBid = userBids.OrderBy(b => b.PlacedAt).First();
                    if (offer < lastBid.Offer)
                        return false;
                }

                // Place the bid
                var bid = new DbBid {
                    AuctionId = auction.AuctionId,
                    UserId = bidderUser.UserId,
                    Offer = offer
                };

                bids.Add(bid);

                // Adjust the current values in the auction
                if (isFirstBid) {
                    auction.MaxOffer = offer;
                    auction.Winner = bidderSession.User;
                } else if (isWinning) { // not first bid
                    auction.MaxOffer = offer;
                } else if (offer > auction.MaxOffer) { // not first bid && not winning
                    auction.CurrentPrice = Math.Min(offer, auction.MaxOffer + Site.MinimumBidIncrement);
                    auction.MaxOffer = offer;
                    auction.Winner = bidderSession.User;
                } else // not first bid && not winning && offer <= max offer
                    auction.CurrentPrice = Math.Min(auction.MaxOffer, offer + Site.MinimumBidIncrement);

                c.SaveChanges();
            }

            return true;
        }
        public double CurrentPrice() {
            using (var c = new AuctionContext(_connectionString)) {
                var auction = c.Auctions.FirstOrDefault(a => a.AuctionId == Id && a.SiteId == Site.Id)
                                ?? throw new AuctionSiteInvalidOperationException("The auction does not exists on the site");
                return auction.CurrentPrice;
            }
        }

        public IUser? CurrentWinner() {
            using (var c = new AuctionContext(_connectionString)) {
                var auction = c.Auctions
                                  .Include(a => a.Winner)
                                  .FirstOrDefault(a => a.AuctionId == Id && a.SiteId == Site.Id)
                              ?? throw new AuctionSiteInvalidOperationException("The auction does not exists on the site");
                if (auction.Winner == null)
                    return null;

                var winner = auction.Winner;

                return new User(winner.Username, winner.Password, _connectionString, Site.Copy());
            }
        }

        public void Delete() {
            using (var c = new AuctionContext(_connectionString)) {
                var auction = c.Auctions.FirstOrDefault(a => a.AuctionId == Id && a.SiteId == Site.Id)
                              ?? throw new AuctionSiteInvalidOperationException("The auction does not exists on the site");

                c.Auctions.Remove(auction);
                c.SaveChanges();
            }
        }

        private void ValidateBidParams(ISession session, double offer) {
            if (EndsOn <= Site.Now())
                throw new AuctionSiteInvalidOperationException("The auction is closed");
            if (offer < 0)
                throw new AuctionSiteArgumentOutOfRangeException("Offer cannot be negative");
            if (session is null)
                throw new AuctionSiteArgumentNullException("The bidder's session does not exists");
            if (session.ValidUntil <= Site.Now())
                throw new AuctionSiteArgumentException("The session is expired");
            if (session.User.Equals(Seller))
                throw new AuctionSiteArgumentException("The seller cannot bid on his own auctions");
        }

        public override bool Equals(object? obj) {
            if (obj == null || obj.GetType() != GetType())
                return false;
            var o = obj as Auction;
            return o!.Id == Id && o.Site.Id == Site.Id;
        }

        public override int GetHashCode() {
            return HashCode.Combine(Id, Site.GetHashCode());
        }
    }
}

