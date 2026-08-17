using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Tests.Domain.Golden;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Domain;

public class CS42SGoldenTests
{
    private static readonly string[] LineNumbers = ["1", "1a", "1b", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14"];

    private readonly CS42SCalculator _calculator = new(NullLogger<CS42SCalculator>.Instance);

    // Every expected value comes from the official Form CS-42-S (Eff. 6/2023) workbook - see Golden/Generate-GoldenCases.ps1.
    [Theory]
    [MemberData(nameof(GoldenCases.CS42S), MemberType = typeof(GoldenCases))]
    public void Calculate_MatchesOfficialWorkbook(string caseName)
    {
        GoldenCase golden = GoldenCases.GetCS42S(caseName);

        CalculationResult result = _calculator.Calculate(
            golden.Plaintiff.ToParentData(), golden.Defendant.ToParentData(), golden.NumberOfChildren);

        GoldenAssert.Matches(golden, result, LineNumbers);
    }
}
