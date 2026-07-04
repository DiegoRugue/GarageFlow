using GarageFlow.SharedKernel.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;

namespace GarageFlow.Adapters.Api.Middlewares;

public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseGarageFlowExceptionHandler(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                var (statusCode, title) = exception switch
                {
                    UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                    NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                    ValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
                    BusinessRuleViolationException => (StatusCodes.Status409Conflict, "Business rule violation"),
                    _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
                };
                var detail = statusCode == StatusCodes.Status500InternalServerError
                    ? null
                    : exception?.Message;

                var result = Results.Problem(
                    title: title,
                    detail: detail,
                    statusCode: statusCode);

                await result.ExecuteAsync(context);
            });
        });

        return app;
    }
}
