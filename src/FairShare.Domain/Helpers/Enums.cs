using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
namespace FairShare.Domain.Helpers
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

        /// <summary>
        /// Represents the states in the United States.
        /// </summary>
        public enum States
        {
            AL
        }

        /// <summary>
        /// Represents the available forms used in the application.
        /// </summary>
        public enum Forms
        {
            CS42,
            CS42S
        }
    }
}






