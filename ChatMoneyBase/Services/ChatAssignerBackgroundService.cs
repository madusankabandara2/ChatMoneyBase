namespace ChatMoneyBase.Services
{
    public class ChatAssignerBackgroundService : BackgroundService
    {
        private readonly ChatQueueService _queueService;

        public ChatAssignerBackgroundService(ChatQueueService queueService)
        {
            _queueService = queueService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // try assigning every 1 second (can be tuned)
            while (!stoppingToken.IsCancellationRequested)
            {
                _queueService.ProcessQueueAndAssign();
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
