using Microsoft.AspNetCore.Antiforgery;

namespace Gemini.Middleware
{
    using Gemini.Models;
    using Microsoft.AspNetCore.Antiforgery;

    /// <summary>
    /// Middleware zur globalen Validierung von Anti-Forgery-Tokens für unsichere HTTP-Methoden.
    /// </summary>
    /// <param name="next">Der nächste RequestDelegate in der Pipeline.</param>
    /// <param name="antiforgery">Der IAntiforgery-Dienst zur Validierung und Verwaltung von Anti-Forgery-Tokens.</param>
    public class GlobalAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        private readonly RequestDelegate _next = next;
        private readonly IAntiforgery _antiforgery = antiforgery;      
        private static readonly PathString[] ExcludedPaths =
        [
            new("/ws"),
            new("/antiforgery/token"),
            new("/login"),
            new("/logout")            
        ];

        /// <summary>
        /// Verarbeitet die eingehende HTTP-Anfrage und validiert für unsichere HTTP-Methoden (z. B. POST, PUT, DELETE)
        /// das Anti-Forgery-Token. Bei fehlgeschlagener Validierung wird eine 400-Antwort mit einer AlertMessage
        /// im JSON-Format zurückgegeben.
        /// </summary>
        /// <param name="context">Der aktuelle HttpContext der eingehenden Anfrage.</param>
        /// <returns>Ein Task, der die asynchrone Verarbeitung der Anfrage repräsentiert.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (IsUnsafeMethod(context.Request.Method))
            {
                if (!IsPathExcluded(context.Request.Path))
                {
                    try
                    {
                        await _antiforgery.ValidateRequestAsync(context);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;

                        // Korrekte Nutzung des Source Generators für AOT
                        await context.Response.WriteAsJsonAsync(
                            new AlertMessage("error", "CSRF Token validierung fehlgeschlagen."),
                            AppJsonSerializerContext.Default.AlertMessage);
                        return;
                    }
                }
            }

            await _next(context);
        }

        // Extrahiert für bessere Testbarkeit und Inlining durch den JIT/AOT-Compiler
        private static bool IsPathExcluded(PathString path)
        {
            // Nutze eine einfache Schleife oder LINQ - Any ist in modernen .NET Versionen AOT-sicher
            foreach (var excludedPath in ExcludedPaths)
            {
                if (path.StartsWithSegments(excludedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsUnsafeMethod(string method) =>
        !HttpMethods.IsGet(method) &&
        !HttpMethods.IsHead(method) &&
        !HttpMethods.IsOptions(method) &&
        !HttpMethods.IsTrace(method);
    }
}
