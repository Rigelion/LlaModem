namespace LlaModem.Models;

/// <summary>
/// Cost estimate for a single model.
/// </summary>
public readonly record struct ModelCost(
    string Model,
    double InputCost,
    double OutputCost,
    double TotalCost);

/// <summary>
/// Response envelope for cost comparison across cloud models.
/// </summary>
public sealed record CostComparisonResponse(
    DateRange Period,
    string? ModelFilter,
    ModelCost[] Costs,
    ModelCost Cheapest,
    ModelCost MostExpensive);
