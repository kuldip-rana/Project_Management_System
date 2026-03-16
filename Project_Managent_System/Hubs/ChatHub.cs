using Microsoft.AspNet.SignalR;
using System.Threading.Tasks;

namespace Project_Managent_System.Hubs
{
    public class ChatHub : Hub
    {
        // Users join a "Group" based on ProjectId or TaskId so they only hear relevant messages
        public async Task JoinGroup(string groupName)
        {
            await Groups.Add(Context.ConnectionId, groupName);
        }

        public void SendMessage(string groupName, object commentData)
        {
            // Broadcasts to everyone in that specific project/task group
            Clients.Group(groupName).addNewMessageToPage(commentData);
        }
    }
}