using Document.API.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Exceptions;
using System.Linq;

namespace Document.API.Filters
{
    /// <summary>
    /// Converts model validation errors (including FluentValidation) into a unified ErrorException
    /// so our ExceptionHandlingMiddleware can return consistent error responses.
    /// </summary>
    public class ValidationActionFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid)
                return;

            var errors = context.ModelState
                .Where(kvp => kvp.Value != null && kvp.Value.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(e => new { Field = kvp.Key, Error = e.ErrorMessage }))
                .ToList();

            var messages = string.Join(" | ", errors.Select(e => e.Error).Distinct());

            // Throw to be handled by ExceptionHandlingMiddleware
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, messages);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // no-op
        }
    }
}

