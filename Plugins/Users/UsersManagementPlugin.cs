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
using Server.Plugins.API;

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
            ctx.HostDependenciesRegistration += RegisterDependencies;

        }
        private void RegisterDependencies(IDependencyBuilder b)
        {
            //Indices
            b.Register<UserPeerIndex>().As<IUserPeerIndex>().SingleInstance();
            b.Register<PeerUserIndex>().As<IPeerUserIndex>().SingleInstance();
            b.Register<UserToGroupIndex>().SingleInstance();
            b.Register<GroupsIndex>().SingleInstance();
            b.Register<SingleNodeActionStore>().As<IActionStore>().SingleInstance();

            b.Register<UserManagementConfig>(_config);
           
            b.Register<UserService>().As<IUserService>();
            b.Register<UserSessions>().As<IUserSessions>();
            
        }
        private void HostStarting(IHost host)
        {
            host.AddSceneTemplate("authenticator", AuthenticatorSceneFactory);


        }



        private void AuthenticatorSceneFactory(ISceneHost scene)
        {
            scene.AddProcedure("login", async p =>
            {
                scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Logging in an user.", null);

                var accessor = scene.DependencyResolver.Resolve<Management.ManagementClientAccessor>();
                var authenticationCtx = p.ReadObject<Dictionary<string, string>>();
                scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Authentication context read.", authenticationCtx);
                var result = new LoginResult();
                var userService = scene.DependencyResolver.Resolve<IUserService>();
                var userSessions = scene.DependencyResolver.Resolve<IUserSessions>();

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
                        result.UserId = authResult.AuthenticatedUser.Id;
                        result.Token = await client.CreateConnectionToken(_config.OnRedirect(authResult), _config.UserDataSelector(authResult));
                        await userSessions.SetUser(p.RemotePeer, authResult.AuthenticatedUser);
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

            scene.AddController<GroupController>();

            scene.Disconnected.Add(async args =>
            {
                await scene.GetComponent<IUserSessions>().LogOut(args.Peer);
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




}
