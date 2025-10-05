using FairShare.Interfaces;

namespace FairShare.Services
{
    /// <summary>
    /// The registry for child support calculators, allowing retrieval and listing of available calculators by state and form.
    /// </summary>
    /// <param name="calculators">The collection of available <see cref="IChildSupportCalculator"/> instances to register.</param>
    public class CalculatorRegistry(IEnumerable<IChildSupportCalculator> calculators) : ICalculatorRegistry
    {
        /// <summary>
        /// Provides a mapping of (State, Form) tuples to their corresponding <see cref="IChildSupportCalculator"/> instances.
        /// </summary>
        private readonly Dictionary<(string State, string Form), IChildSupportCalculator> _map = calculators.ToDictionary(
                c => (c.State.ToUpperInvariant(), c.Form.ToUpperInvariant()));

        /// <summary>
        /// Gets the child support calculator for the specified state and form.
        /// </summary>
        /// <param name="state">The state to search by.</param>
        /// <param name="form">The form to be used.</param>
        /// <returns>
        /// The <see cref="IChildSupportCalculator"/> matching the given <paramref name="state"/> and <paramref name="form"/>,
        /// or <c>null</c> if no matching calculator is registered.
        /// </returns>
        public IChildSupportCalculator? Get(string state, string form)
        {
            _map.TryGetValue((state.ToUpperInvariant(), form.ToUpperInvariant()), out IChildSupportCalculator? calc);
            return calc;
        }

        /// <summary>
        /// Compiles a list of all available calculators, returning their state, form, and display name.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> of tuples where each item contains the calculator’s
        /// <c>State</c>, <c>Form</c>, and a human-readable <c>Display</c> label.
        /// </returns>
        public IEnumerable<(string State, string Form, string Display)> List()
            => _map.Values
                .OrderBy(c => c.State).ThenBy(c => c.Form)
                .Select(c => (c.State, c.Form, $"{c.State} {c.Form}"));
    }
}
