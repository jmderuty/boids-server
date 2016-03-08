using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.IO;
using Stormancer.Server.Components;
using Server.Plugins.Steam;

namespace Server.Users
{
    public class SteamAuthenticationProvider : IAuthenticationProvider
    {
        private const string Provider_Name = "steam";
        private const string ClaimPath = "steamid";

        private ISteamUserTicketAuthenticator _authenticator;

        public SteamAuthenticationProvider()
        {
        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            result.Add("provider.steamauthentication", "enabled");
        }

        public void AdjustScene(ISceneHost scene)
        {
            var environment = scene.DependencyResolver.Resolve<IEnvironment>();
            ApplyConfig(environment, scene);

            environment.ConfigurationChanged += (sender, e) => ApplyConfig((IEnvironment)e, scene);
        }

        private void ApplyConfig(IEnvironment environment, ISceneHost scene)
        {
            var steamConfig = environment.Configuration.steam;

            if (steamConfig?.usemockup != null && (bool)(steamConfig.usemockup))
            {
                _authenticator = new SteamUserTicketAuthenticatorMockup();
            }
            else
            {
                _authenticator =  
                    
                    new SteamUserTicketAuthenticator(scene.DependencyResolver.Resolve<ISteamService>());
            }
        }

        public async Task<AuthenticationResult> Authenticate(Dictionary<string, string> authenticationCtx, IUserService userService)
        {
            if (authenticationCtx["provider"] != Provider_Name)
            {
                return null;
            }

            string ticket;
            if (!authenticationCtx.TryGetValue("ticket", out ticket) || string.IsNullOrWhiteSpace(ticket))
            {
                return AuthenticationResult.CreateFailure("Steam session ticket must not be empty.", Provider_Name, authenticationCtx);
            }

            var steamId = await _authenticator.AuthenticateUserTicket(ticket);

            if (!steamId.HasValue)
            {
                return AuthenticationResult.CreateFailure("Invalid steam session ticket.", Provider_Name, authenticationCtx);
            }

            var steamIdString = steamId.GetValueOrDefault().ToString();
            var user = await userService.GetUserByClaim(Provider_Name, ClaimPath, steamIdString);

            if (user == null)
            {
                var uid = Guid.NewGuid().ToString("N");
                user = await userService.CreateUser(uid, JObject.FromObject(new { steamid = steamIdString }));

                var claim = new JObject();
                claim[ClaimPath] = steamIdString;
                user = await userService.AddAuthentication(user, Provider_Name, claim);
            }

            return AuthenticationResult.CreateSuccess(user, Provider_Name, authenticationCtx);
        }

    }
}
