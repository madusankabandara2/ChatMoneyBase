==========Summary==========

1. This code implements an in-memory (no DB).
2. Each agent capacity = Floor(10 * seniorityMultiplier). Team capacity = sum(agent capacities to accept new chats). Queue max length = Floor(capacity * 1.5) - Using PDF provided.
3. Office hours (when overflow is allowed) — I pick 09:00–17:00 local time by default.
4. 3 shifts of 8 hours each.
	I. Team A: 00:00–07:59
	II. Team B: 08:00–15:59
	III. Team C: 16:00–23:59
5. Client calls POST /api/chat/{id}/poll every 1s. If a session has not been polled for 3 seconds (3 polls) it becomes Inactive.
6. Fill juniors first, then mid, then senior, etc. Within a seniority group, do round robin



==========How To Test?==========

1. Open Postman
2. Test the Endpoints
	I. Create a chat session:	Method: "POST"
								URL: https://localhost:7003/api/chat - Port may differ
								Respons Example (JSON): {"status":"OK","message":"Queued","sessionId":"457266c8-d2b5-4ee2-919a-........"} OR status:"NOT OK"
	
	II. Poll for session status:	Method: "POST"
									URL: https://localhost:7003/api/chat/{sessionId}/poll - Get seesion id from "Create a chat session"
									Response Example (JSON): {"status":"OK","assignedAgent":"2f64d39f-ab3a-4c6f-8519-..........6","sessionStatus":"Inactive"} OR sessionStatus:"Queued" OR sessionStatus:"Active", Something like that.

	III. Inspect all sessions:	Method: "GET"
								URL: https://localhost:7003/api/chat/all
								Response Example(JSON): list of all sessions

NOTE: Make sure to handle SSL Verification with Postman.		