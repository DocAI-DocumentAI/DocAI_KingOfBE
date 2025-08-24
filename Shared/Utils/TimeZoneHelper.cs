using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Utils
{
    /// <summary>
    /// Unified TimeZone Helper for all services (Document, Notification, Auth)
    /// Handles PostgreSQL UTC requirements and Vietnam timezone conversions
    /// </summary>
    public static class TimeZoneHelper
    {
        /// <summary>
        /// Vietnam TimeZone with cross-platform support
        /// </summary>
        public static TimeZoneInfo VietnamTimeZone { get; }

        /// <summary>
        /// UTC+7 offset for Vietnam
        /// </summary>
        public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        static TimeZoneHelper()
        {
            VietnamTimeZone = GetVietnamTimeZone();
        }

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try
            {
                // Windows timezone
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                try
                {
                    // Linux/Unix timezone
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch
                {
                    try
                    {
                        // Alternative Linux timezone
                        return TimeZoneInfo.FindSystemTimeZoneById("Asia/Saigon");
                    }
                    catch
                    {
                        // Fallback: Create custom timezone
                        return TimeZoneInfo.CreateCustomTimeZone(
                            "Vietnam Standard Time",
                            VietnamOffset,
                            "Vietnam Standard Time (UTC+7)",
                            "VST");
                    }
                }
            }
        }

        #region Core DateTime Methods

        /// <summary>
        /// Get current UTC time - ALWAYS use for database operations
        /// PostgreSQL requires UTC DateTime with DateTimeKind.Utc
        /// </summary>
        public static DateTime UtcNow => DateTime.UtcNow;

        /// <summary>
        /// Get current Vietnam time for display/logging
        /// </summary>
        public static DateTime VietnamNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

        /// <summary>
        /// Get current Vietnam date (date only) for comparisons
        /// </summary>
        public static DateTime VietnamToday => VietnamNow.Date;

        /// <summary>
        /// Get UTC date for PostgreSQL queries - prevents timezone issues
        /// </summary>
        public static DateTime UtcToday => DateTime.UtcNow.Date;

        #endregion

        #region Conversion Methods

        /// <summary>
        /// Convert UTC to Vietnam time for display
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime</param>
        /// <returns>Vietnam DateTime</returns>
        public static DateTime ConvertUtcToVietnam(DateTime utcDateTime)
        {
            // Ensure input is treated as UTC
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone);
        }

        /// <summary>
        /// Convert Vietnam time to UTC for database storage
        /// </summary>
        /// <param name="vietnamDateTime">Vietnam DateTime</param>
        /// <returns>UTC DateTime</returns>
        public static DateTime ConvertVietnamToUtc(DateTime vietnamDateTime)
        {
            if (vietnamDateTime.Kind == DateTimeKind.Utc)
            {
                return vietnamDateTime;
            }

            // Treat as unspecified Vietnam time
            var unspecified = DateTime.SpecifyKind(vietnamDateTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, VietnamTimeZone);
        }

        /// <summary>
        /// Ensure DateTime is UTC with proper Kind for PostgreSQL
        /// Critical for preventing PostgreSQL timezone errors
        /// </summary>
        /// <param name="dateTime">Any DateTime</param>
        /// <returns>UTC DateTime with DateTimeKind.Utc</returns>
        public static DateTime EnsureUtc(DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
        }

        #endregion

        #region Date Calculation Methods

        /// <summary>
        /// Calculate days from today (Vietnam timezone) to target date
        /// Useful for expiration calculations
        /// </summary>
        /// <param name="targetDate">Target date (can be UTC or any kind)</param>
        /// <returns>Number of days (negative if past, positive if future)</returns>
        public static int DaysFromToday(DateTime targetDate)
        {
            var vietnamToday = VietnamToday;

            // Convert target date to Vietnam timezone if it's UTC
            var targetVietnam = targetDate.Kind == DateTimeKind.Utc
                ? ConvertUtcToVietnam(targetDate).Date
                : targetDate.Date;

            return (targetVietnam - vietnamToday).Days;
        }

        /// <summary>
        /// Check if a date is today in Vietnam timezone
        /// </summary>
        /// <param name="date">Date to check</param>
        /// <returns>True if date is today in Vietnam</returns>
        public static bool IsToday(DateTime date)
        {
            var vietnamToday = VietnamToday;
            var checkDate = date.Kind == DateTimeKind.Utc
                ? ConvertUtcToVietnam(date).Date
                : date.Date;

            return checkDate == vietnamToday;
        }

        /// <summary>
        /// Check if a date is in the past (Vietnam timezone)
        /// </summary>
        /// <param name="date">Date to check</param>
        /// <returns>True if date is before today in Vietnam</returns>
        public static bool IsExpired(DateTime date)
        {
            return DaysFromToday(date) < 0;
        }

        /// <summary>
        /// Check if a date is within warning threshold (Vietnam timezone)
        /// </summary>
        /// <param name="date">Date to check</param>
        /// <param name="warningDays">Warning threshold in days</param>
        /// <returns>True if date is within warning threshold</returns>
        public static bool IsNearingExpiration(DateTime date, int warningDays = 7)
        {
            var days = DaysFromToday(date);
            return days >= 0 && days <= warningDays;
        }

        #endregion

        #region PostgreSQL-Safe Methods

        /// <summary>
        /// Create UTC DateTime for PostgreSQL queries
        /// Prevents "cannot determine timezone" errors
        /// </summary>
        /// <param name="year">Year</param>
        /// <param name="month">Month</param>
        /// <param name="day">Day</param>
        /// <param name="hour">Hour (default: 0)</param>
        /// <param name="minute">Minute (default: 0)</param>
        /// <param name="second">Second (default: 0)</param>
        /// <returns>UTC DateTime safe for PostgreSQL</returns>
        public static DateTime CreateUtcDateTime(int year, int month, int day,
            int hour = 0, int minute = 0, int second = 0)
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }

        /// <summary>
        /// Create Vietnam DateTime and convert to UTC for database storage
        /// </summary>
        /// <param name="year">Year</param>
        /// <param name="month">Month</param>
        /// <param name="day">Day</param>
        /// <param name="hour">Hour in Vietnam timezone (default: 0)</param>
        /// <param name="minute">Minute (default: 0)</param>
        /// <param name="second">Second (default: 0)</param>
        /// <returns>UTC DateTime for database storage</returns>
        public static DateTime CreateVietnamDateTimeAsUtc(int year, int month, int day,
            int hour = 0, int minute = 0, int second = 0)
        {
            var vietnamDateTime = new DateTime(year, month, day, hour, minute, second);
            return ConvertVietnamToUtc(vietnamDateTime);
        }

        /// <summary>
        /// Create date range in UTC for PostgreSQL queries
        /// Prevents timezone-related query issues
        /// </summary>
        /// <param name="vietnamDate">Date in Vietnam timezone</param>
        /// <returns>Tuple of (start UTC, end UTC) covering the full Vietnam day</returns>
        public static (DateTime startUtc, DateTime endUtc) CreateUtcDateRange(DateTime vietnamDate)
        {
            var startVietnam = vietnamDate.Date; // Start of day in Vietnam
            var endVietnam = startVietnam.AddDays(1).AddTicks(-1); // End of day in Vietnam

            return (
                ConvertVietnamToUtc(startVietnam),
                ConvertVietnamToUtc(endVietnam)
            );
        }

        #endregion

        #region Formatting and Display Methods

        /// <summary>
        /// Format UTC time as Vietnam time string for display
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime</param>
        /// <param name="format">Format string (default: yyyy-MM-dd HH:mm:ss)</param>
        /// <returns>Formatted Vietnam time string</returns>
        public static string FormatAsVietnamTime(DateTime utcDateTime,
            string format = "yyyy-MM-dd HH:mm:ss")
        {
            var vietnamTime = ConvertUtcToVietnam(utcDateTime);
            return vietnamTime.ToString(format);
        }

        /// <summary>
        /// Format UTC time as Vietnam time with day name
        /// </summary>
        /// <param name="utcDateTime">UTC DateTime</param>
        /// <returns>Formatted string like "2024-08-24 13:00:00 (Saturday)"</returns>
        public static string FormatAsVietnamTimeWithDay(DateTime utcDateTime)
        {
            return FormatAsVietnamTime(utcDateTime, "yyyy-MM-dd HH:mm:ss (dddd)");
        }

        #endregion

        #region Debug and Info Methods

        /// <summary>
        /// Get comprehensive timezone information for debugging
        /// </summary>
        /// <returns>Object with timezone details</returns>
        public static object GetTimezoneInfo()
        {
            var utcNow = UtcNow;
            var vietnamNow = VietnamNow;

            return new
            {
                TimeZoneId = VietnamTimeZone.Id,
                TimeZoneDisplayName = VietnamTimeZone.DisplayName,
                CurrentUtcOffset = VietnamTimeZone.GetUtcOffset(utcNow),
                SupportsDaylightSaving = VietnamTimeZone.SupportsDaylightSavingTime,
                UtcNow = utcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                VietnamNow = vietnamNow.ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                SystemTimeZone = TimeZoneInfo.Local.Id,
                PostgreSqlSafe = true
            };
        }

        /// <summary>
        /// Validate DateTime for PostgreSQL compatibility
        /// </summary>
        /// <param name="dateTime">DateTime to validate</param>
        /// <returns>Validation result with recommendations</returns>
        public static object ValidateForPostgreSQL(DateTime dateTime)
        {
            return new
            {
                OriginalKind = dateTime.Kind.ToString(),
                OriginalValue = dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                IsPostgreSqlSafe = dateTime.Kind == DateTimeKind.Utc,
                RecommendedValue = EnsureUtc(dateTime).ToString("yyyy-MM-dd HH:mm:ss"),
                VietnamEquivalent = FormatAsVietnamTimeWithDay(EnsureUtc(dateTime))
            };
        }

        #endregion
    }
}
