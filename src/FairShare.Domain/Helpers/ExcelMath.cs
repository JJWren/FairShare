using System;

namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// Excel's <c>ROUND</c> - halves away from zero, unlike .NET's default banker's rounding.
    /// The official workbooks are the reference implementation (ADR 0001), so every worksheet
    /// rounding goes through here with the digit count the workbook's formula uses.
    /// </summary>
    internal static class ExcelMath
    {
        /// <summary>Excel <c>ROUND(value, digits)</c>.</summary>
        public static decimal Round(decimal value, int digits)
            => Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }
}
