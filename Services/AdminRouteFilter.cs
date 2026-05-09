using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LlaModem.Services;

/// <summary>
/// Filters OpenAPI document to expose only /admin routes.
/// Excludes /v1/* routes entirely.
/// </summary>
public class AdminRouteFilter : IOpenApiDocumentTransformer
{
    public OpenApiDocument Transform(OpenApiDocument document, OpenApiDocumentTransformerContext context)
    {
        var adminPaths = new OpenApiPaths();
        
        foreach (var path in document.Paths)
        {
            // Only include /admin/* routes
            if (path.Key.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
            {
                adminPaths.Add(path.Key, path.Value);
            }
        }
        
        document.Paths = adminPaths;
        return document;
    }

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var adminPaths = new OpenApiPaths();
        
        foreach (var path in document.Paths)
        {
            // Only include /admin/* routes
            if (path.Key.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
            {
                adminPaths.Add(path.Key, path.Value);
            }
        }
        
        document.Paths = adminPaths;
        await Task.CompletedTask;
    }
}
