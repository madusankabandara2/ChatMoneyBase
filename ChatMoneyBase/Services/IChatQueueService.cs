using ChatMoneyBase.Models;

namespace ChatMoneyBase.Services
{
    public interface IChatQueueService
    {
        // Create a new session; returns (allowed, session)
        (bool allowed, ChatSession session, string message) CreateSession();

        // Poll endpoint: update poll counters
        (bool found, ChatSession session) PollSession(Guid sessionId);

        // Try to assign queued sessions (called by background worker)
        void ProcessQueueAndAssign();

        // Called by monitor to mark session inactive
        void MarkInactive(Guid sessionId);

        // For testing
        IEnumerable<ChatSession> GetAllSessions();
    }
}
