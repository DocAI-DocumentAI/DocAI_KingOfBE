# Document Workflow Notifications

This document describes the notification system for document workflow events in the DocAI system.

## Overview

The document workflow notification system automatically sends notifications when key document workflow events occur:

1. **Document Submission**: When a document is submitted for approval, department managers are notified
2. **Document Approval**: When a document is approved, the document owner is notified
3. **Document Rejection**: When a document is rejected, the document owner is notified with feedback

## Architecture

The notification system uses a microservices architecture with MassTransit for inter-service communication:

```
Document Service → MassTransit → Notification Service → Email/System Notifications
```

### Components

1. **Document Service**
   - `IDocumentNotificationService`: Publishes notification commands via MassTransit
   - `ApprovalService`: Triggers notifications during workflow state changes

2. **Notification Service**
   - `IDocumentWorkflowNotificationService`: Handles workflow-specific notifications
   - Consumers: Process notification commands from Document service
   - Email templates: Predefined templates for each notification type

## Notification Types

### 1. Document Submission Notification

**Trigger**: When `ApprovalService.SubmitForApprovalAsync()` is called
**Recipients**: Department managers in the same department as the document
**Template**: `DocumentSubmitted`

**Template Variables**:
- `{{DocumentTitle}}`: Title of the submitted document
- `{{DocumentVersion}}`: Version name of the document
- `{{SubmittedBy}}`: Full name of the user who submitted the document
- `{{DepartmentName}}`: Name of the department
- `{{SubmissionDate}}`: Date and time of submission
- `{{DocumentLink}}`: Link to view the document

### 2. Document Approval Notification

**Trigger**: When `ApprovalService.ReviewDocument()` is called with `IsApproved = true`
**Recipients**: Document owner
**Template**: `DocumentApproved`

**Template Variables**:
- `{{DocumentTitle}}`: Title of the approved document
- `{{DocumentVersion}}`: Version name of the document
- `{{DocumentOwner}}`: Full name of the document owner
- `{{ApprovedBy}}`: Full name of the manager who approved the document
- `{{ApprovalDate}}`: Date and time of approval
- `{{Comments}}`: Approval comments (if any)
- `{{DocumentLink}}`: Link to view the document

### 3. Document Rejection Notification

**Trigger**: When `ApprovalService.ReviewDocument()` is called with `IsApproved = false`
**Recipients**: Document owner
**Template**: `DocumentRejected`

**Template Variables**:
- `{{DocumentTitle}}`: Title of the rejected document
- `{{DocumentVersion}}`: Version name of the document
- `{{DocumentOwner}}`: Full name of the document owner
- `{{ReviewedBy}}`: Full name of the manager who rejected the document
- `{{ReviewDate}}`: Date and time of rejection
- `{{Comments}}`: Rejection comments/feedback
- `{{DocumentLink}}`: Link to edit the document

## Security and Access Control

### Department-Based Access Control

- **Submission notifications**: Only sent to managers within the same department as the submitted document
- **Approval/Rejection notifications**: Only sent to the document owner
- **JWT token validation**: User information is extracted from JWT tokens for proper authorization

### Data Privacy

- Notifications respect the document security model (public vs department-specific documents)
- Email addresses are obtained securely from the Auth service
- No sensitive document content is included in email notifications

## Configuration

### Email Templates

Email templates are stored in the database and can be managed through the Notification service API:

```sql
-- Example template for document submission
INSERT INTO EmailTemplates (TemplateName, Subject, BodyHtml, AssociatedEvent)
VALUES (
    'DocumentSubmitted',
    '[DocAI Workflow] Document ''{{DocumentTitle}}'' Submitted for Approval',
    '<p>Dear Manager,</p><p>A new document has been submitted for your review...</p>',
    'DocumentSubmitted'
);
```

### MassTransit Queues

The following RabbitMQ queues are used for notification commands:

- `document-submission-notifications-queue`: Document submission notifications
- `document-approval-notifications-queue`: Document approval notifications
- `document-rejection-notifications-queue`: Document rejection notifications

## API Integration

### Document Service Integration

The Document service publishes notification commands automatically during workflow operations:

```csharp
// In ApprovalService.SubmitForApprovalAsync()
await _notificationService.SendDocumentSubmissionNotificationAsync(
    versionId,
    version.Title,
    version.VersionName,
    currentUser,
    version.DocumentFile.DepartmentId);
```

### Notification Service Processing

The Notification service processes these commands asynchronously:

```csharp
// DocumentSubmissionNotificationConsumer
public async Task Consume(ConsumeContext<DocumentSubmissionNotificationCommand> context)
{
    var command = context.Message;
    await _notificationService.SendDocumentSubmissionNotificationAsync(
        command.DocumentId,
        command.DocumentTitle,
        command.DocumentVersion,
        submitterInfo,
        command.DepartmentId,
        command.DocumentLink);
}
```

## Delivery Channels

### Email Notifications

- Sent via SMTP using the configured email service
- HTML templates with professional formatting
- Includes dismiss links for notification management

### System Notifications

- Real-time notifications via SignalR
- Displayed in the DocAI web interface
- Can be dismissed by users

## Error Handling

- Notification failures do not break the main document workflow
- Errors are logged for monitoring and debugging
- Retry mechanisms are implemented for transient failures
- Graceful degradation when notification services are unavailable

## Monitoring and Logging

All notification activities are logged with the following information:

- Document ID and version
- Recipient information
- Notification type and status
- Timestamps and error details
- Performance metrics

## Testing

### Unit Tests

- `DocumentNotificationServiceTests`: Tests for Document service notification publishing
- `DocumentWorkflowNotificationServiceTests`: Tests for Notification service processing

### Integration Tests

- End-to-end workflow testing with real MassTransit messaging
- Email delivery verification
- Department-based access control validation

## Future Enhancements

1. **Notification Preferences**: Allow users to configure notification preferences
2. **Digest Notifications**: Option for daily/weekly digest emails
3. **Mobile Push Notifications**: Integration with mobile apps
4. **Escalation Rules**: Automatic escalation for overdue approvals
5. **Analytics Dashboard**: Notification delivery and engagement metrics
