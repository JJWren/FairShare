namespace FairShare.Contracts.Catalog;

public class StateSummaryDto
{
    /// <summary>Two-letter state code, e.g. "AL".</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Human name for pickers, e.g. "Alabama".</summary>
    public string DisplayName { get; set; } = string.Empty;
}

public class FormSummaryDto
{
    public string Form { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSharedCustody { get; set; }
}
