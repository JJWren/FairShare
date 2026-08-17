using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;

namespace FairShare.Domain.Services
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

        // Ordered by form key so the first entry is stable (the SPA lands on it when no form is given).
        public IReadOnlyCollection<FormInfo> GetFormsForState(string state)
            => _byState.TryGetValue(state, out List<IChildSupportCalculator>? list)
                ? list
                    .OrderBy(c => c.Form, StringComparer.Ordinal)
                    .Select(c => new FormInfo(c.Form, c.DisplayName, c.Description, c.IsSharedCustody))
                    .ToArray()
                : Array.Empty<FormInfo>();

        public IChildSupportCalculator? GetCalculator(string state, string form)
            => _byState.TryGetValue(state, out List<IChildSupportCalculator>? list)
                ? list.FirstOrDefault(c => c.Form.Equals(form, StringComparison.OrdinalIgnoreCase))
                : null;
    }
}
