using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Project_Managent_System.Startup))]
namespace Project_Managent_System
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // This maps the /signalr wire-up
            app.MapSignalR();
        }
    }
}