using Server.Plugins.Configuration;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.Notification
{
    class EmailNotificationProvider : INotificationProvider
    {

        private string _emailServer;
        private int _port;
        private string _login;
        private string _password;
        private string _from;
        private readonly ILogger _logger;

        public EmailNotificationProvider(IConfiguration configuration, ILogger logger)
        {
            ApplyConfiguration(configuration.Settings);
            configuration.SettingsChanged += OnConfigurationSettingsChanged;
            _logger = logger;
            
        }

        private void OnConfigurationSettingsChanged(object sender, dynamic e)
        {
            ApplyConfiguration(e);
        }

        private void ApplyConfiguration(dynamic config)
        {
            _emailServer = (string)config.notifications?.smtp?.server;
            _port = (int)config.notifications?.smtp?.port;
            _from = (string)config.notifications?.smtp?.from;
            _login = (string)config.notification?.smtp?.login;
            _password = (string)config.notification?.smtp?.password;
        }

        public async Task<bool> SendNotification(string type, dynamic data)
        {
            var client = new System.Net.Mail.SmtpClient(_emailServer, _port);
            if (!string.IsNullOrEmpty(_login))
            {
                client.Credentials = new System.Net.NetworkCredential(_login, _password);
            }
            if (type == "loginpassword.resetRequest")
            {
                if(data.email == null)
                {
                    return false;
                }

                var email = data.email;

                await client.SendMailAsync("server@prodigy.com", email, "[Prodigy]Password reset request", PasswordResetBody + data.code);

                _logger.Debug("notifications.email", $"Send password reset request email to {data.email}");
                return true;
            }
            return false;
        }

        private string PasswordResetBody = "Someone(presumably you) requested a password change through e-mail verification.If this was not you, ignore this message and nothing will happen. If you requested this verification, please enter the following code in the password change form of your game client. \br";
    }
}
