using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    internal class User : IUser {
        public string Username { get; }
        public string Password { get; }
        public Site Site { get; }

        private readonly string _connectionString;

        public User(string username, string password, string connectionString, Site site) {
            Username = username;
            Password = password;
            _connectionString = connectionString;
            Site = site;
        }

        public User(DbUser user, string connectionString, Site site)
            : this(user.Username, user.Password, connectionString, site) { }

        public void Delete() {
            using (var c = new AuctionContext(_connectionString)) {
                var siteAuctions = Site.ToyGetAuctions(true).ToList();
                if (siteAuctions.Any(a => a.Seller.Equals(this) && a.EndsOn > Site.Now()))
                    throw new AuctionSiteInvalidOperationException($"The user '{Username}' owns ongoing auctions");
                if (siteAuctions.Any(a => a.CurrentWinner()!.Equals(this) && a.EndsOn > Site.Now()))
                    throw new AuctionSiteInvalidOperationException($"The user '{Username}' is winning an ongoing auction/s");

                var user = c.Users.FirstOrDefault(u => u.Username == Username && u.SiteId == Site.Id)
                           ?? throw new AuctionSiteInvalidOperationException(
                               $"The user '{Username}' does not exists on the site '{Site.Name}'");

                c.Users.Remove(user);
                c.SaveChanges();
            }
        }

        public IEnumerable<IAuction> WonAuctions() {
            var res = new List<Auction>();
            using (var c = new AuctionContext(_connectionString)) {
                var winnerAuctions = c.Auctions
                    .Include(a => a.Seller)
                    .Where(a =>
                    a.Winner != null && a.Winner.Username == Username && a.SiteId == Site.Id);

                foreach (var a in winnerAuctions) {
                    var seller = new User(a.Seller.Username, a.Seller.Password, _connectionString, Site.Copy());
                    var currentAuction = new Auction(a.AuctionId, seller, a.Description, a.EndsOn, seller.Site);
                    res.Add(currentAuction);
                }

                return res;
            }
        }

        public override bool Equals(object? obj) {
            if (obj == null || obj.GetType() != GetType())
                return false;
            var o = obj as User;
            return o!.Username.Equals(Username) && Site.Equals(o.Site);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Username, Site.GetHashCode());
        }
    }
}
