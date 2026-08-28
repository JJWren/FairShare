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
            AL,
            OR
        }

        /// <summary>
        /// Human names for <see cref="States"/> codes. The landing picker shows these -
        /// a stressed parent should never have to decode a two-letter abbreviation.
        /// A new state registers its name here alongside its enum member.
        /// </summary>
        public static class StateNames
        {
            public static string For(string stateCode) => stateCode switch
            {
                nameof(States.AL) => "Alabama",
                nameof(States.OR) => "Oregon",
                _ => stateCode
            };
        }

        /// <summary>
        /// Represents the available forms used in the application.
        /// </summary>
        public enum Forms
        {
            CS42,
            CS42S,

            /// <summary>Oregon's single Child Support Worksheet (CSF 02 0910).</summary>
            Worksheet
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
