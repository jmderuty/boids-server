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
        Task<User> GetUser(string uid);
        Task<User> AddAuthentication(User user,string provider, JObject authData); 
        Task<User> GetUserByClaim(string provider, string claimPath, string login);
        Task<User> CreateUser(string uid, JObject userData);
        Task UpdateUserData<T>(string uid, T data);
    }
}
