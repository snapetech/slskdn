namespace slskd.Integrations.Notifications
{
    using System.Threading.Tasks;

    public interface INotificationService
    {
        Task SendAsync(string title, string body, string cacheKey = null);
        Task SendPrivateMessageAsync(string username, string message);
        Task SendRoomMentionAsync(string room, string username, string message);
    }
}
