using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
namespace FairShareShared.Interfaces
{
    public interface IStateGuidelineCatalog
    {
        IReadOnlyCollection<string> GetStates();
        IReadOnlyCollection<(string Form, string DisplayName)> GetFormsForState(string state);
        IChildSupportCalculator? GetCalculator(string state, string form);
    }
}



