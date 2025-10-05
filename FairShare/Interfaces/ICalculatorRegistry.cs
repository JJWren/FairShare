namespace FairShare.Interfaces
{
    /// <summary>
    /// The registry for child support calculators, allowing retrieval and listing of available calculators by state and form.
    /// </summary>
    public interface ICalculatorRegistry
    {
        /// <summary>
        /// Gets the child support calculator for the specified state and form.
        /// </summary>
        /// <param name="state">The state to search by.</param>
        /// <param name="form">The form to be used.</param>
        /// <returns>
        /// The <see cref="IChildSupportCalculator"/> matching the given <paramref name="state"/> and <paramref name="form"/>,
        /// or <c>null</c> if no matching calculator is registered.
        /// </returns>
        IChildSupportCalculator? Get(string state, string form);

        /// <summary>
        /// Compiles a list of all available calculators, returning their state, form, and display name.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> of tuples where each item contains the calculator’s
        /// <c>State</c>, <c>Form</c>, and a human-readable <c>Display</c> label.
        /// </returns>
        IEnumerable<(string State, string Form, string Display)> List();
    }
}
