using System.Collections.Generic;
using System.Linq;
using FairShare.Contracts.Catalog;
using FairShare.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Api.Controllers;

[ApiController]
[Route("api/v1/states")]
[Authorize]
public class CatalogController(IStateGuidelineCatalog catalog) : ControllerBase
{
    private readonly IStateGuidelineCatalog _catalog = catalog;

    [HttpGet]
    public ActionResult<IEnumerable<StateSummaryDto>> GetStates()
    {
        IEnumerable<StateSummaryDto> states = _catalog.GetStates().Select(s => new StateSummaryDto { State = s });
        return Ok(states);
    }

    [HttpGet("{state}/forms")]
    public ActionResult<IEnumerable<FormSummaryDto>> GetForms(string state)
    {
        IEnumerable<FormSummaryDto> forms = _catalog.GetFormsForState(state)
            .Select(f => new FormSummaryDto { Form = f.Form, DisplayName = f.DisplayName });

        return Ok(forms);
    }
}
