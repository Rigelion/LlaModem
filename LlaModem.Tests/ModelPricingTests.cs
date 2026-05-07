using LlaModem.Services;

namespace LlaModem.Tests;

public class ModelPricingTests
{
    [Fact]
    public void AllModels_ReturnsSixModels()
    {
        Assert.Equal(6, ModelPricing.All.Length);
    }

    [Fact]
    public void AllModels_HaveCorrectPricing()
    {
        var expected = new[]
        {
            ("Claude Opus 4.6", 5.00, 25.00),
            ("Claude Sonnet 4.5", 3.00, 15.00),
            ("GPT-5.1 Codex Max", 1.25, 10.00),
            ("Gemini 3 Pro Image", 2.00, 12.00),
            ("Gemini 3 Flash", 0.50, 3.00),
            ("Qwen 3 Max", 1.20, 6.00),
        };

        for (var i = 0; i < expected.Length; i++)
        {
            var (name, input, output) = expected[i];
            Assert.Equal(name, ModelPricing.All[i].Name);
            Assert.Equal(input, ModelPricing.All[i].InputPerMillion);
            Assert.Equal(output, ModelPricing.All[i].OutputPerMillion);
        }
    }

    [Fact]
    public void CalculateCost_ZeroTokens_ReturnsZero()
    {
        var price = ModelPricing.All[0];
        var cost = ModelPricing.CalculateCost(price, 0, 0);
        Assert.Equal(0.0, cost);
    }

    [Fact]
    public void CalculateCost_1MEach_ReturnsSum()
    {
        var price = ModelPricing.All[0]; // Opus: $5/$25
        var cost = ModelPricing.CalculateCost(price, 1_000_000, 1_000_000);
        Assert.Equal(30.0, cost);
    }

    [Fact]
    public void CalculateCost_PromptOnly_Correct()
    {
        var price = ModelPricing.All[4]; // Gemini 3 Flash: $0.50/$3.00
        var cost = ModelPricing.CalculateCost(price, 500_000, 0);
        Assert.Equal(0.25, cost); // 0.5M * $0.50 = $0.25
    }

    [Fact]
    public void CalculateCost_CompletionOnly_Correct()
    {
        var price = ModelPricing.All[2]; // GPT-5.1: $1.25/$10.00
        var cost = ModelPricing.CalculateCost(price, 0, 250_000);
        Assert.Equal(2.5, cost); // 0.25M * $10.00 = $2.50
    }

    [Fact]
    public void CalculateCost_Qwen_Correct()
    {
        var price = ModelPricing.All[5]; // Qwen 3 Max: $1.20/$6.00
        var cost = ModelPricing.CalculateCost(price, 100_000, 50_000);
        Assert.Equal(0.42, cost); // 0.1M * $1.20 + 0.05M * $6.00 = $0.12 + $0.30 = $0.42
    }
}
