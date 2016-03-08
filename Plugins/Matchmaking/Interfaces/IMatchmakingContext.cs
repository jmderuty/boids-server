using Stormancer.Plugins;
using System;

namespace Stormancer.Matchmaking
{
    public interface IMatchmakingContext
    {
        DateTime CreationTimeUTC { get; }

        Group Group { get; set; }
    }
}