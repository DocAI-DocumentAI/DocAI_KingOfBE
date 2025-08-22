namespace Document.API.Utils;

public static class VietnamTimeHelper
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    public static DateTime GetVietnamDateTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    }

    public static DateTime GetVietnamDate()
    {
        return GetVietnamDateTime().Date;
    }

    // ✅ NEW: Convert UTC to Vietnam time
    public static DateTime ConvertUtcToVietnam(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone);
    }

    // ✅ NEW: Convert Vietnam time to UTC
    public static DateTime ConvertVietnamToUtc(DateTime vietnamDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(vietnamDateTime, VietnamTimeZone);
    }

    // ✅ NEW: Calculate days from today (Vietnam time)
    public static int DaysFromToday(DateTime targetDate)
    {
        var vietnamToday = GetVietnamDate();
        var targetVietnamDate = ConvertUtcToVietnam(targetDate).Date;
        return (targetVietnamDate - vietnamToday).Days;
    }

    // ✅ NEW: Get UTC date that's safe for PostgreSQL queries
    public static DateTime GetUtcDateForQuery()
    {
        return DateTime.UtcNow.Date;
    }

    // ✅ NEW: Ensure DateTime is UTC for database operations
    public static DateTime EnsureUtc(DateTime dateTime)
    {
        return dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}