namespace Gemini.Middleware
{
    // This class is intended to hold endpoint methods related to serving static files, such as HTML pages, images, or other resources from the wwwroot directory.
    // The actual implementation of these endpoints would involve reading the requested file from the wwwroot directory and returning it with the appropriate content type.
    // For example, you might have methods like:

    public static partial class Endpoints
    {

        private static IResult ServeStaticFile(string filePath)
        {
            filePath = "wwwroot" + filePath;
#if DEBUG
            Console.WriteLine($"\r\nServing static file: {filePath}");
#endif
            if (File.Exists(filePath))
            {
                var contentType = GetContentType(filePath);
                var fileContent = File.ReadAllBytes(filePath);
                return Results.File(fileContent, contentType);
            }
            else
            {
                return Results.NotFound();
            }
        }

        private static string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".ico" => "image/x-icon",
                ".pem" => "application/x-pem-file",
                _ => "application/octet-stream",
            };
        }

        /*
        private static IResult Favicon()
        {
            return ServeStaticFile("wwwroot/favicon.ico");            
        }

        private static IResult JavaScriptFile(string filename)
        {
            //ctx.Response.StatusCode = 200;
            //ctx.Response.ContentType = "text/javascript";
            //var file = File.ReadAllText($"wwwroot/js/{filename}", Encoding.UTF8);
            //await ctx.Response.WriteAsync(file);
            //await ctx.Response.CompleteAsync();

            var file = File.ReadAllText($"wwwroot/js/{filename}");
            return Results.Content(file, "text/javascript");
        }

        private static IResult StylesheetFile(string filename)
        {
            //ctx.Response.StatusCode = 200;
            //ctx.Response.ContentType = "text/css";
            //var file = File.ReadAllText($"wwwroot/css/{filename}", Encoding.UTF8);
            //await ctx.Response.WriteAsync(file);
            //await ctx.Response.CompleteAsync();

            var file = File.ReadAllText($"wwwroot/css/{filename}");
            return Results.Content(file, "text/css");
        }

        //*/
      

    }
}
