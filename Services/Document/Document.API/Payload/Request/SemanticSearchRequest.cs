namespace Document.API.Payload.Request;

using Microsoft.AspNetCore.Mvc;

public class SemanticSearchRequest
{
    [FromQuery]
    public string Query { get; set; }
}