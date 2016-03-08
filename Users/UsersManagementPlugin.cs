using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server;
using Stormancer;
using Stormancer.Diagnostics;

namespace Server.Users
{
    class UsersManagementPlugin : Stormancer.Plugins.IHostPlugin
    {
        private readonly UserManagementConfig _config;

        public UsersManagementPlugin(UserManagementConfig config = null)
        {
            if (config == null)
            {
                config = new UserManagementConfig();
            }
            _config = config;

        }
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += HostStarting;

        }
        private void HostStarting(IHost host)
        {
            host.AddSceneTemplate("authenticator", AuthenticatorSceneFactory);
            host.DependencyResolver.Register<UserManagementConfig>(_config);
            host.DependencyResolver.Register<IUserService, UserService>();
        }



        private void AuthenticatorSceneFactory(ISceneHost scene)
        {
            scene.AddProcedure("login", async p =>
            {
                scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Logging in an user.", null);

                var accessor = scene.DependencyResolver.Resolve<Management.ManagementClientAccessor>();
                var authenticationCtx = p.ReadObject<Dictionary<string, string>>();
                var result = new LoginResult();
                var userService = scene.DependencyResolver.Resolve<IUserService>();

                foreach (var provider in _config.AuthenticationProviders)
                {
                    var authResult = await provider.Authenticate(authenticationCtx, userService);
                    if (authResult == null)
                    {
                        continue;
                    }

                    if (authResult.Success)
                    {
                        scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Authentication successful.", authResult);

                        result.Success = true;
                        var client = await accessor.GetApplicationClient();
                        result.Token = await client.CreateConnectionToken(_config.OnRedirect(authResult), _config.UserDataSelector(authResult));
                        userService.SetUid(p.RemotePeer, authResult.AuthenticatedId);
                        break;
                    }
                    else
                    {
                        scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Authentication failed.", authResult);

                        result.ErrorMsg = authResult.ReasonMsg;
                        break;
                    }
                }
                if (!result.Success)
                {
                    if (result.ErrorMsg == null)
                    {
                        result.ErrorMsg = "No authentication provider able to handle these credentials were found.";
                    }
                }

                if (result.Success)
                {
                    scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "User logged in.", null);
                }
                else
                {
                    scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "User failed to log in.", null);
                }
                p.SendValue(result);
            });

            foreach (var provider in _config.AuthenticationProviders)
            {
                provider.AdjustScene(scene);
            }

        }
        private Dictionary<string, string> GetAuthenticateRouteMetadata()
        {
            var result = new Dictionary<string, string>();

            foreach (var provider in _config.AuthenticationProviders)
            {
                provider.AddMetadata(result);
            }

            return result;
        }
    }


    public class LoginResult
    {
        public bool Success { get; set; }

        public string Token { get; set; }

        public string ErrorMsg { get; set; }
    }


}
