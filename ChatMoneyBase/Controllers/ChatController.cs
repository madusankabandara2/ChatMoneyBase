using ChatMoneyBase.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatMoneyBase.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatQueueService _queueService;

        public ChatController(ChatQueueService queueService)
        {
            _queueService = queueService;
        }

        // POST api/chat
        // Creates a new chat session and returns session id and status (OK/NOT OK)
        [HttpPost]
        public IActionResult Create()
        {
            var (allowed, session, message) = _queueService.CreateSession();

            if (!allowed)
            {
                // refused
                return BadRequest(new { status = "NOT OK", message, sessionId = (session?.Id.ToString() ?? null) });
            }

            return Ok(new { status = "OK", message, sessionId = session.Id.ToString() });
        }

        // POST api/chat/{id}/poll
        // The chat window should call this every 1 second
        [HttpPost("{id:guid}/poll")]
        public IActionResult Poll(Guid id)
        {
            var (found, session) = _queueService.PollSession(id);
            if (!found) return NotFound(new { status = "NOK", message = "session not found" });

            // If the session has been assigned to an agent, include that info also
            if (session.AssignedAgentId.HasValue)
                return Ok(new { status = "OK", assignedAgent = session.AssignedAgentId.Value.ToString(), sessionStatus = session.Status.ToString() });

            return Ok(new { status = "OK", message = "still queued", sessionStatus = session.Status.ToString() });
        }

        // GET api/chat/all  (for debugging/inspection)
        [HttpGet("all")]
        public IActionResult All() => Ok(_queueService.GetAllSessions());
    }
}
