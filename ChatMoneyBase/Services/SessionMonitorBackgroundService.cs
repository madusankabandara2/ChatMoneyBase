namespace ChatMoneyBase.Services
{
    public class SessionMonitorBackgroundService : BackgroundService
    {
        private readonly ChatQueueService _queueService;

        public SessionMonitorBackgroundService(ChatQueueService queueService)
        {
            _queueService = queueService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // run every 1 second
            while (!stoppingToken.IsCancellationRequested)
            {
                _queueService.MonitorForMissedPolls();
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
