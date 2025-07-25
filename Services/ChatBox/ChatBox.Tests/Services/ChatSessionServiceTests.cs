using AutoMapper;
using ChatBox.API.Mappers;
using ChatBox.API.Payload.Request;
using ChatBox.API.Services.Implement;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Enum;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChatBox.Tests.Services
{
    public class ChatSessionServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IAIServiceClient> _mockAIClient;
        private readonly Mock<IDocumentServiceClient> _mockDocumentClient;
        private readonly Mock<ILogger<ChatSessionService>> _mockLogger;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ChatSessionService _service;

        public ChatSessionServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockAIClient = new Mock<IAIServiceClient>();
            _mockDocumentClient = new Mock<IDocumentServiceClient>();
            _mockLogger = new Mock<ILogger<ChatSessionService>>();

            // Setup AutoMapper
            var config = new MapperConfiguration(cfg => cfg.AddProfile<ChatMappingProfile>());
            _mapper = config.CreateMapper();

            // Setup MemoryCache
            _cache = new MemoryCache(new MemoryCacheOptions());

            // Setup Configuration
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ChatService:SystemPrompt"] = "Test system prompt",
                ["ChatService:EmptyAnswerText"] = "Test empty answer",
                ["ChatService:ContextWindowSize"] = "10",
                ["ChatService:DocSearchLimit"] = "5",
                ["ChatService:DocMinRelevance"] = "0.7"
            });
            _configuration = configBuilder.Build();

            _service = new ChatSessionService(
                _mockUnitOfWork.Object,
                _mockAIClient.Object,
                _mockDocumentClient.Object,
                _mapper,
                _cache,
                _mockLogger.Object,
                _configuration
            );
        }

        [Fact]
        public async Task SendMessageAsync_WithNewSession_CreatesSessionAndMessages()
        {
            // Arrange
            var userId = "test-user";
            var userRoles = new List<string> { "User" };
            var request = new SendMessageRequest
            {
                Message = "Test message",
                SessionId = null
            };

            var mockSessionRepo = new Mock<IGenericRepository<ChatSession>>();
            var mockMessageRepo = new Mock<IGenericRepository<ChatMessage>>();

            _mockUnitOfWork.Setup(x => x.GetRepository<ChatSession>()).Returns(mockSessionRepo.Object);
            _mockUnitOfWork.Setup(x => x.GetRepository<ChatMessage>()).Returns(mockMessageRepo.Object);

            // Setup document search response
            _mockDocumentClient.Setup(x => x.SearchRelevantDocumentsAsync(It.IsAny<SearchDocumentRequestExternal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SearchDocumentResponseExternal
                {
                    NoResult = true,
                    RelevantSources = new List<RelevantSourceResponseExternal>()
                });

            // Act
            var result = await _service.SendMessageAsync(userId, userRoles, request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsNewSession);
            Assert.NotNull(result.SessionId);
            Assert.NotNull(result.UserMessage);
            Assert.NotNull(result.AIResponse);

            mockSessionRepo.Verify(x => x.InsertAsync(It.IsAny<ChatSession>()), Times.Once);
            mockMessageRepo.Verify(x => x.InsertAsync(It.IsAny<ChatMessage>()), Times.Exactly(2)); // User + AI message
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_WithExistingSession_UpdatesSession()
        {
            // Arrange
            var userId = "test-user";
            var userRoles = new List<string> { "User" };
            var sessionId = "existing-session";
            var request = new SendMessageRequest
            {
                Message = "Test message",
                SessionId = sessionId
            };

            var existingSession = new ChatSession
            {
                Id = sessionId,
                UserId = userId,
                Title = "Test Session",
                IsDeleted = false
            };

            var mockSessionRepo = new Mock<IGenericRepository<ChatSession>>();
            var mockMessageRepo = new Mock<IGenericRepository<ChatMessage>>();

            mockSessionRepo.Setup(x => x.GetByIdAsync(sessionId)).ReturnsAsync(existingSession);

            _mockUnitOfWork.Setup(x => x.GetRepository<ChatSession>()).Returns(mockSessionRepo.Object);
            _mockUnitOfWork.Setup(x => x.GetRepository<ChatMessage>()).Returns(mockMessageRepo.Object);

            // Setup document search response
            _mockDocumentClient.Setup(x => x.SearchRelevantDocumentsAsync(It.IsAny<SearchDocumentRequestExternal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SearchDocumentResponseExternal
                {
                    NoResult = true,
                    RelevantSources = new List<RelevantSourceResponseExternal>()
                });

            // Act
            var result = await _service.SendMessageAsync(userId, userRoles, request);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsNewSession);
            Assert.Equal(sessionId, result.SessionId);

            mockSessionRepo.Verify(x => x.Update(It.IsAny<ChatSession>()), Times.Once);
            mockMessageRepo.Verify(x => x.InsertAsync(It.IsAny<ChatMessage>()), Times.Exactly(2));
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_WithInvalidSession_ThrowsArgumentException()
        {
            // Arrange
            var userId = "test-user";
            var userRoles = new List<string> { "User" };
            var sessionId = "invalid-session";
            var request = new SendMessageRequest
            {
                Message = "Test message",
                SessionId = sessionId
            };

            var mockSessionRepo = new Mock<IGenericRepository<ChatSession>>();
            mockSessionRepo.Setup(x => x.GetByIdAsync(sessionId)).ReturnsAsync((ChatSession?)null);

            _mockUnitOfWork.Setup(x => x.GetRepository<ChatSession>()).Returns(mockSessionRepo.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.SendMessageAsync(userId, userRoles, request));
        }

        [Fact]
        public async Task DeleteSessionAsync_WithValidSession_SoftDeletesSession()
        {
            // Arrange
            var userId = "test-user";
            var sessionId = "test-session";

            var existingSession = new ChatSession
            {
                Id = sessionId,
                UserId = userId,
                IsDeleted = false
            };

            var messages = new List<ChatMessage>
            {
                new ChatMessage { Id = "msg1", SessionId = sessionId, IsDeleted = false },
                new ChatMessage { Id = "msg2", SessionId = sessionId, IsDeleted = false }
            };

            var mockSessionRepo = new Mock<IGenericRepository<ChatSession>>();
            var mockMessageRepo = new Mock<IGenericRepository<ChatMessage>>();

            mockSessionRepo.Setup(x => x.GetByIdAsync(sessionId)).ReturnsAsync(existingSession);
            mockMessageRepo.Setup(x => x.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ChatMessage, bool>>>(),
                null, null, null, false, default))
                .ReturnsAsync(messages);

            _mockUnitOfWork.Setup(x => x.GetRepository<ChatSession>()).Returns(mockSessionRepo.Object);
            _mockUnitOfWork.Setup(x => x.GetRepository<ChatMessage>()).Returns(mockMessageRepo.Object);

            // Act
            var result = await _service.DeleteSessionAsync(userId, sessionId);

            // Assert
            Assert.True(result);
            Assert.True(existingSession.IsDeleted);
            Assert.All(messages, msg => Assert.True(msg.IsDeleted));

            mockSessionRepo.Verify(x => x.Update(existingSession), Times.Once);
            mockMessageRepo.Verify(x => x.Update(It.IsAny<ChatMessage>()), Times.Exactly(2));
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }
    }
}
