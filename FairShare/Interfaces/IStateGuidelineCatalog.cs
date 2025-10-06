namespace FairShare.Interfaces
{
    public interface IStateGuidelineCatalog
    {
        IReadOnlyCollection<string> GetStates();
        IReadOnlyCollection<(string Form, string DisplayName)> GetFormsForState(string state);
        IChildSupportCalculator? GetCalculator(string state, string form);
    }
}
