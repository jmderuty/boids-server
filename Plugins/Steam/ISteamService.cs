using Server.Plugins.Steam.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Plugins.Steam
{
    public interface ISteamService
    {
        Task<ulong?> AuthenticateUserTicket(string ticket);

        Task<Dictionary<ulong, SteamPlayerSummary>> GetPlayerSummaries(IEnumerable<ulong> steamIds);
    }
}