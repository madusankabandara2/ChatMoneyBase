using ChatMoneyBase.Models;
using System.Collections.Concurrent;

namespace ChatMoneyBase.Services
{
    // Thread safe in-memory queue + simple logic for capacity, overflow, assignment bookkeeping.
    public class ChatQueueService : IChatQueueService
    {
        // FIFO queue holds session IDs waiting for assignment
        private readonly ConcurrentQueue<Guid> _queue = new();

        // In-memory storage of sessions
        private readonly ConcurrentDictionary<Guid, ChatSession> _sessions = new();

        // Agents list (teams + overflow).
        private readonly List<Agent> _agents = new();

        // A simple lock for operations that need Synchronized updates.
        private readonly object _lock = new();

        // Round robin index per seniority for balanced assignment among same level agents
        private readonly Dictionary<Seniority, int> _roundRobinIndex = new();

        // Configuration
        private readonly TimeSpan _pollTimeout = TimeSpan.FromSeconds(3); // 3 missed polls -> inactive
        private readonly TimeSpan _nowOffset = TimeSpan.Zero; // used if you want to simulate time

        // Office hours for overflow (local time)
        private readonly TimeSpan _officeStart = TimeSpan.FromHours(9); // 09:00
        private readonly TimeSpan _officeEnd = TimeSpan.FromHours(17);  // 17:00

        // Constructor: populate agents
        public ChatQueueService()
        {
            // Team A: 1x team lead, 2x mid-level, 1x junior
            _agents.Add(new Agent { Name = "A-TeamLead", Seniority = Seniority.TeamLead, ShiftStart = TimeSpan.FromHours(0), ShiftEnd = TimeSpan.FromHours(8) });
            _agents.Add(new Agent { Name = "A-Mid1", Seniority = Seniority.Mid, ShiftStart = TimeSpan.FromHours(0), ShiftEnd = TimeSpan.FromHours(8) });
            _agents.Add(new Agent { Name = "A-Mid2", Seniority = Seniority.Mid, ShiftStart = TimeSpan.FromHours(0), ShiftEnd = TimeSpan.FromHours(8) });
            _agents.Add(new Agent { Name = "A-Junior", Seniority = Seniority.Junior, ShiftStart = TimeSpan.FromHours(0), ShiftEnd = TimeSpan.FromHours(8) });

            // Team B: 1x senior, 1x mid, 2x junior
            _agents.Add(new Agent { Name = "B-Senior", Seniority = Seniority.Senior, ShiftStart = TimeSpan.FromHours(8), ShiftEnd = TimeSpan.FromHours(16) });
            _agents.Add(new Agent { Name = "B-Mid1", Seniority = Seniority.Mid, ShiftStart = TimeSpan.FromHours(8), ShiftEnd = TimeSpan.FromHours(16) });
            _agents.Add(new Agent { Name = "B-Junior1", Seniority = Seniority.Junior, ShiftStart = TimeSpan.FromHours(8), ShiftEnd = TimeSpan.FromHours(16) });
            _agents.Add(new Agent { Name = "B-Junior2", Seniority = Seniority.Junior, ShiftStart = TimeSpan.FromHours(8), ShiftEnd = TimeSpan.FromHours(16) });

            // Team C: 2x mid-level (night shift)
            _agents.Add(new Agent { Name = "C-Mid1", Seniority = Seniority.Mid, ShiftStart = TimeSpan.FromHours(16), ShiftEnd = TimeSpan.FromHours(24) }); // 16:00 - 00:00
            _agents.Add(new Agent { Name = "C-Mid2", Seniority = Seniority.Mid, ShiftStart = TimeSpan.FromHours(16), ShiftEnd = TimeSpan.FromHours(24) });

            // Overflow team (6 juniors) but mark them IsOverflow=true so we only use them in office hours if needed
            for (int i = 0; i < 6; i++)
            {
                _agents.Add(new Agent
                {
                    Name = $"Overflow-J{i + 1}",
                    Seniority = Seniority.Junior,
                    IsOverflow = true,
                    ShiftStart = TimeSpan.FromHours(9),
                    ShiftEnd = TimeSpan.FromHours(17)
                });
            }

            // init round robin indices
            foreach (Seniority s in Enum.GetValues(typeof(Seniority)))
                _roundRobinIndex[s] = 0;
        }

        // PUBLIC API: Create session
        public (bool allowed, ChatSession session, string message) CreateSession()
        {
            lock (_lock)
            {
                var session = new ChatSession();

                // Capacity computed from agents currently able to accept new chats (on-shift)
                int capacity = GetCapacity(onlyNonOverflow: true);
                int maxQueue = (int)Math.Floor(capacity * 1.5);

                int queueCount = _queue.Count;

                // If queue full, check overflow possibility during office hours
                if (queueCount >= maxQueue)
                {
                    if (IsOfficeHours())
                    {
                        // See if overflow can be used
                        int capWithOverflow = GetCapacity(onlyNonOverflow: false);
                        int maxQueueOverflow = (int)Math.Floor(capWithOverflow * 1.5);

                        if (queueCount >= maxQueueOverflow)
                        {
                            // overflow present but still full -> refuse
                            session.Status = SessionStatus.Refused;
                            return (false, session, "Queue + overflow full => refused");
                        }
                        else
                        {
                            // allow adding session because overflow increases capacity
                            _sessions[session.Id] = session;
                            _queue.Enqueue(session.Id);
                            return (true, session, "Queued using overflow capacity (office hours).");
                        }
                    }
                    else
                    {
                        // Not office hours -> refuse straight away
                        session.Status = SessionStatus.Refused;
                        return (false, session, "Queue full and not office hours => refused");
                    }
                }
                else
                {
                    // Normal accept
                    _sessions[session.Id] = session;
                    _queue.Enqueue(session.Id);
                    return (true, session, "Queued");
                }
            }
        }

        // Poll endpoint: client calls every 1s
        public (bool found, ChatSession session) PollSession(Guid sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                // update poll counters
                session.PollCount++;
                session.LastPolledAt = DateTime.UtcNow;

                // If it was assigned previously we mark Active
                if (session.Status == SessionStatus.Assigned)
                    session.Status = SessionStatus.Active;

                return (true, session);
            }
            return (false, null);
        }

        public IEnumerable<ChatSession> GetAllSessions() => _sessions.Values.ToList();

        // Called by background worker to try to assign queued sessions to agents
        public void ProcessQueueAndAssign()
        {
            lock (_lock)
            {
                // Try keep assigning while there is capacity and queued items
                while (_queue.TryPeek(out var sessionId))
                {
                    if (!_sessions.TryGetValue(sessionId, out var session))
                    {
                        // corrupted entry -> remove
                        _queue.TryDequeue(out _);
                        continue;
                    }

                    // Only try to assign queued sessions
                    if (session.Status != SessionStatus.Queued)
                    {
                        _queue.TryDequeue(out _);
                        continue;
                    }

                    var assignedAgent = TryAssignSessionToAgent(session);
                    if (assignedAgent != null)
                    {
                        // assignment succeeded -> dequeue the session
                        _queue.TryDequeue(out _);
                        session.AssignedAgentId = assignedAgent.Id;
                        session.Status = SessionStatus.Assigned;
                        assignedAgent.AssignedSessionIds.Add(session.Id);
                        // loop continues to next queued session
                    }
                    else
                    {
                        // No agent available right now -> stop trying further
                        break;
                    }
                }
            }
        }

        // Try to find an agent (preferring junior -> mid -> senior -> teamlead),
        // using round robin among same level
        private Agent TryAssignSessionToAgent(ChatSession session)
        {
            DateTime now = DateTime.UtcNow;

            // seniority order: prefer juniors first
            var orderedLevels = new[] { Seniority.Junior, Seniority.Mid, Seniority.Senior, Seniority.TeamLead };

            foreach (var level in orderedLevels)
            {
                // Agents of this level that are NOT overflow (unless office hours) or that match overflow rules
                var group = _agents
                    .Where(a => a.Seniority == level)
                    .Where(a => !a.IsOverflow || IsOfficeHours()) // only use overflow during office hours
                    .ToList();

                if (!group.Any()) continue;

                // start index for round robin in this group
                int idx = _roundRobinIndex[level] % group.Count;

                // we attempt each agent in group once (rotating start)
                for (int i = 0; i < group.Count; i++)
                {
                    var agent = group[(idx + i) % group.Count];

                    // Check can this agent accept a new chat right now
                    if (agent.CanAcceptNewChat(now))
                    {
                        // update round robin pointer to next agent in this group
                        _roundRobinIndex[level] = (idx + i + 1) % group.Count;
                        return agent;
                    }
                }
            }

            // No agent available
            return null;
        }

        // Mark a session inactive
        public void MarkInactive(Guid sessionId)
        {
            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.Status = SessionStatus.Inactive;
                    // if it was assigned to an agent, remove from agent's assigned list
                    if (session.AssignedAgentId.HasValue)
                    {
                        var aid = session.AssignedAgentId.Value;
                        var agent = _agents.FirstOrDefault(x => x.Id == aid);
                        if (agent != null)
                        {
                            agent.AssignedSessionIds.Remove(session.Id);
                        }
                    }
                    // If it was still in queue remove it
                    // (ConcurrentQueue doesn't support removal, but we will let ProcessQueueAndAssign skip non queued statuses)
                    // won't reconstruct the queue here.
                }
            }
        }

        // Recompute total capacity from agents currently available to accept new chats.
        // If onlyNonOverflow==true, ignore overflow agents (used when determining queue capacity before overflow is considered)
        public int GetCapacity(bool onlyNonOverflow)
        {
            DateTime now = DateTime.UtcNow;
            double total = 0;
            foreach (var a in _agents)
            {
                if (a.IsOverflow && onlyNonOverflow) continue;
                if (!IsOfficeHours() && a.IsOverflow) continue; // overflow only in office hours
                                                                // consider agent if they are on shift
                if (a.IsOnShift(now))
                {
                    total += a.MaxConcurrency; // MaxConcurrency already applied 10*multiplier floored per agent
                }
            }
            // floor the total capacity
            return (int)Math.Floor(total);
        }

        // Check Are agent in office hours
        private bool IsOfficeHours()
        {
            var t = DateTime.Now.TimeOfDay;
            if (_officeStart <= _officeEnd)
                return t >= _officeStart && t < _officeEnd;
            return t >= _officeStart || t < _officeEnd;
        }

        // Background monitor that marks sessions inactive if they have not been polled for 3 polls (3s)
        public void MonitorForMissedPolls()
        {
            var now = DateTime.UtcNow;
            var toMark = new List<Guid>();
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                // only consider queued/assigned/active sessions
                if (s.Status == SessionStatus.Queued || s.Status == SessionStatus.Assigned || s.Status == SessionStatus.Active)
                {
                    // if never polled and created more than timeout ago OR lastPolled older than timeout
                    if ((s.LastPolledAt == null && (now - s.CreatedAt) > _pollTimeout) ||
                        (s.LastPolledAt != null && (now - s.LastPolledAt.Value) > _pollTimeout))
                    {
                        toMark.Add(s.Id);
                    }
                }
            }

            // mark outside of enumeration lock
            foreach (var id in toMark)
                MarkInactive(id);
        }
    }
}
