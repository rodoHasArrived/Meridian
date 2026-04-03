namespace Meridian.Contracts.SecurityMaster;

/// <summary>
/// Broad market capitalisation tier for equities and ETFs.
/// Thresholds follow common industry conventions (USD-denominated market cap).
/// </summary>
public enum MarketCapTier
{
    /// <summary>Market cap above ~$10 billion.</summary>
    LargeCap = 1,
    /// <summary>Market cap roughly $2 billion – $10 billion.</summary>
    MidCap = 2,
    /// <summary>Market cap roughly $300 million – $2 billion.</summary>
    SmallCap = 3,
    /// <summary>Market cap below ~$300 million.</summary>
    MicroCap = 4,
}
