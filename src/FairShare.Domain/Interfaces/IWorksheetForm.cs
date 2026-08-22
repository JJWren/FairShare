namespace FairShare.Domain.Interfaces
{
    /// <summary>
    /// The catalog-facing identity of one state worksheet form. Every form implements this;
    /// Alabama-style forms additionally implement <see cref="IChildSupportCalculator"/>, while
    /// Oregon's worksheet takes its own richer input and is dispatched by concrete type - the
    /// catalog lists both kinds side by side.
    /// </summary>
    public interface IWorksheetForm
    {
        /// <summary>The two-letter abbreviation for the state this form belongs to.</summary>
        string State { get; }

        /// <summary>The form key used in routes and requests, e.g. "CS42" or "Worksheet".</summary>
        string Form { get; }

        /// <summary>Human-readable form name including its revision, e.g. "CS-42 (Rev. 5/2022)".</summary>
        string DisplayName { get; }

        /// <summary>One-line description of when the form applies, e.g. "Standard custody".</summary>
        string Description { get; }

        /// <summary>Whether this form implements a shared-custody guideline variant.</summary>
        bool IsSharedCustody { get; }
    }
}
