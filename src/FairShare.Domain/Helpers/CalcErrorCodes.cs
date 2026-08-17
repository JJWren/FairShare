namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// The <see cref="CalcError.Code"/> values a calculator can report. Clients switch on these strings, so treat them as wire contract.
    /// </summary>
    public static class CalcErrorCodes
    {
        /// <summary>The number of children is outside the range the schedule covers (1-6).</summary>
        public const string InvalidChildCount = "INVALID_CHILD_COUNT";

        /// <summary>
        /// The combined adjusted gross income rounds above the top of the Basic Child-Support Obligation schedule ($30,000);
        /// the guidelines leave support above the schedule to the court's discretion, so no amount is computed.
        /// </summary>
        public const string IncomeAboveSchedule = "INCOME_ABOVE_SCHEDULE";

        /// <summary>Something unexpected went wrong; details are in the API log, not the response.</summary>
        public const string UnexpectedError = "UNEXPECTED_ERROR";
    }
}
