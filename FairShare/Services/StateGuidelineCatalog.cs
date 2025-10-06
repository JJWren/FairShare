using FairShare.Interfaces;

namespace FairShare.Services
{
    public class StateGuidelineCatalog : IStateGuidelineCatalog
    {
        private readonly Dictionary<string, List<IChildSupportCalculator>> _byState =
            new(StringComparer.OrdinalIgnoreCase);

        public StateGuidelineCatalog(IEnumerable<IChildSupportCalculator> calculators)
        {
            foreach (IChildSupportCalculator calc in calculators)
            {
                if (!_byState.TryGetValue(calc.State, out List<IChildSupportCalculator>? list))
                {
                    list = new List<IChildSupportCalculator>();
                    _byState[calc.State] = list;
                }

                list.Add(calc);
            }
        }

        public IReadOnlyCollection<string> GetStates()
            => _byState.Keys
                .OrderBy(s => s)
                .ToArray();

        public IReadOnlyCollection<(string Form, string DisplayName)> GetFormsForState(string state)
            => _byState.TryGetValue(state, out List<IChildSupportCalculator>? list)
                ? list
                    .OrderBy(c => c.Form)
                    .Select(c => (c.Form, c.Form))
                    .ToArray()
                : Array.Empty<(string, string)>();

        public IChildSupportCalculator? GetCalculator(string state, string form)
            => _byState.TryGetValue(state, out List<IChildSupportCalculator>? list)
                ? list.FirstOrDefault(c => c.Form.Equals(form, StringComparison.OrdinalIgnoreCase))
                : null;
    }
}
