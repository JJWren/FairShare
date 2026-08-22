using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Models;
using Microsoft.Extensions.Logging;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Calculators
{
    /// <summary>
    /// Template for worksheet calculators. <see cref="Calculate"/> owns validation, error mapping and the result
    /// shell; a derived form fills in <see cref="BuildWorksheet"/> by walking its lines top to bottom.
    /// The arithmetic helpers reproduce Excel semantics on purpose: the official AOC workbooks are the reference
    /// implementation, and Excel's ROUND rounds halves away from zero (unlike .NET's default banker's rounding).
    /// </summary>
    public abstract class BaseChildSupportCalculator(ILogger logger) : IChildSupportCalculator
    {
        private readonly ILogger _logger = logger;

        public abstract string State { get; }
        public abstract string Form { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }

        /// <summary>
        /// The state's schedule of basic obligations. Child-count bounds, bracket selection and
        /// above-ceiling behavior all come from here — the template stays state-agnostic (ADR 0005).
        /// </summary>
        protected abstract IObligationSchedule Schedule { get; }

        /// <summary>
        /// Shared custody flag; override in derived calculators that implement shared custody rules.
        /// </summary>
        public virtual bool IsSharedCustody => false;

        public CalculationResult Calculate(ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            CalculationResult result = CreateResultShell(numberOfChildren);

            if (numberOfChildren < Schedule.MinChildren || numberOfChildren > Schedule.MaxChildren)
            {
                AddError(result, CalcErrorCodes.InvalidChildCount,
                    $"Number of children must be between {Schedule.MinChildren} and {Schedule.MaxChildren}.",
                    nameof(numberOfChildren));
                return result;
            }

            try
            {
                WorksheetBuilder worksheet = new();
                WorksheetOutcome outcome = BuildWorksheet(worksheet, plaintiff, defendant, numberOfChildren);

                result.Lines = worksheet.Build();
                result.Payer = outcome.Payer;
                result.FinalAmount = outcome.FinalAmount;
                result.Success = true;
            }
            catch (IncomeAboveScheduleException ex)
            {
                // The schedule words this message in its own state's terms (ADR 0005).
                AddError(result, CalcErrorCodes.IncomeAboveSchedule, ex.Message, "CombinedAdjustedGrossIncome");
                _logger.LogWarning(ex, "Income above schedule in {Form} calculation.", Form);
            }
            catch (Exception ex)
            {
                AddError(result, CalcErrorCodes.UnexpectedError, "An unexpected error occurred during calculation.", null);
                _logger.LogError(ex, "Unexpected error in {Form} calculation.", Form);
            }

            return result;
        }

        /// <summary>
        /// Walks the form line by line, appending each to <paramref name="worksheet"/>, and returns who pays what.
        /// The child count has already been validated. Throw <see cref="IncomeAboveScheduleException"/> (via
        /// <see cref="GetBasicChildSupportObligation"/>) when the income is off the schedule; the template maps it to an error.
        /// </summary>
        protected abstract WorksheetOutcome BuildWorksheet(WorksheetBuilder worksheet, ParentData plaintiff, ParentData defendant, int numberOfChildren);

        /// <summary>
        /// Who pays and how much, as read off the finished worksheet. An empty <see cref="Payer"/> with a zero
        /// amount means "no net transfer".
        /// </summary>
        protected readonly record struct WorksheetOutcome(string Payer, int FinalAmount);

        /// <summary>
        /// Lines 1 through 2 as they appear on both Alabama worksheets (gross income, the two preexisting-payment
        /// deductions, adjusted gross income) plus the combined column of each.
        /// </summary>
        protected static IncomeLines AddIncomeLines(WorksheetBuilder worksheet, ParentData plaintiff, ParentData defendant, string childSupportLabel, string alimonyLabel)
        {
            int plaintiffAdjusted = plaintiff.GetMonthlyAdjustedGrossIncome();
            int defendantAdjusted = defendant.GetMonthlyAdjustedGrossIncome();

            worksheet
                .Add("1", "MONTHLY GROSS INCOME",
                    plaintiff.MonthlyGrossIncome, defendant.MonthlyGrossIncome,
                    plaintiff.MonthlyGrossIncome + defendant.MonthlyGrossIncome)
                .Add("1a", childSupportLabel,
                    plaintiff.PreexistingChildSupport, defendant.PreexistingChildSupport,
                    plaintiff.PreexistingChildSupport + defendant.PreexistingChildSupport)
                .Add("1b", alimonyLabel,
                    plaintiff.PreexistingAlimony, defendant.PreexistingAlimony,
                    plaintiff.PreexistingAlimony + defendant.PreexistingAlimony)
                .Add("2", "MONTHLY ADJUSTED GROSS INCOME",
                    plaintiffAdjusted, defendantAdjusted, plaintiffAdjusted + defendantAdjusted);

            return new IncomeLines(
                plaintiff.MonthlyGrossIncome, defendant.MonthlyGrossIncome,
                plaintiffAdjusted, defendantAdjusted, plaintiffAdjusted + defendantAdjusted);
        }

        /// <summary>
        /// The values from lines 1 and 2 that the rest of the worksheet is built on.
        /// </summary>
        protected readonly record struct IncomeLines(
            int PlaintiffGross,
            int DefendantGross,
            int PlaintiffAdjusted,
            int DefendantAdjusted,
            int CombinedAdjusted);

        /// <summary>
        /// A parent's share of the combined adjusted gross income the way Excel's <c>ROUND(x/total, 2)</c> reports it.
        /// Returns 0 when the combined income is 0 (where the workbook would show #DIV/0!).
        /// </summary>
        protected static decimal ShareOfIncome(int parentAdjustedGrossIncome, int combinedAdjustedGrossIncome)
            => combinedAdjustedGrossIncome == 0
                ? 0m
                : ExcelRound2(parentAdjustedGrossIncome / (decimal)combinedAdjustedGrossIncome);

        /// <summary>Excel <c>ROUND(value, 0)</c>: to a whole number, halves away from zero.</summary>
        protected static int ExcelRound(decimal value)
            => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

        /// <summary>Excel <c>ROUND(value, 2)</c>: to two decimals, halves away from zero.</summary>
        protected static decimal ExcelRound2(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        /// <summary>
        /// The schedule amount for the combined adjusted gross income (worksheet line 4).
        /// </summary>
        protected int GetBasicChildSupportObligation(int numberOfChildren, int combinedAdjustedGrossIncome)
            => Schedule.GetBasicObligation(combinedAdjustedGrossIncome, numberOfChildren);

        protected CalculationResult CreateResultShell(int numberOfChildren)
            => new(string.Empty, 0)
            {
                Success = false,
                State = State,
                Form = Form,
                NumberOfChildren = numberOfChildren,
            };

        private static void AddError(CalculationResult result, string code, string message, string? field)
        {
            result.Success = false;
            result.Errors.Add(new CalcError
            {
                Code = code,
                Message = message,
                Field = field,
                Severity = ErrorSeverity.Error
            });
        }
    }
}
