using Document.API.Constants;

namespace Document.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ErrorException ex)
        {
            _logger.LogError(ex, ex.Message);
            await HandleExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            await HandleGeneralExceptionAsync(context, ex);
        }
    }
    private static Task HandleExceptionAsync(HttpContext context, ErrorException ex
        )
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = ex.StatusCode;
        var result = System.Text.Json.JsonSerializer.Serialize(ex.ErrorDetail);
        return context.Response.WriteAsync(result);
    }
    private static Task HandleGeneralExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        //var result = System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message });
        var errorDetail = new ErrorDetail
        {
            ErrorCode = "INTERNAL_SERVER_ERROR",
            Message = "An unexpected error occurred on the server."
        };
        var result = System.Text.Json.JsonSerializer.Serialize(errorDetail);
        return context.Response.WriteAsync(result);
    }
}