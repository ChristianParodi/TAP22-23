using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    public class Host : IHost {
        public string ConnectionString { get; }
        public IAlarmClockFactory AlarmClockFactory { get; }

        public Host(string connectionString, IAlarmClockFactory alarmClockFactory) {
            ConnectionString = connectionString;
            AlarmClockFactory = alarmClockFactory;
        }

        public void CreateSite(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement) {
            ValidateCreateSiteParams(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement);

            using (var c = new AuctionContext(ConnectionString)) {
                if (c.Sites.Any(s => s.Name == name))
                    throw new AuctionSiteNameAlreadyInUseException(
                        $"the name '{name}' is already in use for this host");
                var site = new DbSite {
                    Name = name,
                    Timezone = timezone,
                    SessionExpirationInSeconds = sessionExpirationTimeInSeconds,
                    MinimumBidIncrement = minimumBidIncrement
                };
                c.Sites.Add(site);
                c.SaveChanges();
            }
        }

        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos() {
            var res = new List<(string Name, int TimeZone)>();

            using (var c = new AuctionContext(ConnectionString)) {
                var dbSites = c.Sites.ToList();
                foreach (var dbSite in dbSites)
                    res.Add((dbSite.Name, dbSite.Timezone));
            }

            return res;
        }

        public ISite LoadSite(string name) {
            if (name is null or "")
                throw new AuctionSiteArgumentNullException("Site name cannot be null");
            if (name.Length is < DomainConstraints.MinSiteName or > DomainConstraints.MaxSiteName)
                throw new AuctionSiteArgumentException("Invalid site name (too short/long)");

            using (var c = new AuctionContext(ConnectionString)) {
                var dbSite = c.Sites.FirstOrDefault(s => s.Name == name) ??
                             throw new AuctionSiteInexistentNameException(
                                 $"the site named {name} is nonexistent on this host");
                var alarmClock = AlarmClockFactory.InstantiateAlarmClock(dbSite.Timezone);
                return new Site(dbSite, ConnectionString, alarmClock);
            }
        }

        private static void ValidateCreateSiteParams(string name, int timezone, int sessionExpirationTimeInSeconds,
            double minimumBidIncrement) {
            if (name is null)
                throw new AuctionSiteArgumentNullException("Site name cannot be null");
            if (name.Length is < DomainConstraints.MinSiteName or > DomainConstraints.MaxSiteName)
                throw new AuctionSiteArgumentException("Invalid site name length");
            if (timezone is < DomainConstraints.MinTimeZone or > DomainConstraints.MaxTimeZone)
                throw new AuctionSiteArgumentOutOfRangeException("Invalid timezone");
            if (sessionExpirationTimeInSeconds <= 0)
                throw new AuctionSiteArgumentOutOfRangeException($"{nameof(sessionExpirationTimeInSeconds)} must be positive");
            if (minimumBidIncrement <= 0)
                throw new AuctionSiteArgumentOutOfRangeException($"{nameof(minimumBidIncrement)} must be positive");
        }
    }
}
