using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormancer;

namespace Server.Users
{
    public interface IUserService
    {
        Task<User> GetUser(IScenePeerClient peer);

        Task<User> GetUser(string uid);

        Task UpdateUserData<T>(IScenePeerClient peer,T data);

        Task<User> AddAuthentication(User user,string provider, JObject authData); 

        bool IsAuthenticated(IScenePeerClient peer);

        Task<User> GetUserByClaim(string provider, string claimPath, string login);

        Task<User> CreateUser(string v, JObject userData);

        void SetUid(IScenePeerClient peer, string id);
    }
}
