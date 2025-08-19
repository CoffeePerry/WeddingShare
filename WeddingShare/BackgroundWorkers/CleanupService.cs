using NCrontab;
using WeddingShare.Constants;
using WeddingShare.Helpers;

namespace WeddingShare.BackgroundWorkers
{
    public sealed class CleanupService(IWebHostEnvironment hostingEnvironment, ISettingsHelper settingsHelper, IFileHelper fileHelper, ILogger<CleanupService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = await settingsHelper.GetOrDefault(BackgroundServices.Cleanup.Enabled, true);
            if (enabled)
            {
                var cron = await settingsHelper.GetOrDefault(BackgroundServices.Cleanup.Schedule, "0 4 * * *");
                var nextExecutionTime = DateTime.Now.AddSeconds(10);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var currentCron = await settingsHelper.GetOrDefault(BackgroundServices.Cleanup.Schedule, "0 4 * * *");

                    var now = DateTime.Now;
                    if (now >= nextExecutionTime)
                    {
                        await Cleanup();
                        
                        var schedule = CrontabSchedule.Parse(cron, new CrontabSchedule.ParseOptions() { IncludingSeconds = cron.Split(new[] { ' ' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length == 6 });
                        nextExecutionTime = schedule.GetNextOccurrence(now);
                    }
                    else
                    {
                        if (!currentCron.Equals(cron))
                        {
                            nextExecutionTime = DateTime.Now;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }

                    cron = currentCron;
                }
            }
        }

        private async Task Cleanup()
        {
            try
            {
                await Task.Run(() =>
                {
                    var paths = new List<string>()
                    {
                        Path.Combine(hostingEnvironment.WebRootPath, Directories.TempFiles)
                    };

                    if (paths != null)
                    {
                        foreach (var path in paths)
                        {
                            try
                            {
                                fileHelper.DeleteDirectoryIfExists(path);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, $"An error occurred while running cleanup of '{path}'");
                            }
                        }
                    }
                });
            }
            catch (Exception ex) 
            {
                logger.LogError(ex, $"CleanupService - Failed to clean up files - {ex?.Message}");
            }
        }
    }
}