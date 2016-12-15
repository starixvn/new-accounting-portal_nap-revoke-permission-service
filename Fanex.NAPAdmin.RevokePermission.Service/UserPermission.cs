namespace Fanex.NAPAdmin.RevokePermission.Service
{
    using System;
    using System.Configuration;
    using System.Timers;
    using Fanex.Data;
    using Fanex.Logging;
    using ONEbook.UM.Services;
    using ONEbook.UM.Services.Impl;

    internal class UserPermission : IDisposable
    {
        private const int DefaultGMT = 7;
        private const string DefaultStartFrom = "00:05";
        private const double DefaultDelayHours = 1;

        private IMaintenanceService _maintenanceService = new MaintenanceService();

        private Timer _dailyTimer = new Timer();
        private Timer _delayTimer = new Timer(DefaultDelayHours * 60 * 60 * 1000);
        private bool _isFirstTime = true;

        public void Revoke()
        {
            SetUpTimer();
            _dailyTimer.Start();
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
            _dailyTimer.Interval = firstInterval.TotalMilliseconds;

            _dailyTimer.Elapsed += DailyElapsedEvent;
            _delayTimer.Elapsed += DelayElapsedEvent;
        }

        private TimeSpan CalculateFirstInterval(TimeSpan startTime, TimeSpan startTimeOfService)
        {
            return startTimeOfService <= startTime
                ? startTime - startTimeOfService
                : startTime.Add(new TimeSpan(24, 0, 0)) - startTimeOfService;
        }

        private void DailyElapsedEvent(object source, ElapsedEventArgs e)
        {
            if (_isFirstTime)
            {
                ResetDailyTimer();
            }

            if (_maintenanceService.GetUnderMaintenanceTime().IsUnderMaintenanceTime)
            {
                if (_delayTimer.Enabled == false)
                {
                    _delayTimer.Start();
                }
            }
            else
            {
                ExecuteRevocationSP();
            }
        }

        private void ResetDailyTimer()
        {
            _dailyTimer.Stop();
            _dailyTimer.Interval = new TimeSpan(24, 0, 0).TotalMilliseconds;
            _dailyTimer.Start();
            _isFirstTime = false;
        }

        private void DelayElapsedEvent(object source, ElapsedEventArgs e)
        {
            if (!_maintenanceService.GetUnderMaintenanceTime().IsUnderMaintenanceTime)
            {
                _delayTimer.Stop();
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

        public void OnStop()
        {
            StopTimer();
        }

        public void Dispose()
        {
            StopTimer();
        }

        private void StopTimer()
        {
            _dailyTimer.Stop();
            _dailyTimer.Close();

            _delayTimer.Stop();
            _delayTimer.Close();
        }
    }
}