using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// Catalog summary of one worksheet a state offers.
    /// </summary>
    /// <param name="Form">The form key used in routes and requests, e.g. "CS42".</param>
    /// <param name="DisplayName">Human-readable name including the revision, e.g. "CS-42 (Rev. 5/2022)".</param>
    /// <param name="Description">One-line description of when the form applies, e.g. "Standard custody".</param>
    /// <param name="IsSharedCustody">Whether the form implements a shared-custody variant (and therefore ignores the primary-custody flag).</param>
    public sealed record FormInfo(string Form, string DisplayName, string Description, bool IsSharedCustody);
}
