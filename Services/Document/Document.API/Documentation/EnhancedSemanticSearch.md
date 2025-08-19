# Enhanced Semantic Search with AI-Powered Conversational Responses

## Overview

The Enhanced Semantic Search feature provides AI-powered conversational responses to user queries by leveraging Kernel Memory's `AskAsync` functionality instead of raw search results. This enhancement transforms the traditional document search into an intelligent Q&A system that provides contextual answers with relevant document sources.

## Key Features

- **Conversational AI Responses**: Instead of returning raw search results, the system provides natural language answers to user questions
- **Relevant Document Sources**: Each response includes the specific documents that contributed to the answer
- **Department-based Access Control**: Maintains existing security rules for document access
- **Hybrid Scoring Support**: Optionally applies advanced scoring algorithms for better relevance
- **Comprehensive Metadata**: Includes search configuration and processing information

## API Endpoint

```
GET /api/v1/documents/enhanced-semantic-search
```

## Request Parameters

All parameters from the standard semantic search are supported:

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `query` | string | The search query/question (required) | - |
| `minRelevance` | double | Minimum relevance threshold (0.0-1.0) | 0.3 |
| `maxResults` | int | Maximum number of document sources to return | 20 |
| `enableHybridScoring` | bool | Enable advanced scoring algorithms | true |
| `scope` | enum | Search scope (All, PublicOnly, DepartmentOnly) | All |
| `documentTypeId` | string | Filter by document type | - |
| `departmentId` | string | Filter by department | - |
| `fromDate` | datetime | Filter documents created from this date | - |
| `toDate` | datetime | Filter documents created until this date | - |
| `effectiveFrom` | datetime | Filter by effective date range | - |
| `effectiveUntil` | datetime | Filter by effective date range | - |
| `folderId` | string | Filter by folder | - |
| `includeSubfolders` | bool | Include documents from subfolders | false |

## Response Structure

```json
{
  "success": true,
  "message": "AI generated response with 5 relevant document sources",
  "data": {
    "requestId": "12345678-1234-1234-1234-123456789012",
    "query": "What are the company's vacation policies?",
    "answer": "Based on the company documents, the vacation policy includes...",
    "hasAnswer": true,
    "relevantDocuments": [
      {
        "id": "doc-123",
        "title": "Employee Handbook 2024",
        "documentName": "handbook.pdf",
        "relevance": 0.95,
        "departmentId": "hr-dept",
        "isPublic": true,
        // ... other document fields
      }
    ],
    "totalDocuments": 5,
    "processingTimeMs": 1250,
    "metadata": {
      "minRelevance": 0.3,
      "maxResults": 20,
      "hybridScoringEnabled": true,
      "scope": "All",
      "departmentFilter": null,
      "documentTypeFilter": null,
      "dateRange": null
    },
    "success": true
  }
}
```

## Usage Examples

### Basic Question
```
GET /api/v1/documents/enhanced-semantic-search?query=What is the company dress code?
```

### Department-Specific Query
```
GET /api/v1/documents/enhanced-semantic-search?query=HR policies for remote work&departmentId=hr-dept&scope=DepartmentOnly
```

### Date-Filtered Query
```
GET /api/v1/documents/enhanced-semantic-search?query=Recent policy changes&fromDate=2024-01-01&maxResults=10
```

## Implementation Details

### AI Prompt Configuration

The system uses configurable AI prompts defined in `AiPromptConstant.SemanticSearch`:

- **ConversationalSearchPrompt**: Main prompt for generating conversational responses
- **NoResultsPrompt**: Fallback message when no relevant documents are found

### Security and Access Control

- Maintains all existing department-based access control rules
- Users can only access documents they have permission to view
- Public documents are accessible to all employees
- Private documents are restricted to the same department

### Performance Considerations

- 2-minute timeout for AI response generation
- Efficient document filtering using database predicates
- Optional hybrid scoring for improved relevance
- Comprehensive logging for monitoring and debugging

### Error Handling

The system provides graceful error handling:

- **Timeout errors**: Returns appropriate error message if AI processing takes too long
- **No results**: Returns helpful suggestions for refining the search
- **Invalid queries**: Validates input parameters and provides clear error messages
- **Access denied**: Respects security rules and returns appropriate responses

## Differences from Standard Semantic Search

| Feature | Standard Search | Enhanced Search |
|---------|----------------|-----------------|
| Response Type | List of documents | Conversational answer + documents |
| AI Processing | None | Uses Kernel Memory AskAsync |
| User Experience | Manual document review | Direct answers to questions |
| Response Time | Faster | Slightly slower due to AI processing |
| Use Case | Document discovery | Question answering |

## Best Practices

1. **Query Formulation**: Frame queries as specific questions for better AI responses
2. **Scope Management**: Use appropriate scope settings to limit search domain
3. **Relevance Tuning**: Adjust minRelevance based on your quality requirements
4. **Error Handling**: Implement proper error handling for timeout scenarios
5. **Caching**: Consider caching responses for frequently asked questions

## Monitoring and Logging

The system provides comprehensive logging:

- Request/response tracking with unique request IDs
- Processing time monitoring
- AI response quality indicators
- Security access logging
- Error tracking and debugging information

## Future Enhancements

- Configurable AI prompts through admin interface
- Response caching for improved performance
- Multi-language support
- Advanced conversation context management
- Integration with external knowledge bases
