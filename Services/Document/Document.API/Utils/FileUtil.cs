using DocumentFormat.OpenXml.Packaging;
using System.Security.AccessControl;
using System.Text;
using UglyToad.PdfPig;

namespace Document.API.Utils
{
    public class FileUtil
    {
        //private readonly ILogger<FileUtil> _logger;
        //public FileUtil(ILogger<FileUtil> logger)
        //{
        //    _logger = logger;
        //}
        public string EtractText(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return ext switch
            {
                ".pdf" => ExtractPdfText(filePath),
                ".docx" => ExtractDocxText(filePath),
                _ => throw new NotSupportedException($"File type {ext} is not supported.")
            };
        }
        private string ExtractPdfText(string filePath)
        {
            var sb = new StringBuilder();
            using var pdf = PdfDocument.Open(filePath);
            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return sb.ToString();
        }

        private string ExtractDocxText(string filePath)
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            return doc.MainDocumentPart.Document.Body.InnerText;
        }

        public List<string> SplitTextIntoChunks(string text, int minChunkSize = 300, int maxChunkSize = 500, double overlapRatio = 0.1)
        {
            var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();
            int totalWords = words.Length;
            int start = 0;

            // Use a fixed chunk size (e.g., 400) or randomize between min and max if desired
            int chunkSize = Math.Min(maxChunkSize, Math.Max(minChunkSize, 400));
            int overlap = (int)(chunkSize * overlapRatio);

            while (start < totalWords)
            {
                int end = Math.Min(start + chunkSize, totalWords);
                var chunkWords = words.Skip(start).Take(end - start).ToArray();
                chunks.Add(string.Join(" ", chunkWords));
                if (end == totalWords) break;
                start += chunkSize - overlap;
            }

            return chunks;
        }
    }
}
