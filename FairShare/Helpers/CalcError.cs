using static FairShare.Helpers.Enums;

namespace FairShare.Helpers
{
    /// <summary>
    /// Organizes error information for calculations, including an error code, message, associated field, and severity level.
    /// </summary>
    public class CalcError
    {
        /// <summary>
        /// The error code representing the type of calculation error.
        /// </summary>
        public string Code { get; init; } = "CALC_ERROR";

        /// <summary>
        /// The human-readable message describing the calculation error.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// The specific field related to the error, if applicable.
        /// </summary>
        public string? Field { get; init; }    // e.g., "Plaintiff.MonthlyGrossIncome"

        /// <summary>
        /// The severity level of the error, defaulting to <see cref="ErrorSeverity.Error"/>.
        /// </summary>
        public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
    }
}
