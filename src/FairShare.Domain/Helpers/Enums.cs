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

        /// <summary>
        /// How a worksheet line's values should be displayed.
        /// </summary>
        public enum LineFormat
        {
            /// <summary>Whole-dollar amounts.</summary>
            Currency,

            /// <summary>Fractions in the range 0..1 (e.g. 0.57 = 57%).</summary>
            Percent,

            /// <summary>Plain counts (children, overnights) - no currency symbol.</summary>
            Number
        }
    }
}
