using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Domain.Helpers;

namespace FairShare.Domain.Interfaces
{
    public interface IStateGuidelineCatalog
    {
        IReadOnlyCollection<string> GetStates();
        IReadOnlyCollection<FormInfo> GetFormsForState(string state);

        /// <summary>The form's catalog entry, whatever input shape it takes; null when unknown.</summary>
        IWorksheetForm? GetForm(string state, string form);

        /// <summary>The form as a classic two-parents-plus-child-count calculator; null when unknown or when the form takes richer inputs (Oregon).</summary>
        IChildSupportCalculator? GetCalculator(string state, string form);
    }
}
