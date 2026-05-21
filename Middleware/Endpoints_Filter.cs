namespace Gemini.Middleware
{
    using Gemini.Models;
    using Microsoft.AspNetCore.Antiforgery;
    using Microsoft.AspNetCore.Http;
    using System;
    using System.Linq;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="next"></param>
    /// <param name="antiforgery"></param>
    public class GlobalAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        private readonly RequestDelegate _next = next;
        private readonly IAntiforgery _antiforgery = antiforgery;
        private static readonly PathString[] ExcludedPaths =
        [
            // "/ws" entfernt, damit WebSocket-Upgrades ebenfalls geprüft werden
            new("/antiforgery/token"),
            new("/login"),
            new("/logout")
        ];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // Spezialfall: WebSocket-Upgrade (GET + Upgrade oder HTTP/2 CONNECT)
            //if (context.Request.Path.StartsWithSegments(new PathString("/ws"), StringComparison.OrdinalIgnoreCase)
            //    && (string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase)
            //        || string.Equals(context.Request.Method, "CONNECT", StringComparison.OrdinalIgnoreCase))
            //    && (context.Request.Headers.ContainsKey("Upgrade") || context.Request.Headers.ContainsKey("Sec-WebSocket-Key")))
            if (context.Request.Path.StartsWithSegments(new PathString("/ws"), StringComparison.OrdinalIgnoreCase))
            {
                var csrfToken = context.Request.Query["csrf"].FirstOrDefault();

                if (string.IsNullOrEmpty(csrfToken)
                    && context.Request.Headers.TryGetValue("Sec-WebSocket-Protocol", out var protocols))
                {
                    var items = protocols.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items)
                    {
                        if (item.StartsWith("csrf-", StringComparison.OrdinalIgnoreCase))
                        {
                            var safe = item.Substring("csrf-".Length);
                            csrfToken = Base64UrlDecodeToString(safe);
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(csrfToken))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(
                        new AlertMessage("error", "CSRF Token fehlt für WebSocket-Upgrade."),
                        AppJsonSerializerContext.Default.AlertMessage);
                    return;
                }

                context.Request.Headers["X-CSRF-TOKEN"] = csrfToken;
                context.Request.Headers["RequestVerificationToken"] = csrfToken;

                try
                {
                    await _antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(
                        new AlertMessage("error", "CSRF Token validierung fehlgeschlagen."),
                        AppJsonSerializerContext.Default.AlertMessage);
                    return;
                }

                // Markiere, dass die AntiForgery-Validierung für dieses /ws-Upgrade bereits erfolgt ist.
                context.Items["AntiforgeryValidatedForWebSocket"] = true;

                // Erfolgreiche Validierung -> Pipeline fortsetzen; WebSocketMiddleware übernimmt das Upgrade.
                await _next(context);
                return;
            }

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

                        await context.Response.WriteAsJsonAsync(
                            new AlertMessage("error", "CSRF Token validierung fehlgeschlagen."),
                            AppJsonSerializerContext.Default.AlertMessage);
                        return;
                    }
                }
            }

            await _next(context);
        }

        // Helper: base64url -> string
        private static string? Base64UrlDecodeToString(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            string s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 0: break;
                default: s += new string('=', 4 - (s.Length % 4)); break;
            }
            try
            {
                var bytes = Convert.FromBase64String(s);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPathExcluded(PathString path)
        {
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