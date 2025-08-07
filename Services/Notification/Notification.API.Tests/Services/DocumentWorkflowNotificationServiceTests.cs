using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Notification.API.Hubs;
using Notification.API.Payload.Response;
using Notification.API.Services.Implement;
using Notification.API.Services.Interfaces;
using Notification.API.Utils;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Xunit;

namespace Notification.API.Tests.Services
{
    public class DocumentWorkflowNotificationServiceTests
    {
        private readonly Mock<IUnitOfWork<NotificationDbContext>> _mockUnitOfWork;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IEmailTemplateService> _mockEmailTemplateService;
        private readonly Mock<INotificationLogService> _mockLogService;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<ITemplateRendererUtil> _mockTemplateRenderer;
        private readonly Mock<IAuthClient> _mockAuthClient;
        private readonly Mock<ILogger<DocumentWorkflowNotificationService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly IDocumentWorkflowNotificationService _notificationService;

        public DocumentWorkflowNotificationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork<NotificationDbContext>>();
            _mockEmailService = new Mock<IEmailService>();
            _mockEmailTemplateService = new Mock<IEmailTemplateService>();
            _mockLogService = new Mock<INotificationLogService>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockTemplateRenderer = new Mock<ITemplateRendererUtil>();
            _mockAuthClient = new Mock<IAuthClient>();
            _mockLogger = new Mock<ILogger<DocumentWorkflowNotificationService>>();
            _mockConfiguration = new Mock<IConfiguration>();

            _notificationService = new DocumentWorkflowNotificationService(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockEmailTemplateService.Object,
                _mockLogService.Object,
                _mockHubContext.Object,
                _mockTemplateRenderer.Object,
                _mockAuthClient.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);
        }

        [Fact]
        public async Task SendDocumentSubmissionNotificationAsync_ShouldSendEmailToManagers()
        {
            // Arrange
            var documentId = "test-doc-id";
            var documentTitle = "Test Document";
            var documentVersion = "v1.0";
            var departmentId = "dept-123";
            var documentLink = "https://example.com/doc";

            var submitterInfo = new UserInfo
            {
                UserId = "user-123",
                Email = "submitter@example.com",
                FullName = "Test Submitter",
                DepartmentId = departmentId,
                DepartmentName = "Test Department"
            };

            var managerEmails = new List<string> { "manager1@example.com", "manager2@example.com" };
            var template = new EmailTemplateResponse
            {
                TemplateName = "DocumentSubmitted",
                Subject = "Document '{{DocumentTitle}}' Submitted",
                BodyHtml = "<p>Document {{DocumentTitle}} submitted by {{SubmittedBy}}</p>"
            };

            _mockAuthClient.Setup(x => x.GetDepartmentManagerEmailsAsync(departmentId))
                .ReturnsAsync(managerEmails);
            _mockEmailTemplateService.Setup(x => x.GetEmailTemplateByNameAsync("DocumentSubmitted"))
                .ReturnsAsync(template);
            _mockTemplateRenderer.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .Returns<string, Dictionary<string, string>>((template, data) => template);
            _mockEmailService.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockLogService.Setup(x => x.CreateLogAsync(It.IsAny<NotificationLog>()))
                .Returns(Task.CompletedTask);

            // Act
            await _notificationService.SendDocumentSubmissionNotificationAsync(
                documentId, documentTitle, documentVersion, submitterInfo, departmentId, documentLink);

            // Assert
            _mockAuthClient.Verify(x => x.GetDepartmentManagerEmailsAsync(departmentId), Times.Once);
            _mockEmailTemplateService.Verify(x => x.GetEmailTemplateByNameAsync("DocumentSubmitted"), Times.Once);
            _mockEmailService.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Exactly(managerEmails.Count));
            _mockLogService.Verify(x => x.CreateLogAsync(It.IsAny<NotificationLog>()), 
                Times.Exactly(managerEmails.Count));
        }

        [Fact]
        public async Task SendDocumentApprovalNotificationAsync_ShouldSendEmailToOwner()
        {
            // Arrange
            var documentId = "test-doc-id";
            var documentTitle = "Test Document";
            var documentVersion = "v1.0";
            var ownerEmail = "owner@example.com";
            var ownerName = "Document Owner";
            var comments = "Approved with suggestions";
            var documentLink = "https://example.com/doc";

            var approverInfo = new UserInfo
            {
                UserId = "manager-123",
                Email = "manager@example.com",
                FullName = "Test Manager"
            };

            var template = new EmailTemplateResponse
            {
                TemplateName = "DocumentApproved",
                Subject = "Document '{{DocumentTitle}}' Approved",
                BodyHtml = "<p>Document {{DocumentTitle}} approved by {{ApprovedBy}}</p>"
            };

            _mockEmailTemplateService.Setup(x => x.GetEmailTemplateByNameAsync("DocumentApproved"))
                .ReturnsAsync(template);
            _mockTemplateRenderer.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .Returns<string, Dictionary<string, string>>((template, data) => template);
            _mockEmailService.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockLogService.Setup(x => x.CreateLogAsync(It.IsAny<NotificationLog>()))
                .Returns(Task.CompletedTask);

            // Act
            await _notificationService.SendDocumentApprovalNotificationAsync(
                documentId, documentTitle, documentVersion, ownerEmail, ownerName, approverInfo, comments, documentLink);

            // Assert
            _mockEmailTemplateService.Verify(x => x.GetEmailTemplateByNameAsync("DocumentApproved"), Times.Once);
            _mockEmailService.Verify(x => x.SendEmailAsync(ownerEmail, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockLogService.Verify(x => x.CreateLogAsync(It.IsAny<NotificationLog>()), Times.Once);
        }

        [Fact]
        public async Task SendDocumentRejectionNotificationAsync_ShouldSendEmailToOwner()
        {
            // Arrange
            var documentId = "test-doc-id";
            var documentTitle = "Test Document";
            var documentVersion = "v1.0";
            var ownerEmail = "owner@example.com";
            var ownerName = "Document Owner";
            var rejectionComments = "Please revise formatting";
            var documentLink = "https://example.com/doc";

            var reviewerInfo = new UserInfo
            {
                UserId = "manager-123",
                Email = "manager@example.com",
                FullName = "Test Manager"
            };

            var template = new EmailTemplateResponse
            {
                TemplateName = "DocumentRejected",
                Subject = "Document '{{DocumentTitle}}' Requires Revision",
                BodyHtml = "<p>Document {{DocumentTitle}} rejected by {{ReviewedBy}}</p>"
            };

            _mockEmailTemplateService.Setup(x => x.GetEmailTemplateByNameAsync("DocumentRejected"))
                .ReturnsAsync(template);
            _mockTemplateRenderer.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .Returns<string, Dictionary<string, string>>((template, data) => template);
            _mockEmailService.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockLogService.Setup(x => x.CreateLogAsync(It.IsAny<NotificationLog>()))
                .Returns(Task.CompletedTask);

            // Act
            await _notificationService.SendDocumentRejectionNotificationAsync(
                documentId, documentTitle, documentVersion, ownerEmail, ownerName, reviewerInfo, rejectionComments, documentLink);

            // Assert
            _mockEmailTemplateService.Verify(x => x.GetEmailTemplateByNameAsync("DocumentRejected"), Times.Once);
            _mockEmailService.Verify(x => x.SendEmailAsync(ownerEmail, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockLogService.Verify(x => x.CreateLogAsync(It.IsAny<NotificationLog>()), Times.Once);
        }

        [Fact]
        public async Task SendDocumentSubmissionNotificationAsync_ShouldHandleNoManagersGracefully()
        {
            // Arrange
            var documentId = "test-doc-id";
            var documentTitle = "Test Document";
            var documentVersion = "v1.0";
            var departmentId = "dept-123";

            var submitterInfo = new UserInfo
            {
                UserId = "user-123",
                Email = "submitter@example.com",
                FullName = "Test Submitter",
                DepartmentId = departmentId,
                DepartmentName = "Test Department"
            };

            var emptyManagerList = new List<string>();

            _mockAuthClient.Setup(x => x.GetDepartmentManagerEmailsAsync(departmentId))
                .ReturnsAsync(emptyManagerList);

            // Act
            await _notificationService.SendDocumentSubmissionNotificationAsync(
                documentId, documentTitle, documentVersion, submitterInfo, departmentId);

            // Assert
            _mockAuthClient.Verify(x => x.GetDepartmentManagerEmailsAsync(departmentId), Times.Once);
            _mockEmailService.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Never);
        }
    }
}
