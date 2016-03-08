using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormancer;
using Stormancer.Server.Components;
using Stormancer.Diagnostics;

namespace Server.Users
{
    class UserService : IUserService
    {
        private readonly Database.IESClientFactory _clientFactory;
        private readonly string _indexName;
        private readonly ILogger _logger;


        private static bool _mappingChecked = false;
        private static AsyncLock _mappingCheckedLock = new AsyncLock();
        private async Task CreateUserMapping()
        {

            await (await Client()).MapAsync<User>(m => m
                .DynamicTemplates(templates => templates
                    .Add(t => t
                        .Name("auth")
                        .PathMatch("auth.*")
                        .MatchMappingType("string")
                        .Mapping(ma => ma.String(s => s.Index(Nest.FieldIndexOption.NotAnalyzed)))
                        )
                    .Add(t => t
                        .Name("data")
                        .PathMatch("userData.*")
                        .MatchMappingType("string")
                        .Mapping(ma => ma.String(s => s.Index(Nest.FieldIndexOption.NotAnalyzed)))
                        )
                     )
                 );
        }


        public UserService(UserManagementConfig config, Database.IESClientFactory clientFactory, IEnvironment environment, ILogger logger)
        {
            _indexName = (string)(environment.Configuration.index);

            _logger = logger;
            _logger.Log(LogLevel.Trace, "users", $"Using index {_indexName}", new { index = _indexName });

            _clientFactory = clientFactory;
        }


        private async Task<Nest.IElasticClient> Client()
        {
            var client = await _clientFactory.CreateClient(_indexName);
            if (!_mappingChecked)
            {
                using (await _mappingCheckedLock.LockAsync())
                {
                    if (!_mappingChecked)
                    {
                        _mappingChecked = true;
                        await CreateUserMapping();
                    }
                }
            }
            return client;
        }
        public async Task<User> AddAuthentication(User user, string provider, JObject authData)
        {
            var c = await Client();

            user.Auth[provider] = authData;

            await c.IndexAsync(user);
            return user;
        }

        public async Task<User> CreateUser(string id, JObject userData)
        {

            var user = new User() { Id = id, UserData = userData };
            var esClient = await Client();
            await esClient.IndexAsync(user);

            return user;
        }


        public async Task<User> GetUserByClaim(string provider, string claimPath, string login)
        {
            var c = await Client();
            var r = await c.SearchAsync<User>(sd => sd.Query(qd => qd.Term("auth." + provider + "." + claimPath, login)));
            var h = r.Hits.FirstOrDefault();
            if (h != null)
            {
                return h.Source;
            }
            else
            {
                return null;
            }
        }

        public async Task<User> GetUser(string uid)
        {
            var c = await Client();
            var r = await c.GetAsync<User>(gd => gd.Id(uid));
            if (r.Source != null)
            {
                return r.Source;
            }
            else
            {
                return null;
            }
        }

        public async Task UpdateUserData<T>(string uid, T data)
        {
            var user = await GetUser(uid);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            else
            {
                user.UserData = JObject.FromObject(data);
                await (await Client()).IndexAsync(user);
            }
        }
    }
}
