namespace SRN.Application.Interfaces
{
    public interface INotificationService
    {
        Task SendSuccessAsync(string userId, string message, string artifactId);
        Task SendFailureAsync(string userId, string message, string artifactId);
    }
}