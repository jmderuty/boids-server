using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using Stormancer;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Globalization;
using Stormancer.Plugins;
using Stormancer.Diagnostics;

namespace Server.Users
{
    class LoginPasswordAuthenticationProvider : IAuthenticationProvider
    {
        private const string PROVIDER_NAME = "loginpassword";
        private const int SaltValueSize = 32;
        public void AddMetadata(Dictionary<string, string> result)
        {
            result.Add("provider.loginpassword", "enabled");
        }

        public void AdjustScene(ISceneHost scene)
        {
            scene.AddProcedure("provider.loginpassword.createAccount", p => CreateAccount(p, scene));
        }

        private async Task CreateAccount(RequestContext<IScenePeerClient> p, ISceneHost scene)
        {
            try
            {
                var userService = scene.GetComponent<IUserService>();
                var rq = p.ReadObject<CreateAccountRequest>();

                scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Creating user " + rq.Login + ".", rq.Login);
                ValidateLoginPassword(rq.Login, rq.Password);

                var user = await userService.GetUserByClaim(PROVIDER_NAME, "login", rq.Login);

                if (user != null)
                {
                    scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "User with login " + rq.Login + " already exists.", rq.Login);

                    throw new ClientException("An user with this login already exist.");
                }

                user = await userService.GetUser(p.RemotePeer);
                if (user == null)
                {
                    try
                    {
                        var uid = PROVIDER_NAME + "-" + rq.Login;
                        user = await userService.CreateUser(uid, JObject.Parse(rq.UserData));
                    }
                    catch (Exception ex)
                    {
                        scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Couldn't create user " + rq.Login + ".", ex);

                        throw new ClientException("Couldn't create account : " + ex.Message);
                    }
                }

                var salt = GenerateSaltValue();

                try
                {
                    await userService.AddAuthentication(user, PROVIDER_NAME, JObject.FromObject(new
                    {
                        login = rq.Login,
                        email = rq.Email,
                        salt = salt,
                        password = HashPassword(rq.Password, salt),
                        validated = false,
                    }));
                }
                catch (Exception ex)
                {
                    scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Couldn't link account " + rq.Login + ".", ex);

                    throw new ClientException("Couldn't link account : " + ex.Message);
                }

                scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Creating user " + rq.Login + ".", rq.Login);
                p.SendValue(new LoginResult
                {
                    Success = true
                });


            }
            catch (Exception ex)
            {
                p.SendValue(new LoginResult { ErrorMsg = ex.Message, Success = false });
            }
        }


        private void ValidateLoginPassword(string login, string password)
        {
            if (string.IsNullOrEmpty(login))
            {
                throw new ClientException("User id must be non null or empty.");

            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(login, @"^[\w|_-]+$"))
            {
                throw new ClientException("User id must contain alphanumeric characters, _  or -.");
            }
            if (string.IsNullOrEmpty(password))
            {
                throw new ClientException("Password must be non null or empty.");
            }
            if (password.Length < 6)
            {
                throw new ClientException("Password must be more than 6 characters long.");
            }
            var complexityScore = 0;

            if (System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]"))
            {
                complexityScore += 1;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]"))
            {
                complexityScore += 1;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]"))
            {
                complexityScore += 1;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"\W|_"))
            {
                complexityScore += 1;
            }

            if (complexityScore < 3)
            {
                throw new ClientException("Password must contain at least 3 types of characters among lowercase, uppercase, numbers and  non word characters.");
            }
        }

        public async Task<AuthenticationResult> Authenticate(Dictionary<string, string> authenticationCtx, IUserService _userService)
        {

            if (authenticationCtx["provider"] != PROVIDER_NAME)
            {
                return null;
            }

            var login = authenticationCtx["login"];
            var password = authenticationCtx["password"];
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                return AuthenticationResult.CreateFailure("Login and password must be non empty.", PROVIDER_NAME, authenticationCtx);
            }

            var user = await _userService.GetUserByClaim(PROVIDER_NAME, "login", login);
            if (user == null)
            {
                return AuthenticationResult.CreateFailure("No user found that matches the provided login/password.", PROVIDER_NAME, authenticationCtx);
            }

            dynamic authData = user.Auth[PROVIDER_NAME];

            string salt = authData.salt;
            string hash = authData.password;

            var candidateHash = HashPassword(password, salt);
            if (hash != candidateHash)
            {
                return AuthenticationResult.CreateFailure("No user found that matches the provided login/password.", PROVIDER_NAME, authenticationCtx);
            }
            return AuthenticationResult.CreateSuccess(user, PROVIDER_NAME, authenticationCtx);
        }


        private string GenerateSaltValue()
        {

            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789&é'(-è_çà)=$*ù%*µ!:;.?,";


            Random random = new Random();

            if (random != null)
            {
                // Create an array of random values.

                char[] saltValue = new char[SaltValueSize];

                for (int i = 0; i < saltValue.Length; i++)
                {
                    saltValue[i] = chars[random.Next(chars.Length)];
                }
                return new string(saltValue);

            }

            return null;
        }
        private string HashPassword(string password, string salt)
        {
            return Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password + salt)));
        }


    }

    public class CreateAccountRequest
    {
        public string Login { get; set; }

        public string Password { get; set; }

        public string Email { get; set; }

        //Json userdata
        public string UserData { get; set; }
    }
}
