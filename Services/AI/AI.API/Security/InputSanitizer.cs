using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI.API.Security
{
    /// <summary>
    /// Input sanitization service to prevent prompt injection and malicious inputs
    /// Provides comprehensive validation and sanitization for AI prompts
    /// </summary>
    public class InputSanitizer
    {
        private readonly InputSanitizationOptions _options;
        private readonly ILogger<InputSanitizer> _logger;
        
        // Dangerous patterns that could indicate prompt injection
        private static readonly List<Regex> DangerousPatterns = new()
        {
            new Regex(@"ignore\s+previous\s+instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"forget\s+everything", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"system\s*:\s*you\s+are", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"assistant\s*:\s*i\s+will", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"data\s*:\s*text/html", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"eval\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"exec\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"__import__", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"subprocess", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"os\.system", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        // Suspicious keywords that might indicate attempts to manipulate the AI
        private static readonly HashSet<string> SuspiciousKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "jailbreak", "roleplay", "pretend", "simulate", "act as", "imagine you are",
            "override", "bypass", "circumvent", "hack", "exploit", "vulnerability",
            "admin", "root", "sudo", "administrator", "superuser", "privilege"
        };

        public InputSanitizer(IOptions<InputSanitizationOptions> options, ILogger<InputSanitizer> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Validates and sanitizes input text for AI processing
        /// </summary>
        public SanitizationResult SanitizeInput(string input, string userId = null, string context = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new SanitizationResult
                {
                    IsValid = false,
                    SanitizedInput = string.Empty,
                    Issues = new List<string> { "Input cannot be empty" }
                };
            }

            var result = new SanitizationResult
            {
                OriginalInput = input,
                SanitizedInput = input,
                Issues = new List<string>(),
                RiskLevel = RiskLevel.Low
            };

            // Check length limits
            if (input.Length > _options.MaxInputLength)
            {
                result.Issues.Add($"Input length {input.Length} exceeds maximum of {_options.MaxInputLength}");
                result.IsValid = false;
                return result;
            }

            // Check for dangerous patterns
            var dangerousMatches = CheckDangerousPatterns(input);
            if (dangerousMatches.Any())
            {
                result.Issues.AddRange(dangerousMatches);
                result.RiskLevel = RiskLevel.High;
                
                if (_options.BlockHighRiskInputs)
                {
                    result.IsValid = false;
                    _logger.LogWarning("Blocked high-risk input from user {UserId}: {Issues}", 
                        userId, string.Join(", ", dangerousMatches));
                    return result;
                }
            }

            // Check for suspicious keywords
            var suspiciousMatches = CheckSuspiciousKeywords(input);
            if (suspiciousMatches.Any())
            {
                result.Issues.AddRange(suspiciousMatches.Select(m => $"Suspicious keyword detected: {m}"));
                result.RiskLevel = result.RiskLevel > RiskLevel.Medium ? result.RiskLevel : RiskLevel.Medium;
            }

            // Check for excessive repetition
            if (HasExcessiveRepetition(input))
            {
                result.Issues.Add("Excessive character or word repetition detected");
                result.RiskLevel = result.RiskLevel > RiskLevel.Medium ? result.RiskLevel : RiskLevel.Medium;
            }

            // Check for unusual encoding or hidden characters
            if (HasSuspiciousEncoding(input))
            {
                result.Issues.Add("Suspicious character encoding detected");
                result.RiskLevel = result.RiskLevel > RiskLevel.Medium ? result.RiskLevel : RiskLevel.Medium;
            }

            // Sanitize the input if needed
            if (_options.EnableSanitization)
            {
                result.SanitizedInput = PerformSanitization(input);
            }

            // Final validation
            result.IsValid = result.RiskLevel != RiskLevel.High || !_options.BlockHighRiskInputs;

            // Log suspicious activity
            if (result.RiskLevel >= RiskLevel.Medium)
            {
                _logger.LogWarning("Suspicious input detected from user {UserId} in context {Context}. Risk: {RiskLevel}, Issues: {Issues}",
                    userId, context, result.RiskLevel, string.Join(", ", result.Issues));
            }

            return result;
        }

        /// <summary>
        /// Validates template variables for potential injection
        /// </summary>
        public bool ValidateTemplateVariables(Dictionary<string, string> variables)
        {
            if (variables == null || !variables.Any())
                return true;

            foreach (var kvp in variables)
            {
                var keyResult = SanitizeInput(kvp.Key);
                var valueResult = SanitizeInput(kvp.Value);

                if (!keyResult.IsValid || !valueResult.IsValid || 
                    keyResult.RiskLevel >= RiskLevel.High || valueResult.RiskLevel >= RiskLevel.High)
                {
                    _logger.LogWarning("Invalid template variable detected: {Key} = {Value}", kvp.Key, kvp.Value);
                    return false;
                }
            }

            return true;
        }

        private List<string> CheckDangerousPatterns(string input)
        {
            var matches = new List<string>();

            foreach (var pattern in DangerousPatterns)
            {
                if (pattern.IsMatch(input))
                {
                    matches.Add($"Dangerous pattern detected: {pattern}");
                }
            }

            return matches;
        }

        private List<string> CheckSuspiciousKeywords(string input)
        {
            var words = input.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?' }, 
                StringSplitOptions.RemoveEmptyEntries);

            return words.Where(word => SuspiciousKeywords.Contains(word.Trim())).Distinct().ToList();
        }

        private bool HasExcessiveRepetition(string input)
        {
            // Check for repeated characters
            var charRepetition = Regex.IsMatch(input, @"(.)\1{10,}"); // Same character 10+ times
            
            // Check for repeated words
            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 5)
            {
                var wordCounts = words.GroupBy(w => w.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.Count());
                
                var maxWordCount = wordCounts.Values.Max();
                var wordRepetition = maxWordCount > words.Length * 0.3; // More than 30% repetition
                
                return charRepetition || wordRepetition;
            }

            return charRepetition;
        }

        private bool HasSuspiciousEncoding(string input)
        {
            // Check for unusual Unicode characters
            var suspiciousChars = input.Where(c => 
                char.IsControl(c) && c != '\n' && c != '\r' && c != '\t' ||
                char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format ||
                char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherNotAssigned
            ).Any();

            // Check for potential encoding attacks
            var encodingPatterns = new[]
            {
                @"\\u[0-9a-fA-F]{4}", // Unicode escape sequences
                @"\\x[0-9a-fA-F]{2}", // Hex escape sequences
                @"%[0-9a-fA-F]{2}",   // URL encoding
                @"&#\d+;",            // HTML entities
                @"&[a-zA-Z]+;"        // Named HTML entities
            };

            var hasEncodingPatterns = encodingPatterns.Any(pattern => Regex.IsMatch(input, pattern));

            return suspiciousChars || hasEncodingPatterns;
        }

        private string PerformSanitization(string input)
        {
            // Remove or replace dangerous characters
            var sanitized = input;

            // Remove control characters except common whitespace
            sanitized = Regex.Replace(sanitized, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            // Normalize whitespace
            sanitized = Regex.Replace(sanitized, @"\s+", " ");

            // Remove excessive repetition
            sanitized = Regex.Replace(sanitized, @"(.)\1{5,}", "$1$1$1"); // Limit to 3 repetitions

            // Trim and clean up
            sanitized = sanitized.Trim();

            return sanitized;
        }
    }

    /// <summary>
    /// Configuration options for input sanitization
    /// </summary>
    public class InputSanitizationOptions
    {
        /// <summary>
        /// Maximum allowed input length
        /// </summary>
        public int MaxInputLength { get; set; } = 50000;

        /// <summary>
        /// Whether to block high-risk inputs
        /// </summary>
        public bool BlockHighRiskInputs { get; set; } = true;

        /// <summary>
        /// Whether to perform automatic sanitization
        /// </summary>
        public bool EnableSanitization { get; set; } = true;

        /// <summary>
        /// Whether to log all sanitization attempts
        /// </summary>
        public bool LogAllAttempts { get; set; } = false;
    }

    /// <summary>
    /// Result of input sanitization process
    /// </summary>
    public class SanitizationResult
    {
        public bool IsValid { get; set; } = true;
        public string OriginalInput { get; set; }
        public string SanitizedInput { get; set; }
        public List<string> Issues { get; set; } = new();
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    }

    /// <summary>
    /// Risk levels for input validation
    /// </summary>
    public enum RiskLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}
