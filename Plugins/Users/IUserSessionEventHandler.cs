﻿using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public interface IUserSessionEventHandler
    {
        Task OnLoggedIn(IScenePeerClient client, User user);

        Task OnLoggedOut(IScenePeerClient client, User user);
    }
}
