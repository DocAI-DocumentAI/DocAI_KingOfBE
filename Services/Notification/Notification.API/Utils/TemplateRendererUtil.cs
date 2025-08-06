using System.Text.RegularExpressions;

namespace Notification.API.Utils
{
    public class TemplateRendererUtil : ITemplateRendererUtil
    {
        public string Render(string templateContent, Dictionary<string, string>? data)
        {
            if (string.IsNullOrEmpty(templateContent) || data == null || data.Count == 0)
            {
                return templateContent ?? string.Empty;
            }

            string renderedContent = templateContent;
            foreach (var entry in data)
            {
                renderedContent = Regex.Replace(
                    renderedContent,
                    $"\\{{{{{Regex.Escape(entry.Key)}}}\\}}", // Matches {{Key}}
                    entry.Value,
                    RegexOptions.IgnoreCase);
            }
            return renderedContent;
        }
    }
}
