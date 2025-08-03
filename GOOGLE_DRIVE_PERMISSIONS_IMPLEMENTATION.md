# Google Drive Permissions Implementation

## Overview
This document outlines the implementation of Google Drive permissions management for the DocAI system, ensuring proper access control based on document status and department membership.

## Architecture

### Core Components

1. **DocumentPermissionManager** (`IDocumentPermissionManager`)
   - Central service for managing document permissions
   - Handles permission logic based on document status and department
   - Integrates with Auth service for user/role information

2. **GoogleDriveService** (Enhanced)
   - Added permission management methods
   - Supports granting/revoking user access
   - Provides file permission listing capabilities

3. **Service Integration**
   - DocumentService: Applies permissions on document creation/update
   - ApprovalService: Updates permissions during status transitions
   - Auth Service: Provides user and department information

## Permission Rules

### Document Status-Based Access Control

| Status | Access Rules |
|--------|-------------|
| **Draft** | Owner only |
| **Pending** | Owner + Department Managers |
| **Approved** | Based on IsPublic flag:<br/>• Public: All company employees<br/>• Private: Department employees only |
| **Rejected** | Owner only |
| **Archived** | Based on IsPublic flag (same as Approved) |

### Folder Structure
- `approved/public/` - Public approved documents (all employees)
- `approved/{departmentId}/` - Private approved documents (department only)
- `pending/{departmentId}/` - Pending documents (owner + managers)
- `drafts/` - Draft documents (owner only)
- `archived/public/` - Public archived documents
- `archived/{departmentId}/` - Private archived documents

## Implementation Details

### 1. DocumentPermissionManager

**Key Methods:**
- `ApplyDocumentPermissionsAsync()` - Sets initial permissions for new documents
- `UpdateDocumentPermissionsAsync()` - Updates permissions during status changes
- `GetDepartmentEmployeeEmailsAsync()` - Retrieves department member emails (with Redis caching)
- `GetDepartmentManagerEmailsAsync()` - Retrieves department manager emails (with Redis caching)
- `GetAllCompanyEmployeeEmailsAsync()` - Retrieves all company employee emails (with Redis caching)
- `GetUserEmailAsync()` - Retrieves user email by ID (with Redis caching)

**Optimized Architecture:**
- **Redis Caching**: All Auth service responses are cached for improved performance
- **RabbitMQ Messaging**: Asynchronous communication with Auth service via message queues
- **Graceful Degradation**: Fallback mechanisms when Auth service is unavailable
- **Timeout Handling**: 30-second timeouts for Auth service requests

**Permission Logic:**
```csharp
// Draft: Owner only
await _googleDriveService.GrantUserAccessAsync(fileId, ownerEmail, "writer");

// Pending: Owner + Department Managers
var managers = await GetDepartmentManagerEmailsAsync(departmentId); // Cached
foreach (var manager in managers)
{
    await _googleDriveService.GrantUserAccessAsync(fileId, manager, "reader");
}

// Approved Public: All employees
var allEmployees = await GetAllCompanyEmployeeEmailsAsync(); // Cached
foreach (var employee in allEmployees)
{
    await _googleDriveService.GrantUserAccessAsync(fileId, employee, "reader");
}

// Approved Private: Department only
var deptEmployees = await GetDepartmentEmployeeEmailsAsync(departmentId); // Cached
foreach (var employee in deptEmployees)
{
    await _googleDriveService.GrantUserAccessAsync(fileId, employee, "reader");
}
```

### 2. Service Integration Points

**DocumentService:**
- `CreateDraftAsync()` - Applies Draft permissions after document creation
- `UpdateDraftAsync()` - Reapplies Draft permissions after updates

**ApprovalService:**
- `SubmitForApprovalAsync()` - Updates permissions from Draft to Pending
- `ReviewDocument()` - Updates permissions based on approval/rejection
  - Approved: Draft/Pending → Approved
  - Rejected: Pending → Rejected
  - Archives previous approved versions

### 3. Optimized Auth Service Integration

**Redis Caching Strategy:**
- **Department Employees**: Cached for 30 minutes (`dept_employees:{departmentId}`)
- **Department Managers**: Cached for 30 minutes (`dept_managers:{departmentId}`)
- **All Company Employees**: Cached for 60 minutes (`company_employees:all`)
- **User Emails**: Cached for 24 hours (`user_email:{userId}`)

**RabbitMQ Message Queues:**
- `department-employee-queue` - Department employee requests
- `company-employee-queue` - Company-wide employee requests
- `user-email-queue` - User email lookup requests

**Message DTOs:**
```csharp
// Request DTOs
public class DepartmentEmployeeRequest
{
    public string DepartmentId { get; set; }
    public bool ManagersOnly { get; set; } // true for managers, false for all employees
    public string RequestId { get; set; }
}

public class CompanyEmployeeRequest
{
    public string RequestId { get; set; }
}

public class UserEmailRequest
{
    public string UserId { get; set; }
    public string RequestId { get; set; }
}

// Response DTOs
public class DepartmentEmployeeResponse
{
    public List<string> EmployeeEmails { get; set; }
    public string RequestId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

**Auth Service Consumers:**
- `DepartmentEmployeeConsumer` - Handles department employee/manager requests
- `CompanyEmployeeConsumer` - Handles company-wide employee requests
- `UserEmailConsumer` - Handles user email lookup requests

### 4. Enhanced Error Handling & Performance

**Error Handling:**
- Permission operations are wrapped in try-catch blocks
- Failures don't break main document operations
- Comprehensive logging for troubleshooting
- Graceful degradation when Auth service is unavailable
- **RequestTimeoutException** handling for RabbitMQ timeouts
- **Fallback mechanisms** when cache misses and Auth service fails

**Performance Optimizations:**
- **Cache-First Strategy**: Always check Redis cache before making Auth service requests
- **Batch Operations**: Single RabbitMQ request can handle both employees and managers
- **Configurable Timeouts**: 30-second timeout for Auth service requests
- **Async Processing**: Non-blocking permission operations
- **Connection Pooling**: Efficient Redis and RabbitMQ connection management

## Security Considerations

1. **Company Account Control**: All CRUD operations go through s3rcc.9@gmail.com
2. **Read-Only Personal Access**: Personal Gmail accounts have read-only access
3. **Department Isolation**: Private documents restricted to department members
4. **Manager Oversight**: Department managers can review pending documents
5. **Audit Trail**: All permission changes are logged

## Configuration

### Required Settings

**appsettings.json:**
```json
{
  "GoogleDrive": {
    "CompanyEmail": "s3rcc.9@gmail.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "CompanyRootFolderId": "your-root-folder-id"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  }
}
```

### Service Registration

**Document Service DependencyService.cs:**
```csharp
services.AddScoped<IDocumentPermissionManager, DocumentPermissionManager>();

// RabbitMQ Request Clients
services.AddMassTransit(x =>
{
    x.AddRequestClient<DepartmentEmployeeRequest>(new Uri("queue:department-employee-queue"));
    x.AddRequestClient<CompanyEmployeeRequest>(new Uri("queue:company-employee-queue"));
    x.AddRequestClient<UserEmailRequest>(new Uri("queue:user-email-queue"));
});
```

**Auth Service DependencyService.cs:**
```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<DepartmentEmployeeConsumer>();
    x.AddConsumer<CompanyEmployeeConsumer>();
    x.AddConsumer<UserEmailConsumer>();

    // Configure endpoints
    cfg.ReceiveEndpoint("department-employee-queue", e =>
    {
        e.ConfigureConsumer<DepartmentEmployeeConsumer>(context);
        e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        e.UseInMemoryOutbox();
    });
    // ... other endpoints
});
```

## Testing Recommendations

1. **Unit Tests:**
   - Permission rule logic
   - Status transition scenarios
   - Error handling paths

2. **Integration Tests:**
   - End-to-end document lifecycle
   - Auth service integration
   - Google Drive API calls

3. **Manual Testing:**
   - Create documents with different IsPublic settings
   - Test approval/rejection workflows
   - Verify access from different user accounts
   - Test department-based restrictions

## Performance Metrics & Monitoring

**Cache Hit Rates:**
- Monitor Redis cache hit rates for each data type
- Target: >90% cache hit rate for user/department data
- Alert when cache hit rate drops below 80%

**RabbitMQ Performance:**
- Monitor message processing times
- Target: <100ms average response time for Auth service requests
- Alert on message queue backlog >100 messages

**Permission Operation Metrics:**
- Track permission update success/failure rates
- Monitor Google Drive API rate limits
- Log permission operation latency

## Future Enhancements

1. ✅ **Caching**: Implemented Redis caching with configurable TTL
2. ✅ **Async Messaging**: Implemented RabbitMQ for Auth service communication
3. **Batch Operations**: Optimize permission updates for large user lists
4. **Real-time Sync**: Implement webhooks for immediate permission updates
5. **Advanced Roles**: Support for custom roles beyond owner/manager/employee
6. **Permission Audit**: Track and report permission changes over time
7. **Cache Warming**: Pre-populate cache with frequently accessed data
8. **Circuit Breaker**: Implement circuit breaker pattern for Auth service calls

## Troubleshooting

**Common Issues:**
1. **RabbitMQ Connection Issues**:
   - Check RabbitMQ service status and connection string
   - Verify queue configurations match between services
   - Monitor connection pool exhaustion

2. **Redis Cache Issues**:
   - Check Redis service availability and connection string
   - Monitor memory usage and eviction policies
   - Verify cache key naming conventions

3. **Auth Service Timeouts**:
   - Check Auth service performance and load
   - Adjust timeout settings if needed (default: 30 seconds)
   - Monitor RabbitMQ message processing times

4. **Permission Denied**:
   - Verify company account has proper Google Drive permissions
   - Check Google Drive API quotas and rate limits
   - Validate file IDs and user email addresses

5. **Cache Inconsistency**:
   - Implement cache invalidation strategies
   - Monitor cache TTL settings
   - Consider cache warming for critical data

**Enhanced Logging:**
- All permission operations logged with document IDs, user emails, and cache status
- RabbitMQ request/response correlation IDs for tracing
- Redis cache hit/miss metrics
- Performance metrics for all Auth service interactions
- Detailed error context including retry attempts and fallback usage

**Monitoring Dashboards:**
- Cache hit rates by data type
- RabbitMQ message throughput and latency
- Permission operation success rates
- Google Drive API usage and quotas
