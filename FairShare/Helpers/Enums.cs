namespace FairShare.Helpers
{
    /// <summary>
    /// Organizes various enumerations used throughout the FairShare application.
    /// </summary>
    public class Enums
    {
        /// <summary>
        /// The type of parent in the child support calculation context.
        /// </summary>
        public enum ParentType
        {
            Plaintiff,
            Defendant
        }

        /// <summary>
        /// The severity level of an error encountered during processing.
        /// </summary>
        public enum ErrorSeverity
        {
            Info,
            Warning,
            Error
        }
    }
}
