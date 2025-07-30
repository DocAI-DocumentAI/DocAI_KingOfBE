using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ChatBox.API.Plugins
{
    public class TimePlugin
    {
        [KernelFunction]
        [Description("Lấy thời gian hiện tại")]
        public string GetCurrentTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        [KernelFunction]
        [Description("Lấy ngày hiện tại")]
        public string GetCurrentDate()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        [KernelFunction]
        [Description("Tính số ngày giữa hai thời điểm")]
        public int DaysBetween(
            [Description("Ngày bắt đầu (yyyy-MM-dd)")] string startDate,
            [Description("Ngày kết thúc (yyyy-MM-dd)")] string endDate)
        {
            if (DateTime.TryParse(startDate, out var start) && DateTime.TryParse(endDate, out var end))
            {
                return (int)(end - start).TotalDays;
            }

            return 0;
        }
    }
}
