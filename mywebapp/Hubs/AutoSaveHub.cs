using Microsoft.AspNetCore.SignalR;

namespace mywebapp.Hubs
{
    public class AutoSaveHub : Hub
    {
        public async Task SaveCode(string studentId, int groupId, int questionIndex, string code)
        {
            await Clients.All.SendAsync("CodeSaved", studentId, groupId, questionIndex, code);
        }
    }
}