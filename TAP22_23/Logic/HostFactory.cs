using Microsoft.Data.SqlClient;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    public class HostFactory : IHostFactory {
        public void CreateHost(string connectionString) {
            if (connectionString is null or "")
                throw new AuctionSiteArgumentNullException("Invalid connection string");

            try {
                using (var c = new AuctionContext(connectionString)) {
                    c.Database.EnsureDeleted();
                    c.Database.EnsureCreated();
                }
            } catch (SqlException e) {
                throw new AuctionSiteUnavailableDbException("Invalid connection string", e);
            }
        }

        public IHost LoadHost(string connectionString, IAlarmClockFactory alarmClockFactory) {
            if (connectionString is null or "")
                throw new AuctionSiteArgumentNullException("Invalid connection string");
            if (alarmClockFactory is null)
                throw new AuctionSiteArgumentNullException("alarmClockFactory cannot be null");

            try {
                using (var c = new AuctionContext(connectionString)) {
                    if (!c.Database.CanConnect())
                        throw new AuctionSiteUnavailableDbException("Cannot connect to DB");
                    return new Host(connectionString, alarmClockFactory);
                }
            } catch (SqlException e) {
                throw new AuctionSiteUnavailableDbException("Invalid connection string", e);
            }
        }
    }
}
