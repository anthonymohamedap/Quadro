using System.Collections.Generic;

namespace QuadroApp.Service.Pricing;

public sealed class PricingResult
{
    public List<PricingRegelResult> Regels { get; } = new();

    public decimal SubtotaalExBtw { get; init; }
    public decimal BtwBedrag { get; init; }
    public decimal TotaalInclBtw { get; init; }
    public decimal VoorschotBedrag { get; init; }
}

public sealed class PricingRegelResult
{
    public int RegelId { get; init; }
    public decimal TotaalExcl { get; init; }
    public decimal SubtotaalExBtw { get; init; }
    public decimal BtwBedrag { get; init; }
    public decimal TotaalInclBtw { get; init; }
}
