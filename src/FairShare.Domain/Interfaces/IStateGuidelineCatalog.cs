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
        IChildSupportCalculator? GetCalculator(string state, string form);
    }
}
