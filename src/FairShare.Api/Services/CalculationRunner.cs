using System;
using System.Collections.Generic;
using System.Linq;
using FairShare.Api.Services.Forms;
using FairShare.Contracts.Calculation;

namespace FairShare.Api.Services;

/// <summary>
/// The outcome of dispatching one calculation request. <see cref="FormFound"/> false maps to 404;
/// a non-null <see cref="InputError"/> maps to 400; otherwise <see cref="Response"/> is set (its
/// own Success flag distinguishes computed results from validation errors inside the worksheet).
/// </summary>
public sealed record CalculationRun(bool FormFound, string? InputError, CalculationResponse? Response);

/// <summary>
/// Runs a calculation request against the right calculator for a (state, form) - the shared engine
/// behind the calculations endpoint and saved scenarios (which recompute on save and on reopen).
/// </summary>
public interface ICalculationRunner
{
    CalculationRun Run(string state, string form, CalculationRequest request);
}

/// <summary>
/// Pure dispatch: selects the <see cref="IFormRunner"/> registered for (state, form) - the same
/// case-insensitive matching the catalog uses - and hands it the request. Every shape-specific
/// concern (which inputs the form needs, how its outcome maps back to the wire) lives in that
/// form's runner, so adding a state never touches this class.
/// </summary>
public sealed class CalculationRunner(IEnumerable<IFormRunner> forms) : ICalculationRunner
{
    private readonly IReadOnlyList<IFormRunner> _forms = forms.ToList();

    public CalculationRun Run(string state, string form, CalculationRequest request)
    {
        IFormRunner? runner = _forms.FirstOrDefault(f =>
            f.State.Equals(state, StringComparison.OrdinalIgnoreCase)
            && f.Form.Equals(form, StringComparison.OrdinalIgnoreCase));

        if (runner is null)
        {
            return new CalculationRun(false, null, null);
        }

        FormRunResult result = runner.Run(request);
        return new CalculationRun(true, result.InputError, result.Response);
    }
}
