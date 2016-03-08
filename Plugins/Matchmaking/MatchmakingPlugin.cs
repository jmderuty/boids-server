using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public class MatchmakingPlugin : IHostPlugin
    {
        private const string _pluginName = "stormancer.plugins.matchmaking";

        private readonly IMatchmakingConfig _config;
        private IMatchmakingService _matchmakingService;

        public MatchmakingPlugin(IMatchmakingConfig config)
        {
            _config = config;
        }

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += HostStarting;

            ctx.HostShuttingDown += HostShuttingDown;

            ctx.SceneCreating += SceneCreating;
            ctx.HostDependenciesRegistration += HostDependenciesRegistration;
        }
       
        private IHost _host;
        private void HostDependenciesRegistration(IDependencyBuilder builder)
        {
            _config.RegisterDependencies(builder);
            builder.Register<MatchmakingService>().As<IMatchmakingService>().InstancePerScene();
        }
        private void HostStarting(IHost host)
        {
            
            _host = host;

           
            //host.DependencyResolver.Resolve<ILogger>().Info("plugins.matchmaking","Matchmaking plugin registered.");
        }

        private void SceneCreating(ISceneHost scene)
        {
            if (scene.Id == _config.MatchmakingSceneId)
            {
                var logger = scene.DependencyResolver.Resolve<ILogger>();

                try
                {
                 

                    _matchmakingService = scene.DependencyResolver.Resolve<IMatchmakingService>();
                    _matchmakingService.Init(scene);

                    scene.Disconnected.Add(args => _matchmakingService.CancelMatch(args.Peer));
                    scene.AddProcedure("match.find", _matchmakingService.FindMatch);
                    scene.AddRoute("match.ready.resolve", _matchmakingService.ResolveReadyRequest);
                    scene.AddRoute("match.cancel", _matchmakingService.CancelMatch);

                    scene.Metadata[_pluginName] = _config.Kind;
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, "plugins.matchmaking", $"An exception occured when creating scene {scene.Id}.", ex);
                    throw;
                }
            }
        }

        private void HostShuttingDown(IHost host)
        {
            var _ = this._matchmakingService?.Stop();
        }
    }
}
