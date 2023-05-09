using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    internal class Session : ISession {
        public string Id { get; }
        public DateTime ValidUntil { get; private set; }
        public IUser User { get; }
        public Site Site { get; }

        private readonly string _connectionString;

        public Session(string id, DateTime validUntil, IUser user, Site site) {
            Id = id;
            ValidUntil = validUntil;
            User = user;
            Site = site;
            _connectionString = site.ConnectionString;
        }

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice) {
            ValidateAuctionParams(description, endsOn, startingPrice);

            using (var c = new AuctionContext(_connectionString)) {
                var dbSeller = c.Users.FirstOrDefault(u => u.Username == User.Username && u.SiteId == Site.Id)
                             ?? throw new AuctionSiteInvalidOperationException(
                                 "the owner of the session does not exists");
                var auctionSite = c.Sites.FirstOrDefault(s => s.SiteId == Site.Id)
                                    ?? throw new AuctionSiteInvalidOperationException(
                                        "the site does not exists");

                var dbAuction = new DbAuction {
                    Description = description,
                    EndsOn = endsOn,
                    CurrentPrice = startingPrice,
                    Seller = dbSeller,
                    Site = auctionSite
                };

                c.Auctions.Add(dbAuction);
                c.SaveChanges();
                ResetExpiration();

                var seller = new User(dbSeller, Site.ConnectionString, Site);
                return new Auction(dbAuction.AuctionId, seller, description, endsOn, Site);
            }
        }

        public void Logout() {
            using (var c = new AuctionContext(_connectionString)) {
                var session = c.Sessions.FirstOrDefault(s => s.SessionId.ToString().Equals(Id))
                              ?? throw new AuctionSiteInvalidOperationException("This session does not exist on db");
                c.Sessions.Remove(session);
                c.SaveChanges();
            }
        }

        internal void ResetExpiration() {
            using (var c = new AuctionContext(_connectionString)) {
                var session = c.Sessions.FirstOrDefault(s => s.SessionId.ToString() == Id)
                              ?? throw new AuctionSiteInvalidOperationException("This session does not exist on db");
                session.ValidUntil = Site.Now().AddSeconds(Site.SessionExpirationInSeconds);
                c.SaveChanges();
                ValidUntil = session.ValidUntil;
            }
        }

        private void ValidateAuctionParams(string description, DateTime endsOn, double startingPrice) {
            if (ValidUntil < Site.Now())
                throw new AuctionSiteInvalidOperationException("session is expired");
            if (description == null)
                throw new AuctionSiteArgumentNullException($"{nameof(description)} cannot be null");
            if (description == "")
                throw new AuctionSiteArgumentException($"{nameof(description)} cannot be empty");
            if (startingPrice < 0)
                throw new AuctionSiteArgumentOutOfRangeException($"{nameof(startingPrice)} cannot be negative");
            if (endsOn < Site.Now())
                throw new AuctionSiteUnavailableTimeMachineException($"{nameof(endsOn)} cannot be in the past");
        }

        public override bool Equals(object? obj) {
            if (obj == null || obj.GetType() != GetType())
                return false;
            var o = obj as Session;
            return o!.Id == Id;
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }
    }
}
