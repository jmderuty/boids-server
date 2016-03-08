using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;
using Stormancer;
using Stormancer.Plugins;
using Stormancer.Server.Components;

namespace Server.Database
{
    public class Startup
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ESClientPlugin());

        }
    }

    internal class ESClientPlugin : IHostPlugin
    {
        private object synclock = new object();
       
        public void Build(HostPluginBuildContext ctx)
        {


            ctx.HostStarting += h =>
            {
                h.DependencyResolver.Register<IESClientFactory, ESClientFactory>();
            };
        }
    }
    public interface IESClientFactory
    {
        Task<Nest.IElasticClient> CreateClient(string index);
    }
    class ESClientFactory : IESClientFactory
    {
        private IEnvironment _environment;
        private AsyncLock _lock = new AsyncLock();
        private IEnumerable<Stormancer.Server.Index> _indices;

        public ESClientFactory(IEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<IElasticClient> CreateClient(string indexName)
        {
            if(_indices == null)
            {
                using (await _lock.LockAsync())
                {
                    if(_indices == null)
                    {
                        _indices = await _environment.ListIndices();
                    }
                }
            }

            var endpoint = (await _environment.GetApplicationInfos()).ApiEndpoint;
            var index = _indices.FirstOrDefault(i => i.name == indexName);
            var connection = new Elasticsearch.Net.Connection.HttpClientConnection(
                 new ConnectionSettings(),
                 new AuthenticatedHttpClientHandler(index));

            return new Nest.ElasticClient(new ConnectionSettings(new Uri(endpoint + "/" + index.accountId + "/_indices/_q"), index.name), connection);
        }
    }
}
