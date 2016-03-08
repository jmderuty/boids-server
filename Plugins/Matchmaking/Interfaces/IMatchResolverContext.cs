using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public interface IMatchResolverContext
    {
        Match Match { get; }

        Action<IMatchmakingResolutionWriterContext> ResolutionAction { get; set; }
    }
}
