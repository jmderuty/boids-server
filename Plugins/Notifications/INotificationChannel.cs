using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.Notification
{
    interface INotificationChannel
    {
        Task<bool> SendNotification(string type, dynamic data);
    }

    internal class NotificationChannel: INotificationChannel
    {
        private readonly IEnumerable<INotificationProvider> _providers;
        public NotificationChannel(IEnumerable<INotificationProvider> providers)
        {
            _providers = providers;
        }

        public async Task<bool> SendNotification(string type, dynamic data)
        {
            foreach(var provider in _providers)
            {
                if(await provider.SendNotification(type,data))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
