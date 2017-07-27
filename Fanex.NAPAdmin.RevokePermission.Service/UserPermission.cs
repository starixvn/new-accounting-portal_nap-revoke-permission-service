namespace Fanex.NAPAdmin.RevokePermission.Service
{
    using System;
    using System.Configuration;
    using System.Timers;
    using Data;
    using Logging;
    using ONEbook.UM.Services;
    using ONEbook.UM.Services.Impl;

    public class UserPermission : IDisposable
    {
        private const int DefaultGMT = 7;
        private const string DefaultStartFrom = "00:05";
        private const double DefaultDelayHours = 1;

        private readonly IMaintenanceService maintenanceService;
        private readonly Timer dailyTimer;
        private readonly Timer delayTimer;
        private bool isFirstTime;

        public UserPermission()
        {
            maintenanceService = new MaintenanceService();
            dailyTimer = new Timer();
            delayTimer = new Timer(DefaultDelayHours * 60 * 60 * 1000);
            isFirstTime = true;
        }

        public void Revoke()
        {
            SetUpTimer();
            dailyTimer.Start();
        }

        public void OnStop()
        {
            StopTimer();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopTimer();
            }
        }

        private void SetUpTimer()
        {
            int gmt = DefaultGMT;

            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["GMT"])
                && int.TryParse(ConfigurationManager.AppSettings["GMT"], out gmt))
            {
                gmt = int.Parse(ConfigurationManager.AppSettings["GMT"]);
            }

            var startFrom = ConfigurationManager.AppSettings["StartFrom"];
            TimeSpan timeStart = !string.IsNullOrEmpty(startFrom) && Utility.Validate24Hour(startFrom)
                ? TimeSpan.Parse(startFrom)
                : TimeSpan.Parse(DefaultStartFrom);

            var utcTime = DateTime.UtcNow.AddHours(gmt);
            var firstInterval = CalculateFirstInterval(timeStart, utcTime.TimeOfDay);
            dailyTimer.Interval = firstInterval.TotalMilliseconds;

            dailyTimer.Elapsed += DailyElapsedEvent;
            delayTimer.Elapsed += DelayElapsedEvent;
        }

        private TimeSpan CalculateFirstInterval(TimeSpan startTime, TimeSpan startTimeOfService)
        {
            return startTimeOfService <= startTime
                ? startTime - startTimeOfService
                : startTime.Add(new TimeSpan(24, 0, 0)) - startTimeOfService;
        }

        private void DailyElapsedEvent(object source, ElapsedEventArgs e)
        {
            if (isFirstTime)
            {
                ResetDailyTimer();
            }

            if (maintenanceService.GetUnderMaintenanceTime().IsUnderMaintenanceTime)
            {
                if (delayTimer.Enabled == false)
                {
                    delayTimer.Start();
                }
            }
            else
            {
                ExecuteRevocationSP();
            }
        }

        private void ResetDailyTimer()
        {
            dailyTimer.Stop();
            dailyTimer.Interval = new TimeSpan(24, 0, 0).TotalMilliseconds;
            dailyTimer.Start();
            isFirstTime = false;
        }

        private void DelayElapsedEvent(object source, ElapsedEventArgs e)
        {
            if (!maintenanceService.GetUnderMaintenanceTime().IsUnderMaintenanceTime)
            {
                delayTimer.Stop();
                ExecuteRevocationSP();
            }
        }

        private void ExecuteRevocationSP()
        {
            using (var objDB = new ObjectDb("RevokeUserPermission"))
            {
                try
                {
                    objDB.ExecuteNonQuery();
                }
                catch (Exception exception)
                {
                    LogService.Error("Revoke User Permission Service: Error when calling SP", exception);
                }
            }
        }

        private void StopTimer()
        {
            dailyTimer.Stop();
            dailyTimer.Close();

            delayTimer.Stop();
            delayTimer.Close();
        }
    }
}