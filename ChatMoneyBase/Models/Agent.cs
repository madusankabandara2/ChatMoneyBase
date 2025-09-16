namespace ChatMoneyBase.Models
{
    public class Agent
    {
        // Unique identifier
        public Guid Id { get; set; } = Guid.NewGuid();

        // Human friendly Name
        public string Name { get; set; }

        // Seniority
        public Seniority Seniority { get; set; }

        // Shift times (local time of day). Support overnight shifts.
        public TimeSpan ShiftStart { get; set; } //Shift begins
        public TimeSpan ShiftEnd { get; set; } //End time of the shift

        // If true this agent is part of overflow team
        public bool IsOverflow { get; set; } = false;

        // Session IDs currently assigned to this agent
        public HashSet<Guid> AssignedSessionIds { get; } = new();

        // Maximum concurrent chats this agent can handle (10 * multiplier, floored)
        public int MaxConcurrency => (int)Math.Floor(10 * SeniorityHelper.GetMultiplier(Seniority));

        // Is the agent currently in shift (based on local DateTime)
        public bool IsOnShift(DateTime now)
        {
            var t = now.TimeOfDay;
            // Normal shift (start < end)
            if (ShiftStart <= ShiftEnd)
                return t >= ShiftStart && t < ShiftEnd;
            // Overnight shift (ex: 20:00 - 06:00)
            return t >= ShiftStart || t < ShiftEnd;
        }

        // How many chats are currently assigned
        public int CurrentAssignments => AssignedSessionIds.Count;

        // Check Can accept a new chat right now based on the current time?
        public bool CanAcceptNewChat(DateTime now) => IsOnShift(now) && CurrentAssignments < MaxConcurrency;
    }
}
