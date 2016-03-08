using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public class AuthenticationResult
    {
        private AuthenticationResult()
        {
        }

        public static AuthenticationResult CreateSuccess(User user, string provider, Dictionary<string, string> context)
        {
            return new AuthenticationResult { Success = true, AuthenticatedUser = user, Provider = provider, AuthenticationContext = context };
        }

        public static AuthenticationResult CreateFailure(string reason, string provider, Dictionary<string, string> context)
        {
            return new AuthenticationResult { Success = false, ReasonMsg = reason, Provider = provider, AuthenticationContext = context };
        }

        public bool Success { get; private set; }

        public string AuthenticatedId
        {
            get
            {
                return (AuthenticatedUser != null) ? AuthenticatedUser.Id : null;
            }
        }

        public User AuthenticatedUser { get; private set; }

        public string ReasonMsg { get; private set; }

        public string Provider { get; private set; }

        public Dictionary<string, string> AuthenticationContext { get; private set; }
    }
}
