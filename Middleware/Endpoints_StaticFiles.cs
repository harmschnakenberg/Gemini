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
                ".svg" => "image/svg+xml",
                //".png" => "image/png",
                //".jpg" => "image/jpeg",
                //".jpeg" => "image/jpeg",
                //".gif" => "image/gif",
                ".ico" => "image/x-icon",
                //".pem" => "application/x-pem-file",
                _ => "application/octet-stream",
            };
        }
    }
}
