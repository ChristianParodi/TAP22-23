using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    public class Site : ISite {
        public int Id { get; }
        public string Name { get; }
        public int Timezone { get; }
        public int SessionExpirationInSeconds { get; }
        public double MinimumBidIncrement { get; }
        public string ConnectionString { get; }

        private readonly IAlarmClock _alarmClock;
        private IAlarm _alarm;

        public Site Copy() {
            return new Site(Id, Name, Timezone, SessionExpirationInSeconds, MinimumBidIncrement, ConnectionString, _alarmClock);
        }

        public Site(int id, string name, int timezone, int sessionExpirationInSeconds, double minimumBidIncrement, string connectionString, IAlarmClock alarmClock) {
            Id = id;
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            ConnectionString = connectionString;

            _alarmClock = alarmClock;
            _alarm = _alarmClock.InstantiateAlarm(300_000); // 5 minutes 
            _alarm.RingingEvent += DeleteExpiredSessions;
        }

        // Same constructor as above but with a DbSite object
        public Site(DbSite site, string connectionString, IAlarmClock alarmClock)
            : this(site.SiteId, site.Name, site.Timezone, site.SessionExpirationInSeconds,
                site.MinimumBidIncrement, connectionString, alarmClock) { }

        public void CreateUser(string username, string password) {
            ValidateCreateUserParams(username, password);

            using (var c = new AuctionContext(ConnectionString)) {
                var site = c.Sites
                               .Include(s => s.Users)
                               .FirstOrDefault(s => s.SiteId == Id)
                           ?? throw new AuctionSiteInvalidOperationException($"{Name} does not exist on DB");

                site.Users ??= new List<DbUser>();
                if (site.Users.Any(u => u.Username.Equals(username)))
                    throw new AuctionSiteNameAlreadyInUseException(username, $"'{username}' user is already registered");

                var user = new DbUser {
                    Username = username,
                    Password = Utils.HashPassword(password),
                    SiteId = site.SiteId
                };
                c.Users.Add(user);
                c.SaveChanges();
            }
        }

        public void Delete() {
            using (var c = new AuctionContext(ConnectionString)) {
                var site = c.Sites.FirstOrDefault(s => s.SiteId == Id)
                           ?? throw new AuctionSiteInvalidOperationException("site already deleted");

                c.Sites.Remove(site);
                c.SaveChanges();
            }
        }

        public ISession? Login(string username, string password) {
            if (username is null or "")
                throw new AuctionSiteArgumentNullException("username cannot be null or empty");
            if (password is null or "")
                throw new AuctionSiteArgumentNullException("password cannot be null or empty");
            if (username.Length is < DomainConstraints.MinUserName or > DomainConstraints.MaxUserName)
                throw new AuctionSiteArgumentException("Invalid username length");
            if (password.Length < DomainConstraints.MinUserPassword)
                throw new AuctionSiteArgumentException("Invalid password length");

            using (var c = new AuctionContext(ConnectionString)) {
                if (!c.Sites.Any(s => s.SiteId == Id))
                    throw new AuctionSiteInvalidOperationException("site already deleted");

                var user = c.Users
                    .Include(u => u.Session)
                    .FirstOrDefault(u => u.Username.Equals(username) && u.SiteId == Id);
                if (user is null || !Utils.VerifyHashPassword(user.Password, password))
                    return null;

                var sessionUser = new User(user.Username, user.Password, ConnectionString, Copy());
                // if a user has already logged in
                if (user.Session is not null) {
                    // if the session exists but is expired, we delete it and proceed with the normal login
                    // otherwise we can return it
                    if (user.Session.ValidUntil < Now()) {
                        c.Sessions.Remove(user.Session);
                        c.SaveChanges();
                    } else
                        return new Session(user.Session.SessionId.ToString(), user.Session.ValidUntil, sessionUser, sessionUser.Site);
                }

                var session = new DbSession {
                    ValidUntil = Now().AddSeconds(SessionExpirationInSeconds),
                    UserId = user.UserId,
                    SiteId = user.SiteId
                };
                c.Sessions.Add(session);
                c.SaveChanges();

                return new Session(session.SessionId.ToString(), session.ValidUntil, sessionUser, sessionUser.Site);
            }
        }

        public DateTime Now() {
            return _alarmClock.Now;
        }

        public IEnumerable<IAuction> ToyGetAuctions(bool onlyNotEnded) {
            var res = new List<Auction>();
            using (var c = new AuctionContext(ConnectionString)) {
                if (!c.Sites.Any(s => s.SiteId == Id))
                    throw new AuctionSiteInvalidOperationException("site is deleted");

                var auctions = c.Auctions
                    .Include(a => a.Seller)
                    .Where(a => a.SiteId == Id && (!onlyNotEnded || (onlyNotEnded && a.EndsOn > Now())))
                    .ToList();

                foreach (var a in auctions) {
                    var seller = new User(a.Seller.Username, a.Seller.Password, ConnectionString, Copy());
                    var currentAuction = new Auction(a.AuctionId, seller, a.Description, a.EndsOn, seller.Site);
                    res.Add(currentAuction);
                }

                return res;
            }
        }

        public IEnumerable<ISession> ToyGetSessions() {
            var res = new List<Session>();
            using (var c = new AuctionContext(ConnectionString)) {
                if (!c.Sites.Any(s => s.SiteId == Id))
                    throw new AuctionSiteInvalidOperationException("site is deleted");

                var sessions = c.Sessions
                    .Include(s => s.User)
                    .Where(s => s.SiteId == Id)
                    .ToList();

                foreach (var s in sessions) {
                    var user = new User(s.User.Username, s.User.Password, ConnectionString, Copy());
                    var currentSession = new Session(s.SessionId.ToString(), s.ValidUntil, user, user.Site);
                    res.Add(currentSession);
                }

                return res;
            }
        }

        public IEnumerable<IUser> ToyGetUsers() {
            var res = new List<User>();
            using (var c = new AuctionContext(ConnectionString)) {
                if (!c.Sites.Any(s => s.SiteId == Id))
                    throw new AuctionSiteInvalidOperationException("site is deleted");

                var users = c.Users.Where(u => u.SiteId == Id).ToList();

                foreach (var user in users) {
                    var currentUser = new User(user.Username, user.Password, ConnectionString, Copy());
                    res.Add(currentUser);
                }

                return res;
            }
        }

        // Event function
        private void DeleteExpiredSessions() {
            using (var c = new AuctionContext(ConnectionString)) {
                if (!c.Sites.Any(s => s.SiteId == Id))
                    throw new AuctionSiteInvalidOperationException("site is deleted");

                var expired = c.Sessions
                    .Where(s => s.ValidUntil <= Now() && s.SiteId == Id)
                    .ToList();
                foreach (var s in expired)
                    c.Sessions.Remove(s);
                c.SaveChanges();
            }

            _alarm = _alarmClock.InstantiateAlarm(300_000); // 5 minutes
        }

        private static void ValidateCreateUserParams(string username, string password) {
            if (username is null or "")
                throw new AuctionSiteArgumentNullException($"{nameof(username)} cannot be null");
            if (password is null or "")
                throw new AuctionSiteArgumentNullException($"{nameof(password)} cannot be null");
            if (username.Length is < DomainConstraints.MinUserName or > DomainConstraints.MaxUserName)
                throw new AuctionSiteArgumentException("Invalid username length");
            if (password.Length < DomainConstraints.MinUserPassword)
                throw new AuctionSiteArgumentException("Invalid password length");
        }

        public override bool Equals(object? obj) {
            if (obj == null || obj.GetType() != GetType())
                return false;
            var o = obj as Site;
            return o!.Name.Equals(Name);
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}
