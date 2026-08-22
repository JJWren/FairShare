using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Tests.Domain.Golden;

namespace FairShare.Tests.Domain;

public class OregonWorksheetGoldenTests
{
    /// <summary>The worksheet's numeric lines in printed order.</summary>
    public static readonly IReadOnlyList<string> LineNumbers =
    [
        "1a", "1b", "1c", "1d", "1e", "1f", "1g", "1h", "1i", "1j",
        "2a", "2b",
        "3a", "3b", "3c", "3d",
        "4a", "4b", "4c", "4f", "4g", "4h", "4i",
        "5b",
        "6a", "6b", "6c", "6d", "6e", "6f",
        "7a", "7b",
        "8a", "8c", "8d", "8e", "8f", "8g", "8h",
        "9a", "9b", "9c", "9d", "9e", "9g",
    ];

    [Theory]
    [MemberData(nameof(OregonGoldenCases.Worksheet), MemberType = typeof(OregonGoldenCases))]
    public void Calculate_MatchesTheOfficialWorkbook(string caseName)
    {
        OregonGoldenCase golden = OregonGoldenCases.Get(caseName);

        OregonCalculationOutcome outcome = new OregonWorksheetCalculator().Calculate(golden.Input.ToInput());

        OregonGoldenAssert.Matches(golden, outcome, LineNumbers);
    }
}
