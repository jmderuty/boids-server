using Server.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class UserExtensions
    {
        public static ulong GetSteamId(this User user)
        {
            return ulong.Parse(user.UserData["steamid"].ToObject<string>());
        }
    }
}
