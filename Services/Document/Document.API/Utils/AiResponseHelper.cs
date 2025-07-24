using System.Text.Json;
using System.Text.RegularExpressions;
using Document.API.Payload.Response;
using Microsoft.KernelMemory;
using Microsoft.Extensions.Logging;

namespace Document.API.Utils
{
    /// <summary>
    /// Helper class for handling AI response parsing, truncation detection, and fallback analysis
    /// </summary>
    public class AiResponseHelper
    {
        private readonly ILogger<AiResponseHelper> _logger;
        private readonly IKernelMemory _memory;

        public AiResponseHelper(ILogger<AiResponseHelper> logger, IKernelMemory memory)
        {
            _logger = logger;
            _memory = memory;
        }

        /// <summary>
        /// Parses AI JSON response with enhanced error handling for partial/truncated responses
        /// </summary>
        public void ParseAiJsonResponse(string jsonResponse, AnalyzeDocumentResponse response)
        {
            try
            {
                // Step 1: Sanitize possible markdown wrappers
                var cleanJson = jsonResponse.Trim().Trim('`').Replace("json", "").Trim();

                // Step 2: Try to isolate the valid JSON portion (from first { to last })
                int startIndex = cleanJson.IndexOf('{');
                int endIndex = cleanJson.LastIndexOf('}');

                if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
                    throw new JsonException("JSON is not enclosed properly.");

                var jsonFragment = cleanJson.Substring(startIndex, endIndex - startIndex + 1);

                // Step 3: Try parsing with enhanced error handling for partial JSON
                JsonDocument jsonDoc = null;
                try
                {
                    jsonDoc = JsonDocument.Parse(jsonFragment);
                }
                catch (JsonException)
                {
                    // If parsing fails, try to fix common truncation issues
                    jsonFragment = AttemptJsonRepair(jsonFragment);
                    jsonDoc = JsonDocument.Parse(jsonFragment);
                }

                using (jsonDoc)
                {
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        response.Title = title.GetString();

                    if (root.TryGetProperty("versionName", out var versionName) && versionName.ValueKind == JsonValueKind.String)
                        response.VersionName = versionName.GetString();

                    if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
                        response.Summary = summary.GetString();

                    if (root.TryGetProperty("signedBy", out var signedBy) && signedBy.ValueKind == JsonValueKind.String)
                        response.SignedBy = signedBy.GetString();

                    if (root.TryGetProperty("effectiveFrom", out var effectiveFrom) && effectiveFrom.ValueKind == JsonValueKind.String)
                        if (DateTime.TryParse(effectiveFrom.GetString(), out var fromDate))
                            response.EffectiveFrom = fromDate;

                    if (root.TryGetProperty("effectiveUntil", out var effectiveUntil) && effectiveUntil.ValueKind == JsonValueKind.String)
                        if (DateTime.TryParse(effectiveUntil.GetString(), out var untilDate))
                            response.EffectiveUntil = untilDate;

                    if (root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                    {
                        response.Tags = tags.EnumerateArray()
                            .Select(t => t.GetString())
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();
                    }
                }
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "Failed to parse JSON response from AI. Possibly incomplete or malformed. Raw response: {AiResponse}", jsonResponse);
                
                // Try to extract any partial information using regex as fallback
                AttemptPartialExtraction(jsonResponse, response);
            }
        }

        /// <summary>
        /// Detects if the AI response appears to be truncated based on common patterns
        /// </summary>
        public bool IsResponseTruncated(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse))
                return true;

            // Check for incomplete JSON patterns
            var cleanResponse = aiResponse.Trim();
            
            // Count opening and closing braces
            int openBraces = cleanResponse.Count(c => c == '{');
            int closeBraces = cleanResponse.Count(c => c == '}');
            
            // If braces don't match, likely truncated
            if (openBraces != closeBraces)
            {
                _logger.LogWarning("Detected brace mismatch: {OpenBraces} open, {CloseBraces} close", openBraces, closeBraces);
                return true;
            }

            // Check for incomplete JSON strings (unmatched quotes)
            var quoteCount = cleanResponse.Count(c => c == '"');
            if (quoteCount % 2 != 0)
            {
                _logger.LogWarning("Detected unmatched quotes in response");
                return true;
            }

            // Check if response ends abruptly without proper JSON closure
            if (!cleanResponse.TrimEnd().EndsWith("}"))
            {
                _logger.LogWarning("Response does not end with proper JSON closure");
                return true;
            }

            // Check for common truncation indicators
            var truncationIndicators = new[] { "...", "[truncated]", "[cut off]", "incomplete" };
            if (truncationIndicators.Any(indicator => cleanResponse.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Found truncation indicators in response");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts fallback analysis using simpler, targeted prompts when main analysis fails
        /// </summary>
        public async Task AttemptFallbackAnalysis(string tempDocId, AnalyzeDocumentResponse response, string fileName)
        {
            try
            {
                var filter = new MemoryFilter().ByDocument(tempDocId);
                
                // Try to get basic information with very simple prompts
                await ExtractBasicInfo(filter, response, fileName);
                
                _logger.LogInformation("Fallback analysis completed for file: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback analysis also failed for file: {FileName}", fileName);
                // Set minimal fallback values
                response.Title = response.Title ?? "Document Analysis Incomplete";
                response.Summary = response.Summary ?? "<b>Analysis Incomplete</b> - The document could not be fully analyzed due to size limitations.";
                response.Tags = response.Tags ?? new List<string> { "analysis-incomplete" };
            }
        }

        /// <summary>
        /// Attempts to repair common JSON truncation issues
        /// </summary>
        private string AttemptJsonRepair(string jsonFragment)
        {
            _logger.LogInformation("Attempting to repair truncated JSON");
            
            // Remove any trailing incomplete strings or properties
            var lines = jsonFragment.Split('\n');
            var repairedLines = new List<string>();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip lines that appear incomplete (no closing quote, incomplete property)
                if (trimmedLine.EndsWith("\"") || 
                    trimmedLine.EndsWith(",") || 
                    trimmedLine.EndsWith(":") ||
                    trimmedLine == "{" ||
                    trimmedLine == "}")
                {
                    repairedLines.Add(line);
                }
                else if (trimmedLine.Contains(":") && trimmedLine.Contains("\""))
                {
                    // Try to complete incomplete string values
                    if (trimmedLine.Count(c => c == '"') % 2 != 0)
                    {
                        repairedLines.Add(line + "\"");
                    }
                    else
                    {
                        repairedLines.Add(line);
                    }
                }
            }
            
            var repairedJson = string.Join("\n", repairedLines);
            
            // Ensure proper JSON closure
            if (!repairedJson.TrimEnd().EndsWith("}"))
            {
                repairedJson = repairedJson.TrimEnd().TrimEnd(',') + "\n}";
            }
            
            _logger.LogInformation("Repaired JSON: {RepairedJson}", repairedJson);
            return repairedJson;
        }

        /// <summary>
        /// Attempts to extract partial information using regex when JSON parsing fails completely
        /// </summary>
        private void AttemptPartialExtraction(string jsonResponse, AnalyzeDocumentResponse response)
        {
            _logger.LogInformation("Attempting partial extraction from malformed JSON");
            
            try
            {
                // Extract title using regex
                var titleMatch = Regex.Match(jsonResponse, @"""title""\s*:\s*""([^""]+)""");
                if (titleMatch.Success && string.IsNullOrEmpty(response.Title))
                {
                    response.Title = titleMatch.Groups[1].Value;
                    _logger.LogInformation("Extracted title via regex: {Title}", response.Title);
                }

                // Extract summary using regex
                var summaryMatch = Regex.Match(jsonResponse, @"""summary""\s*:\s*""([^""]+)""");
                if (summaryMatch.Success && string.IsNullOrEmpty(response.Summary))
                {
                    response.Summary = summaryMatch.Groups[1].Value;
                    _logger.LogInformation("Extracted summary via regex for partial response");
                }

                // Extract signedBy using regex
                var signedByMatch = Regex.Match(jsonResponse, @"""signedBy""\s*:\s*""([^""]+)""");
                if (signedByMatch.Success && string.IsNullOrEmpty(response.SignedBy))
                {
                    response.SignedBy = signedByMatch.Groups[1].Value;
                    _logger.LogInformation("Extracted signedBy via regex: {SignedBy}", response.SignedBy);
                }

                // Extract dates using regex
                var effectiveFromMatch = Regex.Match(jsonResponse, @"""effectiveFrom""\s*:\s*""([^""]+)""");
                if (effectiveFromMatch.Success && !response.EffectiveFrom.HasValue)
                {
                    if (DateTime.TryParse(effectiveFromMatch.Groups[1].Value, out var fromDate))
                    {
                        response.EffectiveFrom = fromDate;
                        _logger.LogInformation("Extracted effectiveFrom via regex: {EffectiveFrom}", response.EffectiveFrom);
                    }
                }

                var effectiveUntilMatch = Regex.Match(jsonResponse, @"""effectiveUntil""\s*:\s*""([^""]+)""");
                if (effectiveUntilMatch.Success && !response.EffectiveUntil.HasValue)
                {
                    if (DateTime.TryParse(effectiveUntilMatch.Groups[1].Value, out var untilDate))
                    {
                        response.EffectiveUntil = untilDate;
                        _logger.LogInformation("Extracted effectiveUntil via regex: {EffectiveUntil}", response.EffectiveUntil);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Partial extraction also failed");
            }
        }

        /// <summary>
        /// Extracts basic information using simple, targeted prompts
        /// </summary>
        private async Task ExtractBasicInfo(MemoryFilter filter, AnalyzeDocumentResponse response, string fileName)
        {
            // Extract title if not already set
            if (string.IsNullOrEmpty(response.Title))
            {
                var titleAnswer = await _memory.AskAsync("What is the title of this document? Respond with just the title.", filter: filter);
                if (!titleAnswer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    response.Title = titleAnswer.Result.Trim().Replace("\"", "").Trim();
                    _logger.LogInformation("Extracted title via fallback: {Title}", response.Title);
                }
            }

            // Extract basic summary if not already set
            if (string.IsNullOrEmpty(response.Summary) || response.Summary == "AI analysis could not be completed.")
            {
                var summaryAnswer = await _memory.AskAsync("Provide a brief 2-3 sentence summary of this document's main purpose.", filter: filter);
                if (!summaryAnswer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    response.Summary = $"<b>{response.Title ?? "Document"}</b> - {summaryAnswer.Result.Trim()}";
                    _logger.LogInformation("Extracted summary via fallback for file: {FileName}", fileName);
                }
            }

            // Extract signatory if not already set
            if (string.IsNullOrEmpty(response.SignedBy))
            {
                var signatoryAnswer = await _memory.AskAsync("Who signed this document? Respond with just the name or 'Unknown'.", filter: filter);
                if (!signatoryAnswer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase) && 
                    !signatoryAnswer.Result.Contains("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    response.SignedBy = signatoryAnswer.Result.Trim().Replace("\"", "").Trim();
                    _logger.LogInformation("Extracted signatory via fallback: {SignedBy}", response.SignedBy);
                }
            }

            // Extract basic tags if not already set
            if (response.Tags == null || !response.Tags.Any())
            {
                var tagsAnswer = await _memory.AskAsync("List 3 keywords that describe this document. Respond with just the keywords separated by commas.", filter: filter);
                if (!tagsAnswer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    response.Tags = tagsAnswer.Result.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(tag => tag.Trim().Replace("\"", "").Trim())
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Take(5)
                        .ToList();
                    _logger.LogInformation("Extracted tags via fallback: {Tags}", string.Join(", ", response.Tags));
                }
            }
        }
    }
}
