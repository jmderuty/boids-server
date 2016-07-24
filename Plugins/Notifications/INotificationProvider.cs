using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.Notification
{
    public interface INotificationProvider
    {
        Task<bool> SendNotification(string type, dynamic data);
    }
}
