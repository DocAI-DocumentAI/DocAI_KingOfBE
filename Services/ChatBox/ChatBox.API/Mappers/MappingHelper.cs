using AutoMapper;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Payload.Request.UserPreferenceService;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    /// <summary>
    /// Helper class to provide common mapping operations for services
    /// </summary>
    public static class MappingHelper
    {
        /// <summary>
        /// Maps SendMessageRequest to ProcessMessageRequest
        /// </summary>
        public static ProcessMessageRequest MapToProcessMessageRequest(
            this IMapper mapper, 
            SendMessageRequest request, 
            Guid userId, 
            string ipAddress, 
            string userAgent)
        {
            var processRequest = mapper.Map<ProcessMessageRequest>(request);
            processRequest.UserId = userId;
            processRequest.IpAddress = ipAddress;
            processRequest.UserAgent = userAgent;
            return processRequest;
        }

        /// <summary>
        /// Maps ConversationResponse to SendMessageResponse
        /// </summary>
        public static SendMessageResponse MapToSendMessageResponse(
            this IMapper mapper, 
            ConversationResponse orchestrationResult, 
            bool includeSuggestions = true)
        {
            var response = mapper.Map<SendMessageResponse>(orchestrationResult);
            if (!includeSuggestions)
            {
                response.SuggestedQuestions = new List<string>();
            }
            return response;
        }

        /// <summary>
        /// Maps ChatMessage to AdvancedMessageResponse with additional processing
        /// </summary>
        public static AdvancedMessageResponse MapToAdvancedMessageResponse(
            this IMapper mapper,
            ChatMessage message,
            List<DocumentReference>? sources = null,
            Dictionary<string, object>? metadata = null)
        {
            var response = mapper.Map<AdvancedMessageResponse>(message);
            response.Sources = sources ?? new List<DocumentReference>();
            response.Metadata = metadata ?? new Dictionary<string, object>();
            return response;
        }

        /// <summary>
        /// Maps ChatSession to AdvancedSessionResponse with statistics
        /// </summary>
        public static AdvancedSessionResponse MapToAdvancedSessionResponse(
            this IMapper mapper, 
            ChatSession session, 
            SessionStatistics? statistics = null, 
            Dictionary<string, object>? additionalMetadata = null)
        {
            var response = mapper.Map<AdvancedSessionResponse>(session);
            response.Statistics = statistics;
            
            if (additionalMetadata != null)
            {
                response.Metadata = response.Metadata ?? new Dictionary<string, object>();
                foreach (var kvp in additionalMetadata)
                {
                    response.Metadata[kvp.Key] = kvp.Value;
                }
            }
            
            return response;
        }

        /// <summary>
        /// Creates a new ChatSession from CreateSessionRequest
        /// </summary>
        public static ChatSession CreateChatSession(
            this IMapper mapper, 
            CreateSessionRequest request, 
            Guid userId, 
            string? title = null)
        {
            var session = mapper.Map<ChatSession>(request);
            session.Id = Guid.NewGuid();
            session.UserId = userId;
            session.Title = title ?? (string.IsNullOrWhiteSpace(request.Title) ? "New Conversation" : request.Title);
            return session;
        }

        /// <summary>
        /// Creates a new ChatMessage from request
        /// </summary>
        public static ChatMessage CreateChatMessage(
            this IMapper mapper, 
            SendMessageRequest request, 
            Guid userId, 
            Guid sessionId, 
            Guid messageId)
        {
            var message = mapper.Map<ChatMessage>(request);
            message.Id = messageId;
            message.UserId = userId;
            message.SessionId = sessionId;
            return message;
        }

        /// <summary>
        /// Updates UserPreference from UpdatePreferencesRequest
        /// </summary>
        public static void UpdateUserPreference(
            this IMapper mapper, 
            UserPreference preference, 
            UpdatePreferencesRequest request)
        {
            // Map non-null values from request to preference
            mapper.Map(request, preference);
            preference.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates error response for SendMessage
        /// </summary>
        public static SendMessageResponse CreateErrorResponse(
            string message, 
            Guid? messageId = null, 
            Guid? sessionId = null)
        {
            return new SendMessageResponse
            {
                Success = false,
                Message = message,
                MessageId = messageId ?? Guid.Empty,
                SessionId = sessionId ?? Guid.Empty,
                Response = string.Empty,
                Sources = new List<DocumentReference>(),
                SuggestedQuestions = new List<string>(),
                TokensUsed = 0,
                ProcessingTime = TimeSpan.Zero,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Creates success response for operations
        /// </summary>
        public static object CreateSuccessResponse(string message, object? data = null)
        {
            return new
            {
                Success = true,
                Message = message,
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Validates mapping result and throws exception if null
        /// </summary>
        public static T ValidateMappingResult<T>(T? result, string operationName) where T : class
        {
            if (result == null)
            {
                throw new InvalidOperationException($"Mapping failed for operation: {operationName}");
            }
            return result;
        }

        /// <summary>
        /// Maps with fallback value if mapping fails
        /// </summary>
        public static TDestination MapWithFallback<TSource, TDestination>(
            this IMapper mapper, 
            TSource source, 
            TDestination fallback) where TDestination : class
        {
            try
            {
                return mapper.Map<TDestination>(source) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// Maps collection with error handling
        /// </summary>
        public static List<TDestination> MapCollectionSafely<TSource, TDestination>(
            this IMapper mapper, 
            IEnumerable<TSource>? source)
        {
            if (source == null)
                return new List<TDestination>();

            try
            {
                return mapper.Map<List<TDestination>>(source);
            }
            catch
            {
                return new List<TDestination>();
            }
        }

        /// <summary>
        /// Merges two objects using AutoMapper
        /// </summary>
        public static TDestination MergeObjects<TSource, TDestination>(
            this IMapper mapper, 
            TSource source, 
            TDestination destination) where TDestination : class
        {
            return mapper.Map(source, destination);
        }

        /// <summary>
        /// Creates audit log entry with mapped data
        /// </summary>
        public static object CreateAuditLogData(
            this IMapper mapper, 
            object? request = null, 
            object? response = null, 
            Dictionary<string, object>? additionalData = null)
        {
            var auditData = new Dictionary<string, object>();

            if (request != null)
                auditData["Request"] = request;

            if (response != null)
                auditData["Response"] = response;

            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    auditData[kvp.Key] = kvp.Value;
                }
            }

            auditData["Timestamp"] = DateTime.UtcNow;
            return auditData;
        }
    }
}
