using Microsoft.OpenApi;

namespace LlaModem.Services.OpenApiExtensions;

public class SchemaReferenceExtension : IOpenApiExtension
{
    private readonly string _reference;

    public SchemaReferenceExtension(string reference)
    {
        _reference = reference;
    }

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
        writer.WriteValue(_reference);
    }
}
