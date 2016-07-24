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

            ctx.HostDependenciesRegistration += (IDependencyBuilder b) =>
            {
                b.Register<ESClientFactory>().As<IESClientFactory>().SingleInstance();
            };

        }
    }
    public interface IESClientFactory
    {
        Task<Nest.IElasticClient> CreateClient(string index);

    }
    class ESClientFactory : IESClientFactory, IDisposable
    {
        private IEnvironment _environment;
        private AsyncLock _lock = new AsyncLock();
        private IEnumerable<Stormancer.Server.Index> _indices;
        private Nest.ElasticClient _client;
        //private List<Elasticsearch.Net.Connection.HttpClientConnection> _connections = new List<Elasticsearch.Net.Connection.HttpClientConnection>();
        public ESClientFactory(IEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<IElasticClient> CreateClient(string indexName)
        {
            if (_client == null)
            {
                if (_indices == null || !_indices.Any(i => i.name == indexName))
                {
                    using (await _lock.LockAsync())
                    {
                        if (_indices == null || !_indices.Any(i => i.name == indexName))
                        {
                            _indices = await _environment.ListIndices();
                        }
                    }
                }

                //var endpoint = (await _environment.GetApplicationInfos()).ApiEndpoint;
                var index = _indices.FirstOrDefault(i => i.name == indexName);
                var indexPath = indexName;
                if (index != null)
                {
                    indexPath = index.accountId + "-" + index.name;
                    
                }
                //var connection = new Elasticsearch.Net.Connection.HttpClientConnection(
                //     new ConnectionSettings(),
                //     new AuthenticatedHttpClientHandler(index));
                //_connections.Add(connection);
                _client = new Nest.ElasticClient(new ConnectionSettings(new Elasticsearch.Net.SniffingConnectionPool(new[] { new Uri("http://localhost:9200") })).DefaultIndex(indexPath).MaximumRetries(10).MaxRetryTimeout(TimeSpan.FromSeconds(30)));
            }
            return _client;
        }

        public void Dispose()
        {
            //foreach (var c in _connections)
            //{
            //    c.Dispose();
            //}
        }
    }
}
