namespace SRN.Application.Interfaces
{
    /// <summary>
    /// Contract for pushing asynchronous notifications to clients.
    /// Typically implemented using WebSockets (SignalR) for real-time UI updates.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Dispatches a positive notification to a specific user (e.g., blockchain anchoring succeeded).
        /// </summary>
        Task SendSuccessAsync(string userId, string message, string artifactId);

        /// <summary>
        /// Dispatches an error notification to a specific user (e.g., blockchain transaction failed).
        /// </summary>
        Task SendFailureAsync(string userId, string message, string artifactId);
    }
}