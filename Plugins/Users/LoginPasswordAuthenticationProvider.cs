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
using MsgPack.Serialization;

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

        public void Initialize(ISceneHost scene)
        {

            scene.AddProcedure("provider.loginpassword.createAccount", p => CreateAccount(p, scene));
            scene.AddProcedure("provider.loginpassword.requestPasswordRecovery", p => RequestPasswordRecovery(p, scene));
            scene.AddProcedure("provider.loginpassword.resetPassword", p => ChangePassword(p, scene));
        }
        private Task ValidateKey(string key, ISceneHost scene)
        {
            return Task.FromResult(true);
        }

        private Task ValidatePseudo(string pseudo, ISceneHost scene)
        {

            if (pseudo == null || pseudo.Length < 3 || pseudo.Length > 15 || !System.Text.RegularExpressions.Regex.IsMatch(pseudo, "^[a-zA-Z0-9_]*$", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Compiled))
            {
                throw new ClientException("Pseudo not valid");
            }
            return Task.FromResult(true);
        }

        private async Task ValidateUserData(dynamic userData, ISceneHost scene)
        {
            await ValidatePseudo(userData?.pseudo, scene);
            await ValidateKey(userData?.key, scene);
        }
        private async Task CreateAccount(RequestContext<IScenePeerClient> requestContext, ISceneHost scene)
        {
            try
            {
                scene.DependencyResolver.Resolve<ILogger>().Debug("user.loginpassword", "test");
                var userService = scene.GetComponent<IUserService>();
                var userSessions = scene.GetComponent<IUserSessions>();
                var rq = requestContext.ReadObject<CreateAccountRequest>();
                var userData = JObject.Parse(rq.UserData);


                scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Creating user " + rq.Login + ".", rq.Login);
                ValidateLoginPassword(rq.Login, rq.Password);
                await ValidateUserData(userData, scene);

                var user = await userService.GetUserByClaim(PROVIDER_NAME, "login", rq.Login);

                if (user != null)
                {
                    //scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "User with login " + rq.Login + " already exists.", rq.Login);

                    throw new ClientException("An user with this login already exist.");
                }

                user = await userSessions.GetUser(requestContext.RemotePeer);
                if (user == null)
                {
                    try
                    {
                        var uid = Guid.NewGuid().ToString("N");
                        user = await userService.CreateUser(uid, userData);
                    }
                    catch (Exception ex)
                    {
                        //scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Couldn't create user " + rq.Login + ".", ex);

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
                    }), rq.Login);

                    scene.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Debug, "user.provider.loginpassword", $"created user. {rq.Login}", new { rq.Login, rq.Email });
                }
                catch (Exception ex)
                {
                    scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Couldn't link account " + rq.Login + ".", ex);

                    throw new ClientException("Couldn't link account : " + ex.Message);
                }

                //scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.provider.loginpassword", "Creating user " + rq.Login + ".", rq.Login);
                requestContext.SendValue(new LoginResult
                {
                    Success = true
                });


            }
            catch (Exception ex)
            {
                requestContext.SendValue(new LoginResult { ErrorMsg = ex.Message, Success = false });
            }
        }


        private void ValidateLoginPassword(string login, string password)
        {
            if (string.IsNullOrEmpty(login))
            {
                throw new ClientException("User id must be non null or empty.");

            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(login, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled))
            {
                throw new ClientException("User id must be an email address.");
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

        public async Task RequestPasswordRecovery(RequestContext<IScenePeerClient> requestContext, ISceneHost scene)
        {
            var email = requestContext.ReadObject<string>();

            var users = scene.DependencyResolver.Resolve<IUserService>();

            var user = await users.GetUserByClaim(PROVIDER_NAME, "email", email);
            if (user == null)
            {
                return;
            }
            var auth = (JObject)user.Auth[PROVIDER_NAME];
            var code = GenerateCode();
            if (await scene.DependencyResolver.Resolve<Plugins.Notification.INotificationChannel>().SendNotification("loginpassword.resetRequest", JObject.FromObject(new { email, code })))
            {
                auth["resetCode"] = JObject.FromObject(new { code = code, expiration = DateTime.UtcNow + TimeSpan.FromHours(1), retriesLeft = 3 });
                await users.AddAuthentication(user, PROVIDER_NAME, auth, (string)user.Auth[PROVIDER_NAME]["login"]);
            }
        }
        private Random rand = new Random();
        private string GenerateCode()
        {
            return rand.Next(0, 10000).ToString().PadLeft(4, '0');
        }

        public async Task ChangePassword(RequestContext<IScenePeerClient> requestContext, ISceneHost scene)
        {
            var rq = requestContext.ReadObject<ChangePasswordRequest>();

            var users = scene.DependencyResolver.Resolve<IUserService>();

            var user = await users.GetUserByClaim(PROVIDER_NAME, "email", rq.Email);

            dynamic auth = user.Auth[PROVIDER_NAME];
            var code = auth?.resetCode?.code as string;
            if (code == null)
            {
                throw new ClientException("Couldn't reset password. (Error 1)");
            }
            var expiration = (DateTime)auth?.resetCode?.expiration;
            if (expiration < DateTime.UtcNow)
            {
                throw new ClientException("Couldn't reset password. (Error 3)");
            }
            var retriesLeft = (int)auth?.resetCode?.retriesLeft;
            if (retriesLeft <= 0)
            {
                throw new ClientException("Couldn't reset passord. (Error 2)");
            }
            if (code != rq.Code)
            {
                throw new ClientException("Couldn't reset password (Error 3)");
            }
            var salt = GenerateSaltValue();
            var hashedPassword = HashPassword(rq.NewPassword, salt);
            auth.salt = salt;
            auth.password = hashedPassword;
            await users.AddAuthentication(user, PROVIDER_NAME, auth, (string)auth.login);
        }



        public async Task<AuthenticationResult> Authenticate(Dictionary<string, string> authenticationCtx, IUserService _userService)
        {

            if (authenticationCtx["provider"] != PROVIDER_NAME)
            {
                return null;
            }

            string login;
            authenticationCtx.TryGetValue("login", out login);

            string password;
            authenticationCtx.TryGetValue("password", out password);
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
        [MessagePackMember(0)]
        public string Login { get; set; }

        [MessagePackMember(1)]
        public string Password { get; set; }

        [MessagePackMember(2)]
        public string Email { get; set; }

        //Json userdata
        [MessagePackMember(3)]
        public string UserData { get; set; }
    }

    public class ChangePasswordRequest
    {
        [MessagePackMember(0)]
        public string Email { get; set; }
        [MessagePackMember(1)]
        public string Code { get; set; }
        [MessagePackMember(2)]
        public string NewPassword { get; set; }
    }
}
