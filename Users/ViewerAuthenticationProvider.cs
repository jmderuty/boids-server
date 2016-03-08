using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;

namespace Server.Users
{
    public class ViewerAuthenticationProvider : IAuthenticationProvider
    {
        public const string PROVIDER_NAME = "viewer";
        public void AddMetadata(Dictionary<string, string> result)
        {
            result.Add("provider.viewer", "enabled");
        }

        public void AdjustScene(ISceneHost scene)
        {
        }

        public Task<AuthenticationResult> Authenticate(Dictionary<string, string> authenticationCtx, IUserService _userService)
        {
            if (authenticationCtx["provider"] != PROVIDER_NAME)
            {
                return Task.FromResult<AuthenticationResult>(null);
            }

            var user = new User() { Id = "viewer" };

            return Task.FromResult( AuthenticationResult.CreateSuccess(user, PROVIDER_NAME, authenticationCtx));
        }
    }
}
