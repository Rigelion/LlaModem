using LlaModem.Config;

namespace LlaModem.Services;

public class LaunchParamParser
{

    public async Task<ModelLaunchParams?> ParseAsync(HttpContext context, HttpRequest request)
    {
        double? temperature = null;
        double? topP = null;
        double? topK = null;
        double? minP = null;
        double? presencePenalty = null;
        double? repetitionPenalty = null;
        bool hasLaunchParams = false;

        var tempResult = await TryParseDoubleHeader(context, request, ProxyHeaders.Temperature);
        if (tempResult.Parsed.HasValue) { temperature = tempResult.Parsed.Value; hasLaunchParams = true; }
        else if (tempResult.Error) return null;

        var topPResult = await TryParseDoubleHeader(context, request, ProxyHeaders.TopP);
        if (topPResult.Parsed.HasValue) { topP = topPResult.Parsed.Value; hasLaunchParams = true; }
        else if (topPResult.Error) return null;

        var topKResult = await TryParseDoubleHeader(context, request, ProxyHeaders.TopK);
        if (topKResult.Parsed.HasValue) { topK = topKResult.Parsed.Value; hasLaunchParams = true; }
        else if (topKResult.Error) return null;

        var minPResult = await TryParseDoubleHeader(context, request, ProxyHeaders.MinP);
        if (minPResult.Parsed.HasValue) { minP = minPResult.Parsed.Value; hasLaunchParams = true; }
        else if (minPResult.Error) return null;

        var ppResult = await TryParseDoubleHeader(context, request, ProxyHeaders.PresencePenalty);
        if (ppResult.Parsed.HasValue) { presencePenalty = ppResult.Parsed.Value; hasLaunchParams = true; }
        else if (ppResult.Error) return null;

        var rpResult = await TryParseDoubleHeader(context, request, ProxyHeaders.RepetitionPenalty);
        if (rpResult.Parsed.HasValue) { repetitionPenalty = rpResult.Parsed.Value; hasLaunchParams = true; }
        else if (rpResult.Error) return null;

        return hasLaunchParams
            ? new ModelLaunchParams(temperature, topP, topK, minP, presencePenalty, repetitionPenalty)
            : null;
    }

    private static async Task<(double? Parsed, bool Error)> TryParseDoubleHeader(
        HttpContext context, HttpRequest request, string headerName)
    {
        var headerValue = request.Headers[headerName].FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue))
            return (null, false);

        if (!double.TryParse(headerValue, out var parsed))
        {
            await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.BadRequest("InvalidParameterValue",
                $"Invalid {headerName} value: '{headerValue}'"));
            return (null, true);
        }

        return (parsed, false);
    }


}
