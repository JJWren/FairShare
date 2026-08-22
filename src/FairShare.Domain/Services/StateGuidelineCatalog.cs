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
        private readonly Dictionary<string, List<IWorksheetForm>> _byState =
            new(StringComparer.OrdinalIgnoreCase);

        public StateGuidelineCatalog(IEnumerable<IWorksheetForm> forms)
        {
            foreach (IWorksheetForm form in forms)
            {
                if (!_byState.TryGetValue(form.State, out List<IWorksheetForm>? list))
                {
                    list = new List<IWorksheetForm>();
                    _byState[form.State] = list;
                }

                list.Add(form);
            }
        }

        public IReadOnlyCollection<string> GetStates()
            => _byState.Keys
                .OrderBy(s => s)
                .ToArray();

        // Ordered by form key so the first entry is stable (the SPA lands on it when no form is given).
        public IReadOnlyCollection<FormInfo> GetFormsForState(string state)
            => _byState.TryGetValue(state, out List<IWorksheetForm>? list)
                ? list
                    .OrderBy(f => f.Form, StringComparer.Ordinal)
                    .Select(f => new FormInfo(f.Form, f.DisplayName, f.Description, f.IsSharedCustody))
                    .ToArray()
                : Array.Empty<FormInfo>();

        public IWorksheetForm? GetForm(string state, string form)
            => _byState.TryGetValue(state, out List<IWorksheetForm>? list)
                ? list.FirstOrDefault(f => f.Form.Equals(form, StringComparison.OrdinalIgnoreCase))
                : null;

        public IChildSupportCalculator? GetCalculator(string state, string form)
            => GetForm(state, form) as IChildSupportCalculator;
    }
}
