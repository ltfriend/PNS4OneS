using System.Threading.Tasks;

namespace PNS4OneS
{
    interface IMessageSender
    {
        Task SendMessageToUserAsync(string appId, string ibId, string userId, Message message);
        Task SendMessageToGroupAsync(string appId, string ibId, string userGroup, Message message);
        Task SendMessageToAllAsync(string appId, string ibId, Message message);
    }
}
