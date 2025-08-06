namespace Notification.API.Utils
{
    /// <summary>
    /// Interface for template rendering utility
    /// </summary>
    public interface ITemplateRendererUtil
    {
        /// <summary>
        /// Renders a template with the provided data
        /// </summary>
        /// <param name="templateContent">Template content with placeholders</param>
        /// <param name="data">Data to replace placeholders</param>
        /// <returns>Rendered template content</returns>
        string Render(string templateContent, Dictionary<string, string>? data);
    }
}
