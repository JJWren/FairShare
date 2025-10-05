namespace FairShare.ViewModels
{
    /// <summary>
    /// Represents the data model for the home view, including the selected state, selected form,  and a list of
    /// available states.
    /// </summary>
    public class HomeViewModel
    {
        /// <summary>
        /// Gets or sets the selected state.
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Gets or sets the selected form.
        /// </summary>
        public string? Form { get; set; }

        /// <summary>
        /// Gets or sets the list of available states.
        /// </summary>
        public List<string> States { get; set; } = [];
    }
}
