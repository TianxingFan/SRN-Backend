using System.Net;
using System.Text.Json;

namespace SRN.API.Middleware
{
    /// <summary>
    /// Global error handling middleware for the HTTP pipeline.
    /// Catches all unhandled exceptions thrown during request processing to prevent app crashes 
    /// and ensures API consumers receive a consistent, formatted JSON error response.
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Intercepts incoming HTTP requests. Wraps the execution of the next middleware delegate in a try-catch block.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Pass the request to the next middleware in the pipeline
                await _next(context);
            }
            catch (Exception ex)
            {
                // Log the critical error internally for debugging
                _logger.LogError(ex, ex.Message);

                // Formulate and return a graceful error payload to the client
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Formats the exception details into a standard JSON response.
        /// Implements security best practices by hiding raw stack traces in production environments.
        /// </summary>
        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // Conditionally expose the stack trace only if running in the Development environment
            object response = _env.IsDevelopment()
                ? new { StatusCode = context.Response.StatusCode, Message = ex.Message, StackTrace = ex.StackTrace?.ToString() }
                : new { StatusCode = context.Response.StatusCode, Message = "Internal Server Error", StackTrace = (string?)"Hidden" };

            // Ensure JSON serialization follows standard camelCase formatting
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
}