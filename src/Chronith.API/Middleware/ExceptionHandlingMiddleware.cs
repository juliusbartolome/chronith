using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Chronith.API.Middleware;

public static class ExceptionHandlingMiddleware
{
    public static void Configure(IApplicationBuilder app)
    {
        app.Run(async ctx =>
        {
            var feature = ctx.Features.Get<IExceptionHandlerFeature>();
            var ex = feature?.Error;
            if (ex is null) return;

            var problem = ex switch
            {
                FluentValidation.ValidationException ve => new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed",
                    Extensions = { ["errors"] = ve.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()) }
                },
                Chronith.Domain.Exceptions.NotFoundException => new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not found",
                    Detail = ex.Message
                },
                Chronith.Domain.Exceptions.SlotConflictException => new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = ex.Message
                },
                Chronith.Domain.Exceptions.DomainException => new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Domain error",
                    Detail = ex.Message
                },
                _ => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred"
                }
            };

            ctx.Response.StatusCode = problem.Status ?? 500;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(problem);
        });
    }
}
