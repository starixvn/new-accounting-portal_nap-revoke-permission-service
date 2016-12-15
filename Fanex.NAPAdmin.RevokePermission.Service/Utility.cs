namespace Fanex.NAPAdmin.RevokePermission.Service
{
    using System;

    internal class Utility
    {
        public static bool Validate24Hour(string time)
        {
            TimeSpan timeSpan;
            return TimeSpan.TryParse(time, out timeSpan);
        }
    }
}