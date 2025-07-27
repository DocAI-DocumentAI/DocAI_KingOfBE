using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using ChatBox.API.Payload.Response.SecurityServiceResponse;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ChatBox.API.Services.Implement
{
    public class SecurityService : ISecurityService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IAuditService _auditService;
        private readonly ILogger<SecurityService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        public SecurityService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IAuditService auditService,
            ILogger<SecurityService> logger,
            IConfiguration configuration,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _auditService = auditService;
            _logger = logger;
            _configuration = configuration;
            _mapper = mapper;
        }


        public async Task<SecurityAnalysisResult> AnalyzeContentAsync(string content, Guid userId, string ipAddress)
        {
            var analysisId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Starting security analysis for user {UserId}, AnalysisId: {AnalysisId}",
                    userId, analysisId);

                var result = new SecurityAnalysisResult
                {
                    AnalysisId = analysisId,
                    AnalysisTimestamp = DateTime.UtcNow,
                    DetectedThreats = new List<SecurityThreat>(),
                    DetectedIssues = new List<string>()
                };

                var enableThreatDetection = _configuration.GetValue<bool>("Security:EnableThreatDetection", true);
                if (enableThreatDetection)
                {
                    await DetectPatternBasedThreatsAsync(content, result);
                }

                await AnalyzeUserBehaviorAsync(userId, content, result);
                await DetectContentAnomaliesAsync(content, result);

                var enableIPReputation = _configuration.GetValue<bool>("Security:EnableIPReputation", true);
                if (enableIPReputation)
                {
                    await CheckIpReputationAsync(ipAddress, result);
                }

                CalculateRiskScore(result);
                GenerateSecurityRecommendation(result);
                await UpdateUserSecurityProfileAsync(userId, result);

                if (result.HasSecurityIssues)
                {
                    await LogSecurityEventAsync(userId, content, result, ipAddress);
                }

                await _auditService.LogAsync(userId, "SecurityAnalysis", "Content", analysisId,
                    null, new { ContentLength = content.Length, RiskScore = result.RiskScore, ThreatsDetected = result.DetectedThreats.Count }, ipAddress);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in security analysis for user {UserId}, AnalysisId: {AnalysisId}",
                    userId, analysisId);

                await _auditService.LogSecurityEventAsync(userId, "SecurityAnalysisError",
                    $"Security analysis failed: {ex.Message}", "high", ipAddress);

                return new SecurityAnalysisResult
                {
                    HasSecurityIssues = true,
                    RiskScore = _configuration.GetValue<double>("Security:FailureRiskScore", 0.8),
                    DetectedIssues = new List<string> { _configuration["Security:Messages:AnalysisFailure"] ?? "Security analysis failed - treating as high risk" },
                    AnalysisId = analysisId,
                    AnalysisTimestamp = DateTime.UtcNow,
                    Recommendation = new SecurityRecommendation
                    {
                        Action = "block",
                        Reason = _configuration["Security:Messages:AnalysisFailureReason"] ?? "Security analysis failed",
                        Confidence = 0.9,
                        RequiresHumanReview = true
                    }
                };
            }
        }

        public async Task<PIIDetectionResult> DetectPIIAsync(string content)
        {
            try
            {
                _logger.LogDebug("Detecting PII in content of length: {ContentLength}", content?.Length ?? 0);

                var result = new PIIDetectionResult
                {
                    DetectedPII = new List<PIIEntity>(),
                    PIITypes = new List<string>(),
                    ContainsPII = false,
                    ConfidenceScore = 1.0,
                    MaskedContent = content
                };

                if (string.IsNullOrEmpty(content))
                {
                    result.ContainsPII = false;
                    result.ConfidenceScore = 1.0;
                    result.MaskedContent = content;
                    return result;
                }

                var maskedContent = content;
                var detectedEntities = new List<PIIEntity>();

                // Detect different types of PII
                foreach (var pattern in PIIPatterns)
                {
                    var matches = pattern.Value.Matches(content);
                    foreach (Match match in matches)
                    {
                        var entity = new PIIEntity
                        {
                            Type = pattern.Key,
                            Value = match.Value,
                            MaskedValue = GenerateMask(match.Value, pattern.Key),
                            StartPosition = match.Index,
                            EndPosition = match.Index + match.Length,
                            Confidence = CalculatePIIConfidence(match.Value, pattern.Key),
                            Context = ExtractContext(content, match.Index, match.Length)
                        };

                        detectedEntities.Add(entity);

                        if (!result.PIITypes.Contains(pattern.Key))
                        {
                            result.PIITypes.Add(pattern.Key);
                        }

                        // Replace in masked content
                        maskedContent = maskedContent.Replace(match.Value, entity.MaskedValue);
                    }
                }

                result.DetectedPII = detectedEntities;
                result.ContainsPII = detectedEntities.Any();
                result.MaskedContent = maskedContent;
                result.ConfidenceScore = CalculateOverallPIIConfidence(detectedEntities);
                result.RiskAssessment = AssessPIIRisk(detectedEntities);

                _logger.LogInformation("PII detection completed. Found {PIICount} entities of {TypeCount} types",
                    detectedEntities.Count, result.PIITypes.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting PII in content");

                return new PIIDetectionResult
                {
                    ContainsPII = true, // Assume contains PII on error for safety
                    ConfidenceScore = 0.5,
                    MaskedContent = "[CONTENT ANALYSIS FAILED]",
                    RiskAssessment = new PIIRiskAssessment
                    {
                        RiskLevel = "high",
                        RiskFactors = new List<string> { "PII detection failed" },
                        RequiresDataProtection = true
                    }
                };
            }
        }

        public async Task<List<SecurityEvent>> GetSecurityEventsAsync(Guid userId, DateTime? fromDate = null)
        {
            try
            {
                var securityEventRepo = _unitOfWork.GetRepository<SecurityIncident>();

                var events = await securityEventRepo.GetListAsync(
                    predicate: e => e.UserId == userId &&
                                   (!fromDate.HasValue || e.DetectedAt >= fromDate.Value),
                    orderBy: e => e.OrderByDescending(x => x.DetectedAt));

                return events.Select(e => new SecurityEvent
                {
                    Id = e.Id,
                    UserId = e.UserId,
                    EventType = e.IncidentType,
                    Description = e.Description,
                    Severity = e.Severity,
                    Timestamp = e.DetectedAt,
                    Source = e.DetectionMethod,
                    EventData = e.Metadata,
                    Status = e.Status,
                    Resolution = e.Resolution
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security events for user {UserId}", userId);
                return new List<SecurityEvent>();
            }
        }

        // Private helper methods
        private async Task DetectPatternBasedThreatsAsync(string content, SecurityAnalysisResult result)
        {
            foreach (var pattern in SecurityPatterns)
            {
                var matches = pattern.Value.Matches(content);
                if (matches.Count > 0)
                {
                    var threat = new SecurityThreat
                    {
                        ThreatType = pattern.Key,
                        Description = GetThreatDescription(pattern.Key),
                        Severity = GetThreatSeverity(pattern.Key),
                        Evidence = string.Join(", ", matches.Cast<Match>().Select(m => m.Value)),
                        Mitigation = GetThreatMitigation(pattern.Key)
                    };

                    result.DetectedThreats.Add(threat);
                    result.DetectedIssues.Add($"{pattern.Key}: {matches.Count} occurrences");
                }
            }
        }

        private async Task AnalyzeUserBehaviorAsync(Guid userId, string content, SecurityAnalysisResult result)
        {
            try
            {
                var securityProfileRepo = _unitOfWork.GetRepository<UserSecurityProfile>();
                var profile = await securityProfileRepo.SingleOrDefaultAsync(predicate: p => p.UserId == userId);

                if (profile != null)
                {
                    // Check if user is already flagged
                    if (profile.IsBlocked)
                    {
                        result.DetectedThreats.Add(new SecurityThreat
                        {
                            ThreatType = "blocked_user",
                            Description = "User is currently blocked due to security violations",
                            Severity = 1.0,
                            Evidence = profile.BlockReason,
                            Mitigation = new List<string> { "User must be unblocked by administrator" }
                        });
                    }

                    // Check risk score
                    if (profile.RiskScore > 0.7)
                    {
                        result.DetectedThreats.Add(new SecurityThreat
                        {
                            ThreatType = "high_risk_user",
                            Description = "User has high security risk score",
                            Severity = profile.RiskScore,
                            Evidence = $"Risk score: {profile.RiskScore}",
                            Mitigation = new List<string> { "Enhanced monitoring", "Additional verification" }
                        });
                    }

                    // Check recent violations
                    if (profile.SecurityViolationCount > 5 &&
                        profile.LastViolation.HasValue &&
                        profile.LastViolation.Value > DateTime.UtcNow.AddDays(-7))
                    {
                        result.DetectedThreats.Add(new SecurityThreat
                        {
                            ThreatType = "repeat_offender",
                            Description = "User has multiple recent security violations",
                            Severity = 0.8,
                            Evidence = $"{profile.SecurityViolationCount} violations in recent period",
                            Mitigation = new List<string> { "Temporary restriction", "Security training" }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing user behavior for user {UserId}", userId);
            }
        }

        private async Task DetectContentAnomaliesAsync(string content, SecurityAnalysisResult result)
        {
            // Check content length anomalies
            if (content.Length > 10000)
            {
                result.DetectedThreats.Add(new SecurityThreat
                {
                    ThreatType = "content_length_anomaly",
                    Description = "Content exceeds normal length limits",
                    Severity = 0.4,
                    Evidence = $"Content length: {content.Length} characters",
                    Mitigation = new List<string> { "Content length validation", "Chunking large content" }
                });
            }

            // Check for repeated patterns (potential spam/flood)
            var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 10)
            {
                var wordFreq = words.GroupBy(w => w.ToLower())
                                  .ToDictionary(g => g.Key, g => g.Count());

                var maxFreq = wordFreq.Values.Max();
                var totalWords = words.Length;

                if ((double)maxFreq / totalWords > 0.5)
                {
                    result.DetectedThreats.Add(new SecurityThreat
                    {
                        ThreatType = "content_repetition",
                        Description = "Content contains excessive repetition",
                        Severity = 0.6,
                        Evidence = $"Most frequent word appears {maxFreq} times out of {totalWords}",
                        Mitigation = new List<string> { "Content quality check", "Anti-spam measures" }
                    });
                }
            }

            // Check for suspicious encoding or characters
            if (content.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
            {
                result.DetectedThreats.Add(new SecurityThreat
                {
                    ThreatType = "suspicious_encoding",
                    Description = "Content contains suspicious control characters",
                    Severity = 0.7,
                    Evidence = "Control characters detected",
                    Mitigation = new List<string> { "Character encoding validation", "Content sanitization" }
                });
            }
        }

        private async Task CheckIpReputationAsync(string ipAddress, SecurityAnalysisResult result)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return;

            try
            {
                // Check against known bad IP ranges or patterns
                if (IsKnownBadIp(ipAddress))
                {
                    result.DetectedThreats.Add(new SecurityThreat
                    {
                        ThreatType = "malicious_ip",
                        Description = "Request from known malicious IP address",
                        Severity = 0.9,
                        Evidence = $"IP: {ipAddress}",
                        Mitigation = new List<string> { "IP blocking", "Enhanced monitoring" }
                    });
                }

                // Check for Tor exit nodes (basic pattern)
                if (IsLikelyTorNode(ipAddress))
                {
                    result.DetectedThreats.Add(new SecurityThreat
                    {
                        ThreatType = "tor_network",
                        Description = "Request potentially from Tor network",
                        Severity = 0.5,
                        Evidence = $"IP: {ipAddress}",
                        Mitigation = new List<string> { "Additional verification", "Enhanced logging" }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking IP reputation for {IpAddress}", ipAddress);
            }
        }

        private void CalculateRiskScore(SecurityAnalysisResult result)
        {
            if (result.DetectedThreats.Count == 0)
            {
                result.RiskScore = 0.0;
                result.HasSecurityIssues = false;
                return;
            }

            // Calculate weighted risk score
            var totalSeverity = result.DetectedThreats.Sum(t => t.Severity);
            var threatCount = result.DetectedThreats.Count;

            // Base score from average severity
            var baseScore = totalSeverity / threatCount;

            // Multiply by threat count factor (more threats = higher risk)
            var threatFactor = Math.Min(1.0 + (threatCount - 1) * 0.2, 2.0);

            result.RiskScore = Math.Min(baseScore * threatFactor, 1.0);
            result.HasSecurityIssues = result.RiskScore > 0.3;
        }

        private void GenerateSecurityRecommendation(SecurityAnalysisResult result)
        {
            var recommendation = new SecurityRecommendation
            {
                Confidence = Math.Min(result.RiskScore + 0.1, 1.0)
            };

            if (result.RiskScore >= 0.8)
            {
                recommendation.Action = "block";
                recommendation.Reason = "High security risk detected";
                recommendation.RequiresHumanReview = true;
                recommendation.SuggestedActions = new List<string>
                {
                    "Block user temporarily",
                    "Security team investigation",
                    "Enhanced monitoring"
                };
            }
            else if (result.RiskScore >= 0.5)
            {
                recommendation.Action = "flag";
                recommendation.Reason = "Moderate security risk detected";
                recommendation.RequiresHumanReview = false;
                recommendation.SuggestedActions = new List<string>
                {
                    "Enhanced monitoring",
                    "Content moderation",
                    "User notification"
                };
            }
            else if (result.RiskScore >= 0.3)
            {
                recommendation.Action = "moderate";
                recommendation.Reason = "Low security risk detected";
                recommendation.RequiresHumanReview = false;
                recommendation.SuggestedActions = new List<string>
                {
                    "Content review",
                    "Automated filtering"
                };
            }
            else
            {
                recommendation.Action = "allow";
                recommendation.Reason = "No significant security risks detected";
                recommendation.RequiresHumanReview = false;
                recommendation.SuggestedActions = new List<string> { "Continue monitoring" };
            }

            result.Recommendation = recommendation;
        }

        private async Task UpdateUserSecurityProfileAsync(Guid userId, SecurityAnalysisResult result)
        {
            try
            {
                var securityProfileRepo = _unitOfWork.GetRepository<UserSecurityProfile>();
                var profile = await securityProfileRepo.SingleOrDefaultAsync(predicate: p => p.UserId == userId);

                if (profile == null)
                {
                    profile = new UserSecurityProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await securityProfileRepo.InsertAsync(profile);
                }

                // Update risk score (exponential moving average)
                profile.RiskScore = profile.RiskScore * 0.7 + result.RiskScore * 0.3;
                profile.LastSecurityCheck = DateTime.UtcNow;
                profile.UpdatedAt = DateTime.UtcNow;

                // Update violation count if needed
                if (result.HasSecurityIssues && result.RiskScore > 0.5)
                {
                    profile.SecurityViolationCount++;
                    profile.LastViolation = DateTime.UtcNow;
                }

                // Update risk level
                profile.RiskLevel = profile.RiskScore switch
                {
                    >= 0.8 => "critical",
                    >= 0.6 => "high",
                    >= 0.4 => "medium",
                    _ => "low"
                };

                // Auto-block for critical risk
                if (profile.RiskScore >= 0.9 && profile.SecurityViolationCount >= 3)
                {
                    profile.IsBlocked = true;
                    profile.BlockedUntil = DateTime.UtcNow.AddHours(24);
                    profile.BlockReason = "Automatic block due to repeated security violations";
                }

                securityProfileRepo.UpdateAsync(profile);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating security profile for user {UserId}", userId);
            }
        }

        private async Task LogSecurityEventAsync(Guid userId, string content, SecurityAnalysisResult result, string ipAddress)
        {
            try
            {
                var securityIncidentRepo = _unitOfWork.GetRepository<SecurityIncident>();

                var incident = new SecurityIncident
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    IncidentType = result.DetectedThreats.FirstOrDefault()?.ThreatType ?? "unknown",
                    Title = $"Security Analysis Alert - Risk Score: {result.RiskScore:F2}",
                    Description = $"Security analysis detected {result.DetectedThreats.Count} threats with risk score {result.RiskScore:F2}",
                    Severity = result.RiskScore >= 0.8 ? "high" : result.RiskScore >= 0.5 ? "medium" : "low",
                    Status = "new",
                    DetectedAt = DateTime.UtcNow,
                    DetectionMethod = "automated_analysis",
                    Evidence = JsonSerializer.Serialize(new
                    {
                        AnalysisId = result.AnalysisId,
                        RiskScore = result.RiskScore,
                        Threats = result.DetectedThreats.Select(t => new { t.ThreatType, t.Severity, t.Description }),
                        ContentLength = content.Length,
                        IpAddress = ipAddress
                    }, _jsonOptions),
                    Metadata = result.Details
                };

                await securityIncidentRepo.InsertAsync(incident);
                await _unitOfWork.CommitAsync();

                // Log to audit service as well
                await _auditService.LogSecurityEventAsync(userId, incident.IncidentType,
                    incident.Description, incident.Severity, ipAddress, result.Details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event for user {UserId}", userId);
            }
        }
        private string GetThreatDescription(string threatType)
        {
            return threatType switch
            {
                "sql_injection" => "Potential SQL injection attempt detected",
                "xss_attempt" => "Potential cross-site scripting (XSS) attempt detected",
                "path_traversal" => "Potential path traversal attack detected",
                "command_injection" => "Potential command injection attempt detected",
                "suspicious_keywords" => "Suspicious keywords detected in content",
                _ => "Unknown security threat detected"
            };
        }

        private double GetThreatSeverity(string threatType)
        {
            return threatType switch
            {
                "sql_injection" => 0.9,
                "xss_attempt" => 0.8,
                "path_traversal" => 0.8,
                "command_injection" => 0.9,
                "suspicious_keywords" => 0.4,
                _ => 0.5
            };
        }

        private List<string> GetThreatMitigation(string threatType)
        {
            return threatType switch
            {
                "sql_injection" => new List<string> { "Input sanitization", "Parameterized queries", "Database access control" },
                "xss_attempt" => new List<string> { "Output encoding", "Content Security Policy", "Input validation" },
                "path_traversal" => new List<string> { "Path validation", "Access control", "Sandboxing" },
                "command_injection" => new List<string> { "Input validation", "Command sanitization", "Process isolation" },
                "suspicious_keywords" => new List<string> { "Content review", "Keyword filtering", "Context analysis" },
                _ => new List<string> { "Enhanced monitoring", "Manual review" }
            };
        }

        private string GenerateMask(string value, string piiType)
        {
            return piiType switch
            {
                "email" => MaskEmail(value),
                "phone" => MaskPhone(value),
                "ssn" => "***-**-****",
                "credit_card" => "****-****-****-" + value.Substring(Math.Max(0, value.Length - 4)),
                "ip_address" => MaskIpAddress(value),
                "url" => MaskUrl(value),
                "api_key" => value.Substring(0, Math.Min(4, value.Length)) + "***",
                _ => new string('*', value.Length)
            };
        }

        private string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return "***@***.***";

            var username = parts[0];
            var domain = parts[1];

            var maskedUsername = username.Length > 2 ?
                username.Substring(0, 2) + new string('*', username.Length - 2) :
                new string('*', username.Length);

            return $"{maskedUsername}@{domain}";
        }

        private string MaskPhone(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length >= 10)
            {
                return $"***-***-{digits.Substring(digits.Length - 4)}";
            }
            return "***-***-****";
        }

        private string MaskIpAddress(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.***.**";
            }
            return "***.***.***.**";
        }

        private string MaskUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://***/***";
            }
            catch
            {
                return "***://***";
            }
        }

        private double CalculatePIIConfidence(string value, string piiType)
        {
            return piiType switch
            {
                "email" => value.Contains('@') && value.Contains('.') ? 0.9 : 0.5,
                "phone" => value.Length >= 10 ? 0.8 : 0.4,
                "ssn" => value.Replace("-", "").Length == 9 ? 0.9 : 0.5,
                "credit_card" => value.Replace("-", "").Replace(" ", "").Length >= 13 ? 0.7 : 0.3,
                "ip_address" => 0.9,
                "url" => value.StartsWith("http") ? 0.9 : 0.6,
                "api_key" => value.Length >= 20 ? 0.6 : 0.3,
                _ => 0.5
            };
        }

        private string ExtractContext(string content, int position, int length)
        {
            var contextStart = Math.Max(0, position - 20);
            var contextEnd = Math.Min(content.Length, position + length + 20);
            var contextLength = contextEnd - contextStart;

            var context = content.Substring(contextStart, contextLength);

            if (contextStart > 0) context = "..." + context;
            if (contextEnd < content.Length) context = context + "...";

            return context;
        }

        private double CalculateOverallPIIConfidence(List<PIIEntity> entities)
        {
            if (!entities.Any()) return 0.0;

            return entities.Average(e => e.Confidence);
        }

        private PIIRiskAssessment AssessPIIRisk(List<PIIEntity> entities)
        {
            var assessment = new PIIRiskAssessment
            {
                RiskFactors = new List<string>(),
                ComplianceIssues = new List<string>(),
                RecommendedActions = new List<string>()
            };

            if (!entities.Any())
            {
                assessment.RiskLevel = "low";
                return assessment;
            }

            var highRiskTypes = new[] { "ssn", "credit_card", "api_key" };
            var mediumRiskTypes = new[] { "email", "phone" };

            var hasHighRisk = entities.Any(e => highRiskTypes.Contains(e.Type));
            var hasMediumRisk = entities.Any(e => mediumRiskTypes.Contains(e.Type));
            var entityCount = entities.Count;

            if (hasHighRisk || entityCount > 5)
            {
                assessment.RiskLevel = "critical";
                assessment.RiskFactors.AddRange(new[] { "Contains sensitive financial/identity data", "High PII exposure" });
                assessment.ComplianceIssues.AddRange(new[] { "GDPR", "PCI DSS", "CCPA" });
                assessment.RequiresDataProtection = true;
                assessment.RecommendedActions.AddRange(new[] {
                    "Immediate data masking",
                    "Encrypt sensitive data",
                    "Audit data access",
                    "Notify data protection officer"
                });
            }
            else if (hasMediumRisk || entityCount > 2)
            {
                assessment.RiskLevel = "medium";
                assessment.RiskFactors.AddRange(new[] { "Contains personal identifiable information" });
                assessment.ComplianceIssues.AddRange(new[] { "GDPR", "CCPA" });
                assessment.RequiresDataProtection = true;
                assessment.RecommendedActions.AddRange(new[] {
                    "Apply data masking",
                    "Review data retention policies",
                    "Monitor data usage"
                });
            }
            else
            {
                assessment.RiskLevel = "low";
                assessment.RiskFactors.AddRange(new[] { "Contains minimal personal data" });
                assessment.RequiresDataProtection = false;
                assessment.RecommendedActions.AddRange(new[] {
                    "Standard data handling",
                    "Regular compliance review"
                });
            }

            return assessment;
        }

        private bool IsKnownBadIp(string ipAddress)
        {
            // Basic implementation - in production, this would check against threat intelligence feeds
            var knownBadRanges = new[]
            {
                "0.0.0.0",
                "127.0.0.1",
                "169.254.", // Link-local addresses
                "224.", // Multicast
                "255.255.255.255" // Broadcast
            };

            return knownBadRanges.Any(range => ipAddress.StartsWith(range));
        }

        private bool IsLikelyTorNode(string ipAddress)
        {
            // Basic heuristic - in production, use actual Tor exit node lists
            // This is a simplified check for demonstration
            try
            {
                var ip = System.Net.IPAddress.Parse(ipAddress);
                var bytes = ip.GetAddressBytes();

                // Some IP ranges commonly associated with VPN/Proxy services
                // This is a very basic check and not comprehensive
                return false; // Placeholder - implement proper Tor detection
            }
            catch
            {
                return false;
            }
        }
    }
}