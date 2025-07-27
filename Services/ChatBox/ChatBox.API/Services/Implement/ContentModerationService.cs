using AutoMapper;
using ChatBox.API.Payload.Response.ContentModerationServiceResponse;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Enum;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChatBox.API.Services.Implement
{
    public class ContentModerationService : IContentModerationService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IAuditService _auditService;
        private readonly ILogger<ContentModerationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        public ContentModerationService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IAuditService auditService,
            ILogger<ContentModerationService> logger,
            IConfiguration configuration,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _auditService = auditService;
            _logger = logger;
            _configuration = configuration;
            _mapper = mapper;
        }


        public async Task<ContentModerationResponse> ModerateContentAsync(string content, Guid? userId)
        {
            var moderationId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Starting content moderation for user {UserId}, ModerationId: {ModerationId}",
                    userId, moderationId);

                var response = new ContentModerationResponse
                {
                    ModerationId = moderationId,
                    ModerationTimestamp = DateTime.UtcNow,
                    Violations = new List<ContentViolation>(),
                    ViolatedRules = new List<string>()
                };

                if (string.IsNullOrWhiteSpace(content))
                {
                    response.IsApproved = true;
                    response.Reason = _configuration["ContentModeration:Messages:EmptyContent"] ?? "Content is empty";
                    response.Action = "approve";
                    response.ConfidenceScore = 1.0;
                    response.Severity = ContentSeverity.Low;
                    return response;
                }

                var maxContentLength = _configuration.GetValue<int>("ContentModeration:MaxContentLength", 10000);
                if (content.Length > maxContentLength)
                {
                    response.IsApproved = false;
                    response.Reason = _configuration["ContentModeration:Messages:ContentTooLong"] ?? "Content exceeds maximum length";
                    response.Action = "block";
                    response.ConfidenceScore = 1.0;
                    response.Severity = ContentSeverity.High;
                    return response;
                }

                // Get active moderation rules
                var moderationRules = await GetActiveModerationRulesAsync();

                // Check user moderation profile
                var userProfile = userId.HasValue ? await GetUserModerationProfileAsync(userId.Value) : null;

                // Apply content filters
                await ApplyModerationRulesAsync(content, moderationRules, response);

                // Apply user-specific moderation
                if (userProfile != null)
                {
                    await ApplyUserSpecificModerationAsync(content, userProfile, response);
                }

                // Calculate overall moderation result
                CalculateModerationResult(response);

                // Apply user context adjustments
                if (userProfile != null)
                {
                    AdjustModerationForUser(response, userProfile);
                }

                // Log moderation result
                await LogModerationResultAsync(content, response, userId);

                // Update user moderation profile if needed
                if (userId.HasValue && !response.IsApproved)
                {
                    await UpdateUserModerationProfileAsync(userId.Value, response);
                }

                // Audit trail
                await _auditService.LogAsync(userId, "ContentModeration", "Content", moderationId,
                    null, new { ContentLength = content.Length, IsApproved = response.IsApproved, ViolationCount = response.Violations.Count, Action = response.Action });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in content moderation for user {UserId}, ModerationId: {ModerationId}",
                    userId, moderationId);

                await _auditService.LogSecurityEventAsync(userId, "ContentModerationError",
                    $"Content moderation failed: {ex.Message}", "medium");

                return new ContentModerationResponse
                {
                    IsApproved = false,
                    Reason = _configuration["ContentModeration:Messages:SystemError"] ?? "Content moderation system error - content blocked for safety",
                    Action = "block",
                    ConfidenceScore = 0.9,
                    Severity = ContentSeverity.High,
                    ModerationId = moderationId,
                    ModerationTimestamp = DateTime.UtcNow
                };
            }
        }
        public async Task<bool> IsContentSafeAsync(string content)
        {
            try
            {
                var moderationResult = await ModerateContentAsync(content, null);
                var safetyThreshold = _configuration.GetValue<double>("ContentModeration:SafetyThreshold", 0.7);
                return moderationResult.IsApproved && (double)moderationResult.Severity <= safetyThreshold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking content safety");
                return false; // Fail safe
            }
        }

        public async Task<List<string>> DetectProhibitedTermsAsync(string content)
        {
            try
            {
                var prohibitedTerms = new List<string>();

                if (string.IsNullOrEmpty(content))
                    return prohibitedTerms;

                var moderationRules = await GetActiveModerationRulesAsync();

                foreach (var rule in moderationRules.Where(r => r.RuleType == "keyword"))
                {
                    var detectedTerms = await DetectTermsInContentAsync(content, rule);
                    prohibitedTerms.AddRange(detectedTerms);
                }

                return prohibitedTerms.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting prohibited terms");
                return new List<string>();
            }
        }

        public async Task<bool> IsUserFlaggedAsync(Guid userId)
        {
            try
            {
                var userProfile = await GetUserModerationProfileAsync(userId);
                return userProfile?.IsFlagged == true &&
                       (!userProfile.FlaggedUntil.HasValue || userProfile.FlaggedUntil.Value > DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is flagged", userId);
                return false;
            }
        }

        public async Task UpdateModerationRulesAsync(List<ModerationRule> rules)
        {
            try
            {
                _logger.LogInformation("Updating {RuleCount} moderation rules", rules.Count);

                var ruleRepo = _unitOfWork.GetRepository<ContentModerationRule>();

                foreach (var rule in rules)
                {
                    var existingRule = await ruleRepo.SingleOrDefaultAsync(predicate: r => r.Name == rule.Name);

                    if (existingRule != null)
                    {
                        _mapper.Map(rule, existingRule);
                        existingRule.UpdatedAt = DateTime.UtcNow;
                        ruleRepo.UpdateAsync(existingRule);
                    }
                    else
                    {
                        var newRule = _mapper.Map<ContentModerationRule>(rule);
                        newRule.Id = Guid.NewGuid();
                        newRule.CreatedAt = DateTime.UtcNow;
                        newRule.CreatedBy = _configuration["ContentModeration:DefaultCreatedBy"] ?? "system";
                        await ruleRepo.InsertAsync(newRule);
                    }
                }

                await _unitOfWork.CommitAsync();

                await _auditService.LogAsync(null, "UpdateModerationRules", "ModerationRules", "bulk_update",
                    null, new { UpdatedRuleCount = rules.Count });

                _logger.LogInformation("Successfully updated {RuleCount} moderation rules", rules.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating moderation rules");
                throw;
            }
        }

        private async Task<List<ModerationRule>> GetActiveModerationRulesAsync()
        {
            try
            {
                var ruleRepo = _unitOfWork.GetRepository<ContentModerationRule>();
                var dbRules = await ruleRepo.GetListAsync(predicate:
                    r => r.IsActive,
                    orderBy: r => r.OrderBy(x => x.Priority));

                var moderationRules = new List<ModerationRule>();

                // Add default rules
                moderationRules.AddRange(DefaultRules.Values);

                // Add custom rules from database
                foreach (var dbRule in dbRules)
                {
                    var rule = new ModerationRule
                    {
                        Id = dbRule.Id.ToString(),
                        Name = dbRule.Name,
                        Category = dbRule.Category,
                        RuleType = dbRule.RuleType,
                        Pattern = dbRule.Pattern,
                        Keywords = ParseJsonToList(dbRule.Keywords),
                        Description = dbRule.Description,
                        Severity = dbRule.Severity,
                        Action = dbRule.Action,
                        IsActive = dbRule.IsActive,
                        IsCaseSensitive = dbRule.IsCaseSensitive,
                        IsWholeWordOnly = dbRule.IsWholeWordOnly,
                        Configuration = ParseJsonToDictionary(dbRule.Configuration),
                        CreatedAt = dbRule.CreatedAt,
                        UpdatedAt = dbRule.UpdatedAt
                    };

                    moderationRules.Add(rule);
                }

                return moderationRules;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting moderation rules, using defaults only");
                return DefaultRules.Values.ToList();
            }
        }

        private async Task<UserModerationProfile> GetUserModerationProfileAsync(Guid userId)
        {
            try
            {
                var historyRepo = _unitOfWork.GetRepository<UserModerationHistory>();
                var userViolations = await historyRepo.GetListAsync(predicate:
                    h => h.UserId == userId && h.IsActive,
                    orderBy: h => h.OrderByDescending(x => x.ViolationDate));

                var profile = new UserModerationProfile
                {
                    UserId = userId,
                    ViolationCount = userViolations.Count,
                    LastViolation = userViolations.FirstOrDefault()?.ViolationDate,
                    ViolationHistory = userViolations.Select(v => v.ViolationType).ToList(),
                    TrustScore = CalculateUserTrustScore(userViolations.ToList()),
                    AllowedExceptions = new List<string>(),
                    ModerationMetrics = new Dictionary<string, object>()
                };

                // Check if user is currently flagged
                var recentViolations = userViolations.Where(v => v.ViolationDate > DateTime.UtcNow.AddDays(-7)).ToList();
                if (recentViolations.Count >= 3)
                {
                    profile.IsFlagged = true;
                    profile.FlagReason = "Multiple recent violations";
                    profile.FlaggedUntil = DateTime.UtcNow.AddDays(1);
                }

                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting user moderation profile for {UserId}", userId);
                return new UserModerationProfile
                {
                    UserId = userId,
                    ViolationCount = 0,
                    TrustScore = 0.5,
                    IsFlagged = false
                };
            }
        }

        private async Task ApplyModerationRulesAsync(string content, List<ModerationRule> rules, ContentModerationResponse response)
        {
            foreach (var rule in rules.Where(r => r.IsActive))
            {
                try
                {
                    var violations = await CheckRuleViolationAsync(content, rule);
                    response.Violations.AddRange(violations);

                    if (violations.Any())
                    {
                        response.ViolatedRules.Add(rule.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error applying moderation rule {RuleName}", rule.Name);
                }
            }
        }

        private async Task<List<ContentViolation>> CheckRuleViolationAsync(string content, ModerationRule rule)
        {
            var violations = new List<ContentViolation>();

            try
            {
                switch (rule.RuleType.ToLower())
                {
                    case "keyword":
                        violations.AddRange(CheckKeywordViolations(content, rule));
                        break;
                    case "pattern":
                        violations.AddRange(CheckPatternViolations(content, rule));
                        break;
                    case "ml_model":
                        violations.AddRange(await CheckMLModelViolations(content, rule));
                        break;
                    case "external_api":
                        violations.AddRange(await CheckExternalAPIViolations(content, rule));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking rule violation for rule {RuleId}", rule.Id);
            }

            return violations;
        }

        private List<ContentViolation> CheckKeywordViolations(string content, ModerationRule rule)
        {
            var violations = new List<ContentViolation>();
            var contentToCheck = rule.IsCaseSensitive ? content : content.ToLower();

            foreach (var keyword in rule.Keywords)
            {
                var keywordToCheck = rule.IsCaseSensitive ? keyword : keyword.ToLower();

                if (rule.IsWholeWordOnly)
                {
                    var pattern = $@"\b{Regex.Escape(keywordToCheck)}\b";
                    var matches = Regex.Matches(contentToCheck, pattern, RegexOptions.IgnoreCase);

                    foreach (Match match in matches)
                    {
                        violations.Add(CreateViolation(rule, match.Value, match.Index, match.Length, content));
                    }
                }
                else
                {
                    var index = contentToCheck.IndexOf(keywordToCheck);
                    while (index >= 0)
                    {
                        violations.Add(CreateViolation(rule, keyword, index, keyword.Length, content));
                        index = contentToCheck.IndexOf(keywordToCheck, index + 1);
                    }
                }
            }

            return violations;
        }

        private List<ContentViolation> CheckPatternViolations(string content, ModerationRule rule)
        {
            var violations = new List<ContentViolation>();

            try
            {
                var regexOptions = rule.IsCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(rule.Pattern, regexOptions);
                var matches = regex.Matches(content);

                foreach (Match match in matches)
                {
                    violations.Add(CreateViolation(rule, match.Value, match.Index, match.Length, content));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in pattern matching for rule {RuleId}", rule.Id);
            }

            return violations;
        }

        private async Task<List<ContentViolation>> CheckMLModelViolations(string content, ModerationRule rule)
        {
            // Placeholder for ML model integration
            // In a real implementation, this would call a machine learning service
            var violations = new List<ContentViolation>();

            try
            {
                // Simulate ML model call
                await Task.Delay(10); // Simulate API call delay

                // Basic sentiment analysis simulation
                var negativeWords = new[] { "hate", "terrible", "awful", "disgusting", "horrible" };
                var negativeCount = negativeWords.Count(word => content.ToLower().Contains(word));

                if (negativeCount >= 2)
                {
                    violations.Add(new ContentViolation
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        ViolationType = "ml_negative_sentiment",
                        Description = "ML model detected negative sentiment",
                        Severity = rule.Severity,
                        MatchedContent = content.Length > 100 ? content.Substring(0, 100) + "..." : content,
                        Position = 0,
                        Length = content.Length,
                        SuggestedActions = new List<string> { "Review content", "Apply sentiment filter" }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in ML model checking for rule {RuleId}", rule.Id);
            }

            return violations;
        }

        private async Task<List<ContentViolation>> CheckExternalAPIViolations(string content, ModerationRule rule)
        {
            // Placeholder for external API integration
            var violations = new List<ContentViolation>();

            try
            {
                // Simulate external API call
                await Task.Delay(50);

                // This would typically call services like:
                // - Google Cloud Natural Language API
                // - Azure Content Moderator
                // - AWS Comprehend
                // For demo, return empty violations
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in external API checking for rule {RuleId}", rule.Id);
            }

            return violations;
        }

        private ContentViolation CreateViolation(ModerationRule rule, string matchedContent, int position, int length, string fullContent)
        {
            return new ContentViolation
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                ViolationType = rule.Category,
                Description = rule.Description,
                Severity = rule.Severity,
                MatchedContent = matchedContent,
                Position = position,
                Length = length,
                SuggestedActions = GetSuggestedActions(rule.Action, rule.Category),
                Context = new Dictionary<string, object>
                {
                    { "ContextBefore", GetContext(fullContent, position, -20) },
                    { "ContextAfter", GetContext(fullContent, position + length, 20) },
                    { "RuleAction", rule.Action }
                }
            };
        }

        private async Task ApplyUserSpecificModerationAsync(string content, UserModerationProfile userProfile, ContentModerationResponse response)
        {
            // Apply stricter moderation for flagged users
            if (userProfile.IsFlagged)
            {
                response.Violations.Add(new ContentViolation
                {
                    RuleId = "user_flagged",
                    RuleName = "Flagged User Content",
                    ViolationType = "user_status",
                    Description = "Content from flagged user requires additional review",
                    Severity = 0.8,
                    MatchedContent = "User Status",
                    Position = 0,
                    Length = 0,
                    SuggestedActions = new List<string> { "Manual review", "Enhanced monitoring" }
                });
            }

            // Apply trust score adjustments
            if (userProfile.TrustScore < 0.3)
            {
                response.Violations.Add(new ContentViolation
                {
                    RuleId = "low_trust_score",
                    RuleName = "Low Trust Score",
                    ViolationType = "user_behavior",
                    Description = "User has low trust score due to violation history",
                    Severity = 0.6,
                    MatchedContent = "Trust Score",
                    Position = 0,
                    Length = 0,
                    SuggestedActions = new List<string> { "Additional verification", "Content review" }
                });
            }

            // Check for repeat violations of same type
            var violationTypes = response.Violations.Select(v => v.ViolationType).Distinct();
            foreach (var violationType in violationTypes)
            {
                if (userProfile.ViolationHistory.Count(v => v == violationType) >= 2)
                {
                    response.Violations.Add(new ContentViolation
                    {
                        RuleId = "repeat_violation",
                        RuleName = "Repeat Violation Pattern",
                        ViolationType = "pattern_behavior",
                        Description = $"User has repeated {violationType} violations",
                        Severity = 0.7,
                        MatchedContent = "Violation Pattern",
                        Position = 0,
                        Length = 0,
                        SuggestedActions = new List<string> { "Progressive discipline", "Account review" }
                    });
                }
            }
        }

        private void CalculateModerationResult(ContentModerationResponse response)
        {
            if (!response.Violations.Any())
            {
                response.IsApproved = true;
                response.Action = "approve";
                response.Reason = "No violations detected";
                response.ConfidenceScore = 1.0;
                response.Severity = ContentSeverity.Low;
                return;
            }

            // Calculate severity and confidence
            var maxSeverity = response.Violations.Max(v => v.Severity);
            var avgSeverity = response.Violations.Average(v => v.Severity);
            var violationCount = response.Violations.Count;

            // Determine severity level
            response.Severity = maxSeverity switch
            {
                >= 0.9 => ContentSeverity.Critical,
                >= 0.7 => ContentSeverity.High,
                >= 0.4 => ContentSeverity.Medium,
                _ => ContentSeverity.Low
            };

            // Determine action based on highest severity violation
            var highestSeverityViolation = response.Violations.OrderByDescending(v => v.Severity).First();
            var ruleAction = highestSeverityViolation.Context.ContainsKey("RuleAction") ?
                highestSeverityViolation.Context["RuleAction"].ToString() : "flag";

            // Apply escalation based on violation count
            if (violationCount >= 3 && maxSeverity >= 0.5)
            {
                response.Action = "block";
                response.IsApproved = false;
                response.Reason = $"Multiple violations detected ({violationCount}) with high severity";
            }
            else if (maxSeverity >= 0.8)
            {
                response.Action = "block";
                response.IsApproved = false;
                response.Reason = "High severity violation detected";
            }
            else if (maxSeverity >= 0.6)
            {
                response.Action = "flag";
                response.IsApproved = false;
                response.Reason = "Moderate violations detected - requires review";
            }
            else if (maxSeverity >= 0.3)
            {
                response.Action = "warn";
                response.IsApproved = true;
                response.Reason = "Minor violations detected - content approved with warning";
            }
            else
            {
                response.Action = "approve";
                response.IsApproved = true;
                response.Reason = "Low severity violations - content approved";
            }

            // Calculate confidence score
            response.ConfidenceScore = Math.Min(avgSeverity + (violationCount * 0.1), 1.0);
        }

        private void AdjustModerationForUser(ContentModerationResponse response, UserModerationProfile userProfile)
        {
            // Adjust based on user trust score
            if (userProfile.TrustScore > 0.8 && response.Severity <= ContentSeverity.Medium)
            {
                // High trust users get benefit of doubt for medium violations
                response.Action = "approve";
                response.IsApproved = true;
                response.Reason += " (Approved due to high user trust score)";
                response.ConfidenceScore *= 0.8; // Reduce confidence slightly
            }
            else if (userProfile.TrustScore < 0.3)
            {
                // Low trust users get stricter moderation
                if (response.Action == "approve" || response.Action == "warn")
                {
                    response.Action = "flag";
                    response.IsApproved = false;
                    response.Reason += " (Flagged due to low user trust score)";
                }
            }

            // Escalate for repeat offenders
            if (userProfile.ViolationCount >= 5 && response.Severity >= ContentSeverity.Medium)
            {
                response.Action = "block";
                response.IsApproved = false;
                response.Reason += " (Blocked due to violation history)";
            }
        }

        private async Task LogModerationResultAsync(string content, ContentModerationResponse response, Guid? userId)
        {
            try
            {
                var moderationLogRepo = _unitOfWork.GetRepository<ModerationLog>();

                var log = new ModerationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Content = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content,
                    ModerationResult = response.IsApproved ? "approved" : "rejected",
                    Action = response.Action,
                    Reason = response.Reason,
                    ViolatedRules = JsonSerializer.Serialize(response.ViolatedRules, _jsonOptions),
                    ConfidenceScore = response.ConfidenceScore,
                    RequiredHumanReview = response.Severity >= ContentSeverity.High,
                    ReviewStatus = "pending",
                    CreatedAt = DateTime.UtcNow,
                    Metadata = response.Metadata
                };

                await moderationLogRepo.InsertAsync(log);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging moderation result");
            }
        }

        private async Task UpdateUserModerationProfileAsync(Guid userId, ContentModerationResponse response)
        {
            try
            {
                if (response.Severity <= ContentSeverity.Low)
                    return; // Don't record minor violations

                var historyRepo = _unitOfWork.GetRepository<UserModerationHistory>();

                var historyEntry = new UserModerationHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ViolationType = response.Violations.FirstOrDefault()?.ViolationType ?? "unknown",
                    Content = "Content redacted for privacy",
                    Action = response.Action,
                    Reason = response.Reason,
                    Severity = response.Violations.Max(v => v.Severity),
                    ViolationDate = DateTime.UtcNow,
                    ReviewStatus = "pending",
                    IsActive = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(30), // Violations expire after 30 days
                    Details = new Dictionary<string, object>
                    {
                        { "ViolationCount", response.Violations.Count },
                        { "ModerationId", response.ModerationId },
                        { "Severity", response.Severity.ToString() }
                    }
                };

                await historyRepo.InsertAsync(historyEntry);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user moderation profile for user {UserId}", userId);
            }
        }

        private async Task<List<string>> DetectTermsInContentAsync(string content, ModerationRule rule)
        {
            var detectedTerms = new List<string>();

            if (rule.RuleType == "keyword")
            {
                var contentToCheck = rule.IsCaseSensitive ? content : content.ToLower();

                foreach (var keyword in rule.Keywords)
                {
                    var keywordToCheck = rule.IsCaseSensitive ? keyword : keyword.ToLower();

                    if (rule.IsWholeWordOnly)
                    {
                        var pattern = $@"\b{Regex.Escape(keywordToCheck)}\b";
                        if (Regex.IsMatch(contentToCheck, pattern))
                        {
                            detectedTerms.Add(keyword);
                        }
                    }
                    else
                    {
                        if (contentToCheck.Contains(keywordToCheck))
                        {
                            detectedTerms.Add(keyword);
                        }
                    }
                }
            }
            else if (rule.RuleType == "pattern" && !string.IsNullOrEmpty(rule.Pattern))
            {
                try
                {
                    var regexOptions = rule.IsCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    var matches = Regex.Matches(content, rule.Pattern, regexOptions);
                    detectedTerms.AddRange(matches.Cast<Match>().Select(m => m.Value));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in pattern detection for rule {RuleId}", rule.Id);
                }
            }

            return detectedTerms;
        }

        private double CalculateUserTrustScore(List<UserModerationHistory> violations)
        {
            if (!violations.Any())
                return 1.0; // Perfect trust score for users with no violations

            var recentViolations = violations.Where(v => v.ViolationDate > DateTime.UtcNow.AddDays(-30)).ToList();
            var severitySum = recentViolations.Sum(v => v.Severity);
            var violationCount = recentViolations.Count;

            // Base score starts at 1.0 and decreases with violations
            var baseScore = 1.0;
            var severityPenalty = severitySum * 0.2;
            var countPenalty = violationCount * 0.1;

            var trustScore = Math.Max(0.0, baseScore - severityPenalty - countPenalty);

            return trustScore;
        }

        private List<string> GetSuggestedActions(string ruleAction, string category)
        {
            var baseActions = ruleAction.ToLower() switch
            {
                "block" => new List<string> { "Block content", "Notify user", "Log violation" },
                "flag" => new List<string> { "Flag for review", "Notify moderators", "Track violation" },
                "warn" => new List<string> { "Show warning", "Log minor violation", "Continue monitoring" },
                _ => new List<string> { "Log event", "Continue monitoring" }
            };

            var categoryActions = category.ToLower() switch
            {
                "safety" => new List<string> { "Escalate to security team", "Immediate review" },
                "harassment" => new List<string> { "User education", "Temporary restrictions" },
                "spam" => new List<string> { "Rate limiting", "Pattern detection" },
                _ => new List<string>()
            };

            baseActions.AddRange(categoryActions);
            return baseActions.Distinct().ToList();
        }

        private string GetContext(string content, int position, int length)
        {
            if (string.IsNullOrEmpty(content) || position < 0 || position >= content.Length)
                return "";

            if (length > 0)
            {
                var endPos = Math.Min(position + length, content.Length);
                return content.Substring(position, endPos - position);
            }
            else
            {
                var startPos = Math.Max(0, position + length);
                return content.Substring(startPos, position - startPos);
            }
        }

        private List<string> ParseJsonToList(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        //private Dictionary<string, object> ParseJsonToDictionary(string json)
        //{
        //    if (string.IsNullOrEmpty(json))
        //        return new Dictionary<string, object>();

        //    try
        //    {
        //        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? new Dictionary<string, object>();
        //    }
        //    catch
        //    {
        //        return new Dictionary<string, object>();
        //    }
        //}
    }
}