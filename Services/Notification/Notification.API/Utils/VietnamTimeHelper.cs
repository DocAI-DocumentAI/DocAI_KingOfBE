namespace Notification.API.Utils;

public static class VietnamTimeHelper
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // UTC+7

    /// <summary>
    /// Lấy ngày hiện tại theo giờ Việt Nam (chỉ ngày, không có giờ)
    /// </summary>
    public static DateTime GetVietnamDate()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone).Date;
    }

    /// <summary>
    /// Lấy DateTime hiện tại theo giờ Việt Nam (có cả giờ)
    /// </summary>
    public static DateTime GetVietnamDateTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    }

    /// <summary>
    /// Chuyển đổi UTC DateTime sang giờ Việt Nam
    /// </summary>
    public static DateTime ConvertToVietnamTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Utc)
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone);

        // Nếu không phải UTC, giả sử là local time
        return TimeZoneInfo.ConvertTime(utcDateTime, VietnamTimeZone);
    }

    /// <summary>
    /// Kiểm tra xem một ngày có phải là hôm nay theo giờ Việt Nam không
    /// </summary>
    public static bool IsToday(DateTime date)
    {
        var vietnamToday = GetVietnamDate();
        return date.Date == vietnamToday;
    }

    /// <summary>
    /// Tính số ngày từ hôm nay đến ngày chỉ định (theo giờ Việt Nam)
    /// </summary>
    public static int DaysFromToday(DateTime targetDate)
    {
        var vietnamToday = GetVietnamDate();
        return (targetDate.Date - vietnamToday).Days;
    }
}