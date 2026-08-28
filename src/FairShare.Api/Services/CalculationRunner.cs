using System;
using System.Collections.Generic;
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
public sealed class CalculationRunner : ICalculationRunner
{
    // Pre-indexed once per scope: O(1) dispatch, and a duplicate (state, form)
    // registration fails loudly here instead of silently shadowing a form.
    private readonly Dictionary<string, IFormRunner> _forms;

    public CalculationRunner(IEnumerable<IFormRunner> forms)
    {
        _forms = new Dictionary<string, IFormRunner>(StringComparer.OrdinalIgnoreCase);

        foreach (IFormRunner runner in forms)
        {
            string key = FormKey(runner.State, runner.Form);

            if (!_forms.TryAdd(key, runner))
            {
                throw new InvalidOperationException($"Duplicate form runner registered for {key}.");
            }
        }
    }

    public CalculationRun Run(string state, string form, CalculationRequest request)
    {
        if (!_forms.TryGetValue(FormKey(state, form), out IFormRunner? runner))
        {
            return new CalculationRun(false, null, null);
        }

        FormRunResult result = runner.Run(request, state, form);
        return new CalculationRun(true, result.InputError, result.Response);
    }

    // State codes never contain '/', so the joined key is collision-free.
    private static string FormKey(string state, string form) => $"{state}/{form}";
}
