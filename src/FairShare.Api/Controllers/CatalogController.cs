using System.Collections.Generic;
using System.Linq;
using FairShare.Contracts.Catalog;
using FairShare.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Api.Controllers;

[ApiController]
[Route("api/v1/states")]
// Anonymous by design: the catalog is inert public data (state and form names), and the
// homepage must render its state picker without any session so Google's OAuth verification
// never sees a login-gated landing page.
[AllowAnonymous]
public class CatalogController(IStateGuidelineCatalog catalog) : ControllerBase
{
    private readonly IStateGuidelineCatalog _catalog = catalog;

    [HttpGet]
    public ActionResult<IEnumerable<StateSummaryDto>> GetStates()
    {
        IEnumerable<StateSummaryDto> states = _catalog.GetStates()
            .Select(s => new StateSummaryDto { State = s, DisplayName = StateNames.For(s) });
        return Ok(states);
    }

    [HttpGet("{state}/forms")]
    public ActionResult<IEnumerable<FormSummaryDto>> GetForms(string state)
    {
        IEnumerable<FormSummaryDto> forms = _catalog.GetFormsForState(state)
            .Select(f => new FormSummaryDto
            {
                Form = f.Form,
                DisplayName = f.DisplayName,
                Description = f.Description,
                IsSharedCustody = f.IsSharedCustody
            });

        return Ok(forms);
    }
}
