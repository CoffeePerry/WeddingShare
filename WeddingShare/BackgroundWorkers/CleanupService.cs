using NCrontab;
using WeddingShare.Constants;
using WeddingShare.Helpers;

namespace WeddingShare.BackgroundWorkers
{
    public sealed class CleanupService(IWebHostEnvironment hostingEnvironment, ISettingsHelper settingsHelper, IFileHelper fileHelper, ILogger<CleanupService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cron = await settingsHelper.GetOrDefault(BackgroundServices.Cleanup.Schedule, "0 4 * * *");
            var schedule = CrontabSchedule.Parse(cron, new CrontabSchedule.ParseOptions() { IncludingSeconds = cron.Split(new[] { ' ' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length == 6 });

            var enabled = await settingsHelper.GetOrDefault(BackgroundServices.Cleanup.Enabled, true);
            if (enabled)
            {
                await Task.Delay((int)TimeSpan.FromSeconds(10).TotalMilliseconds, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextExecutionTime = schedule.GetNextOccurrence(now);
                var waitTime = nextExecutionTime - now;
                await Task.Delay(waitTime, stoppingToken);

                enabled = await settingsHelper.GetOrDefault(BackgroundServices.Cleanup.Enabled, true);
                if (enabled)
                {
                    await Cleanup();
                }
            }
        }

        private async Task Cleanup()
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
    }
}