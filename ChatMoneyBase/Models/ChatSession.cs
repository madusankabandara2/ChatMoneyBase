namespace ChatMoneyBase.Models
{
    public enum SessionStatus
    {
        Queued,
        Assigned,
        Active,
        Inactive,
        Refused
    }

    public class ChatSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Last time a poll was received (UTC). Null until first poll.
        public DateTime? LastPolledAt { get; set; } = null;

        // How many poll calls received (In here, increment each poll)
        public int PollCount { get; set; } = 0;

        public SessionStatus Status { get; set; } = SessionStatus.Queued;

        // Assigned agent if available
        public Guid? AssignedAgentId { get; set; } = null;
    }
}
