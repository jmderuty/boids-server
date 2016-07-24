using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Users;

namespace Server
{
    public class App
    {
        public void Run(IAppBuilder builder)
        { 
            builder.AddGameScene();

            var userConfig = new Users.UserManagementConfig()
            {
                SceneIdRedirect = Constants.MATCHMAKER_NAME,
                UserDataSelector = r => new PlayerInfos
                {
                    isObserver = r.Provider == ViewerAuthenticationProvider.PROVIDER_NAME,
                    userId = r.AuthenticatedUser.Id
                }
            };

            userConfig.AuthenticationProviders.Add(new LoginPasswordAuthenticationProvider());
            userConfig.AuthenticationProviders.Add(new ViewerAuthenticationProvider());
            builder.AddPlugin(new UsersManagementPlugin(userConfig));

           

            var viewer = builder.AdminPlugin("viewer").Name("Viewer");
        }
    }
}
