using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Tests.Domain.Golden;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Domain;

public class CS42GoldenTests
{
    private readonly CS42Calculator _calculator = new(NullLogger<CS42Calculator>.Instance);

    // Every expected value comes from the official Form CS-42 (Rev. 5/2022) workbook - see Golden/Generate-GoldenCases.ps1.
    [Theory]
    [MemberData(nameof(GoldenCases.CS42), MemberType = typeof(GoldenCases))]
    public void Calculate_MatchesOfficialWorkbook(string caseName)
    {
        GoldenCase golden = GoldenCases.GetCS42(caseName);

        CalculationResult result = _calculator.Calculate(
            golden.Plaintiff.ToParentData(), golden.Defendant.ToParentData(), golden.NumberOfChildren);

        GoldenAssert.Matches(golden, result);
    }
}
