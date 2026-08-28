using FairShare.Contracts.Calculation;

namespace FairShare.Api.Services.Forms;

/// <summary>
/// One worksheet form's API-side entry point: owns mapping the wire request into the form's
/// own domain input shape, running the calculator, and mapping the outcome back to the wire.
/// <see cref="CalculationRunner"/> picks a runner by (State, Form) and knows nothing about
/// shapes - adding a state adds an implementation (or reuses <see cref="ClassicFormRunner"/>
/// for two-parents-plus-child-count forms) plus DI registration, and touches no existing
/// calculator: ADR 0005's additive promise, now independent of the form's input shape.
/// </summary>
public interface IFormRunner
{
    /// <summary>Two-letter state code, e.g. "AL".</summary>
    string State { get; }

    /// <summary>Form key used in routes and requests, e.g. "CS42" or "Worksheet".</summary>
    string Form { get; }

    FormRunResult Run(CalculationRequest request);
}

/// <summary>
/// A non-null <see cref="InputError"/> maps to 400 (the request shape doesn't fit the form);
/// otherwise <see cref="Response"/> is set, and its own Success flag distinguishes computed
/// results from validation errors inside the worksheet.
/// </summary>
public sealed record FormRunResult(string? InputError, CalculationResponse? Response);
