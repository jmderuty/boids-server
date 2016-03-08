using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormancer;

namespace Server.Users
{
    class UserService : IUserService
    {
        private Database.ESClientFactory _clientFactory;
        private string _indexName = Constants.INDEX;
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

        public UserService(UserManagementConfig config, Database.ESClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }


        private async Task<Nest.IElasticClient> Client()
        {
            var client = await _clientFactory.CreateClient(_indexName);
            if(!_mappingChecked)
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
            var r = await c.GetAsync<User>(gd => gd.Id(user.Id));
            r.Source.Auth[provider] = authData;

            await (await Client()).IndexAsync(r.Source);
            return r.Source;
        }

        public async Task<User> CreateUser(string id, JObject userData)
        {

            var user = new User() { Id = id, UserData = userData };
            var esClient = await Client();
            await esClient.IndexAsync(user);

            return user;
        }

        public void SetUid(IScenePeerClient peer, string id)
        {
            peer.Metadata["uid"] = id;
        }

        public async Task<User> GetUser(IScenePeerClient peer)
        {
            string id;
            if (!peer.Metadata.TryGetValue("uid", out id))
            {
                return null;
            }

            var c = await Client();
            var r = await c.GetAsync<User>(gd => gd.Id(id));

            return r.Source;
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

        public bool IsAuthenticated(IScenePeerClient peer)
        {
            return peer.Metadata["uid"] != null;//TODO: Do something better than that. It doesn't prove anything. We should check a signature.
        }

        public async Task UpdateUserData<T>(IScenePeerClient peer, T data)
        {
            var user = await GetUser(peer);
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
    }
}
