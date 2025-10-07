using FairShare.Models;

namespace FairShare.ViewModels
{
    /// <summary>
    /// View model used by the _ParentCard partial to render a parent's financial inputs
    /// while producing field names that bind back to <see cref="CalculatorViewModel"/>.[Plaintiff|Defendant].
    /// Inherits from <seealso cref="ParentData"/> so tag helpers (asp-for) work without changing the partial markup.
    /// </summary>
    public class ParentCardViewModel : ParentData
    {
        public string Title { get; }
        public string Prefix { get; }
        public string? DisplayName { get; }

        public ParentCardViewModel(string title, string prefix, ParentData source, string? displayName)
        {
            Title = title;
            Prefix = prefix;
            DisplayName = displayName;
            MonthlyGrossIncome = source.MonthlyGrossIncome;
            PreexistingChildSupport = source.PreexistingChildSupport;
            PreexistingAlimony = source.PreexistingAlimony;
            WorkRelatedChildcareCosts = source.WorkRelatedChildcareCosts;
            HealthcareCoverageCosts = source.HealthcareCoverageCosts;
            HasPrimaryCustody = source.HasPrimaryCustody;
        }
    }
}
