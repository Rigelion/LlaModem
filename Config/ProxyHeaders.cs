namespace LlaModem.Config;

/// <summary>
/// HTTP header names used by the model proxy for routing and parameter passing.
/// </summary>
public static class ProxyHeaders
{
    public const string Model = "X-Llama-Model";
    public const string Temperature = "X-Llama-Temperature";
    public const string TopP = "X-Llama-TopP";
    public const string TopK = "X-Llama-TopK";
    public const string MinP = "X-Llama-MinP";
    public const string PresencePenalty = "X-Llama-PresencePenalty";
    public const string RepetitionPenalty = "X-Llama-RepetitionPenalty";
}
