namespace LlaModem.Services;

public class LaunchParamParser : ILaunchParamParser
{
    private const string TemperatureHeader = "X-Llama-Temperature";
    private const string TopPHeader = "X-Llama-TopP";
    private const string PresencePenaltyHeader = "X-Llama-PresencePenalty";

    public async Task<ModelLaunchParams?> ParseAsync(HttpContext context, HttpRequest request)
    {
        double? temperature = null;
        double? topP = null;
        double? presencePenalty = null;
        bool hasLaunchParams = false;

        var tempResult = await TryParseDoubleHeader(context, request, TemperatureHeader);
        if (tempResult.Parsed.HasValue) { temperature = tempResult.Parsed.Value; hasLaunchParams = true; }
        else if (tempResult.Error) return null;

        var topPResult = await TryParseDoubleHeader(context, request, TopPHeader);
        if (topPResult.Parsed.HasValue) { topP = topPResult.Parsed.Value; hasLaunchParams = true; }
        else if (topPResult.Error) return null;

        var ppResult = await TryParseDoubleHeader(context, request, PresencePenaltyHeader);
        if (ppResult.Parsed.HasValue) { presencePenalty = ppResult.Parsed.Value; hasLaunchParams = true; }
        else if (ppResult.Error) return null;

        return hasLaunchParams ? new ModelLaunchParams(temperature, topP, presencePenalty) : null;
    }

    private static async Task<(double? Parsed, bool Error)> TryParseDoubleHeader(
        HttpContext context, HttpRequest request, string headerName)
    {
        var headerValue = request.Headers[headerName].FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue))
            return (null, false);

        if (!double.TryParse(headerValue, out var parsed))
        {
            await WriteErrorAsync(context, 400, "Bad request",
                $"Invalid {headerName} value: '{headerValue}'");
            return (null, true);
        }

        return (parsed, false);
    }

    private static async Task WriteErrorAsync(
        HttpContext context, int statusCode, string error, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error, message });
    }
}
