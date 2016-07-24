using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Plugins.API;
using Stormancer.Server;
using Server.Users;

namespace Server.Plugins.Notification
{
    class NotificationPlugin : IHostPlugin
    {
        
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register<EmailNotificationProvider>().As<INotificationProvider>();
                  builder.Register<NotificationChannel>().As<INotificationChannel>();
                  
              };

        }
    }
}
