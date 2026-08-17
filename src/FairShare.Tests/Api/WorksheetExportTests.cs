using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class WorksheetExportTests : IClassFixture<FairShareApiFactory>
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly HttpClient _client;

    public WorksheetExportTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task ExportXlsx_CS42_WritesInputsAndKeepsFormulas()
    {
        string accessToken = await LoginAsAdminAsync();
        WorksheetExportRequest request = WorkbookDefaults(plaintiffPrimary: true);
        request.PlaintiffName = "Jane P.";
        request.DefendantName = "John D.";

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/CS42/calculations/export/xlsx", accessToken, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxContentType, response.Content.Headers.ContentType?.MediaType);
        string? fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
        Assert.Matches(new Regex(@"^FairShare_AL_CS-42_\d{8}\.xlsx$"), fileName?.Trim('"') ?? string.Empty);

        using XLWorkbook workbook = new(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        IXLWorksheet sheet = workbook.Worksheet("Form CS-42 Worksheet");

        // Inputs landed in the unlocked cells...
        Assert.Equal(1, sheet.Cell("K12").GetValue<int>());
        Assert.Equal(1200, sheet.Cell("H14").GetValue<int>());
        Assert.Equal(100, sheet.Cell("H21").GetValue<int>());
        Assert.Equal(1000, sheet.Cell("J14").GetValue<int>());
        Assert.Equal(20, sheet.Cell("J20").GetValue<int>());
        Assert.Equal("Jane P.", sheet.Cell("B6").GetString());
        Assert.Equal("John D.", sheet.Cell("F6").GetString());

        // ...the workbook's own formulas are still there and evaluate to the workbook's own answer...
        Assert.StartsWith("VLOOKUP(", sheet.Cell("L19").FormulaA1);
        Assert.Equal(186, sheet.Cell("H30").GetValue<int>());
        Assert.Equal(50, sheet.Cell("J30").GetValue<int>());

        // ...and the AOC's sheet protection was left alone.
        Assert.True(sheet.IsProtected);
    }

    [Fact]
    public async Task ExportXlsx_CS42S_PlacesOrderInPayingParentsColumn()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/CS42S/calculations/export/xlsx", accessToken, WorkbookDefaults(plaintiffPrimary: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(new Regex(@"^FairShare_AL_CS-42-S_\d{8}\.xlsx$"),
            (response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName)?.Trim('"') ?? string.Empty);

        using XLWorkbook workbook = new(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        IXLWorksheet sheet = workbook.Worksheet("Form CS-42-S");

        Assert.Equal(20, sheet.Cell("J22").GetValue<int>());
        Assert.Equal(100, sheet.Cell("H23").GetValue<int>());
        Assert.Equal(2, sheet.Cell("J32").GetValue<int>());
        Assert.Equal(string.Empty, sheet.Cell("H32").GetString());
    }

    [Fact]
    public async Task ExportXlsx_UnknownForm_ReturnsNotFound()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/BOGUS/calculations/export/xlsx", accessToken, WorkbookDefaults(plaintiffPrimary: true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExportXlsx_InvalidChildCount_ReturnsBadRequestWithCalculationErrors()
    {
        string accessToken = await LoginAsAdminAsync();
        WorksheetExportRequest request = WorkbookDefaults(plaintiffPrimary: true);
        request.NumberOfChildren = 0;

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/CS42/calculations/export/xlsx", accessToken, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        CalculationResponse body = (await response.Content.ReadFromJsonAsync<CalculationResponse>())!;
        Assert.False(body.Success);
        Assert.Contains(body.Errors, e => e.Code == "INVALID_CHILD_COUNT");
    }

    [Fact]
    public async Task ExportXlsx_IncomeAboveSchedule_ReturnsBadRequest()
    {
        string accessToken = await LoginAsAdminAsync();
        WorksheetExportRequest request = new()
        {
            NumberOfChildren = 2,
            Plaintiff = new ParentDataDto { HasPrimaryCustody = true, MonthlyGrossIncome = 20025 },
            Defendant = new ParentDataDto { MonthlyGrossIncome = 10000 }
        };

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/CS42/calculations/export/xlsx", accessToken, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        CalculationResponse body = (await response.Content.ReadFromJsonAsync<CalculationResponse>())!;
        Assert.Contains(body.Errors, e => e.Code == "INCOME_ABOVE_SCHEDULE");
    }

    [Fact]
    public async Task ExportXlsx_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "api/v1/states/AL/forms/CS42/calculations/export/xlsx", WorkbookDefaults(plaintiffPrimary: true));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WorksheetExportRequest WorkbookDefaults(bool plaintiffPrimary) => new()
    {
        NumberOfChildren = 1,
        Plaintiff = new ParentDataDto { HasPrimaryCustody = plaintiffPrimary, MonthlyGrossIncome = 1200, HealthcareCoverageCosts = 100 },
        Defendant = new ParentDataDto { MonthlyGrossIncome = 1000, WorkRelatedChildcareCosts = 20 }
    };

    private async Task<string> LoginAsAdminAsync()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345"
        });

        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return tokens.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string accessToken, object body)
    {
        using HttpRequestMessage request = new(method, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        return await _client.SendAsync(request);
    }
}
