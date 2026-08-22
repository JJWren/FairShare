# Regenerates or-worksheet-golden.json by driving the official Oregon DOJ Guidelines Calculator
# workbook (child_support_worksheet.xls, https://justice.oregon.gov/child-support/xls/) through
# Excel COM and reading every worksheet line back. The workbook is the reference implementation
# (ADR 0001): expected values come FROM the sheet, never from FairShare's own math.
#
# Usage:  .\Generate-OregonGoldenCases.ps1 [-WorkbookPath <path to child_support_worksheet.xls>]
# Requires desktop Excel. Unrounded intermediate cells carry double noise, so every value is
# rounded to 6 decimals on both sides of the comparison (cent- and dollar-rounded lines are exact).

param(
    [string]$WorkbookPath = (Join-Path $env:USERPROFILE "Downloads\oregon_child_support_worksheet.xls"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "or-worksheet-golden.json")
)

$ErrorActionPreference = "Stop"

# Input cell map (named ranges in the workbook; parent 1 = Plaintiff/D column, parent 2 = Defendant/E column).
$inputCells = @{
    pname1 = "D10"; pname2 = "E10"
    income1 = "D12"; income2 = "E12"
    spousalplus1 = "D14"; spousalplus2 = "E14"
    spousalminus1 = "D15"; spousalminus2 = "E15"
    uniondues1 = "D16"; uniondues2 = "E16"
    parentpremium1 = "D17"; parentpremium2 = "E17"
    addch1 = "D19"; addch2 = "E19"
    jointminorch = "D20"; jointCAS = "D22"
    ccoop1 = "D38"; ccoop2 = "E38"
    premium1 = "D46"; premium2 = "E46"
    compelling = "D51"; whoprovides = "D52"
    cmelection = "D60"
    ptime1 = "D66"; ptime2 = "E66"
    exception1 = "D82"; exception2 = "E82"
    ssava1 = "D85"; ssava2 = "E85"
}

# Output line map: worksheet line id -> plaintiff/defendant/combined cells (null = form has no cell there).
$lineCells = [ordered]@{
    "1a" = @{ p = "D12"; d = "E12" }
    "1b" = @{ p = "D18"; d = "E18" }
    "1c" = @{ p = "D19"; d = "E19" }
    "1d" = @{ c = "D20" }
    "1e" = @{ c = "D22" }
    "1f" = @{ p = "D23"; d = "E23" }
    "1g" = @{ p = "D24"; d = "E24" }
    "1h" = @{ p = "D25"; d = "E25"; c = "F26" }
    "1i" = @{ p = "D27"; d = "E27" }
    "1j" = @{ p = "D28"; d = "E28" }
    "2a" = @{ c = "F32" }
    "2b" = @{ p = "D34"; d = "E34" }
    "3a" = @{ p = "D38"; d = "E38" }
    "3b" = @{ p = "D39"; d = "E39" }
    "3c" = @{ p = "D40"; d = "E40" }
    "3d" = @{ p = "D42"; d = "E42" }
    "4a" = @{ p = "D46"; d = "E46" }
    "4b" = @{ p = "D47"; d = "E47" }
    "4c" = @{ p = "D48"; d = "E48"; c = "F49" }
    "4f" = @{ c = "F53" }
    "4g" = @{ p = "D54"; d = "E54" }
    "4h" = @{ p = "D55"; d = "E55" }
    "4i" = @{ p = "D56"; d = "E56" }
    "5b" = @{ p = "D62"; d = "E62" }
    "6a" = @{ p = "D66"; d = "E66" }
    "6b" = @{ p = "D67"; d = "E67" }
    "6c" = @{ p = "D68"; d = "E68" }
    "6d" = @{ p = "D69"; d = "E69" }
    "6e" = @{ p = "D70"; d = "E70" }
    "6f" = @{ p = "D71"; d = "E71" }
    "7a" = @{ p = "D75"; d = "E75" }
    "7b" = @{ p = "D76"; d = "E76" }
    "8a" = @{ p = "D81"; d = "E81" }
    "8c" = @{ p = "D83"; d = "E83" }
    "8d" = @{ p = "D84"; d = "E84" }
    "8e" = @{ p = "D85"; d = "E85" }
    "8f" = @{ p = "D86"; d = "E86" }
    "8g" = @{ p = "D87"; d = "E87" }
    "8h" = @{ p = "D88"; d = "E88" }
    "9a" = @{ p = "D92"; d = "E92" }
    "9b" = @{ p = "D93"; d = "E93" }
    "9c" = @{ p = "D94"; d = "E94" }
    "9d" = @{ p = "D95"; d = "E95" }
    "9e" = @{ p = "D96"; d = "E96" }
    "9g" = @{ c = "D98" }
}

function New-Parent {
    param([decimal]$Income = 0, [decimal]$SpousalReceived = 0, [decimal]$SpousalPaid = 0,
        [decimal]$UnionDues = 0, [decimal]$OwnHealth = 0, [int]$NonJoint = 0,
        [decimal]$ChildCare = 0, $Premium = "none", [decimal]$Overnights = 0,
        [decimal]$Ssva = 0, [bool]$Exception = $false)
    @{
        monthlyIncome = $Income; spousalSupportReceived = $SpousalReceived; spousalSupportPaid = $SpousalPaid
        unionDues = $UnionDues; ownHealthInsuranceCost = $OwnHealth; nonJointChildren = $NonJoint
        childCareCosts = $ChildCare; childrensHealthCoverageCost = $(if ($Premium -eq "none") { $null } else { [decimal]$Premium })
        averageOvernights = $Overnights; socialSecurityVeteransBenefits = $Ssva; minimumOrderException = $Exception
    }
}

# The scenario matrix. coverageSelection must be a line-4d-legal option for the case
# ("Plaintiff", "Defendant", "Both", "EitherWhenAvailable").
$cases = @(
    @{ name = "sole-custody-payer-premium"
       p = New-Parent -Income 4500 -Premium 250 -Overnights 91
       d = New-Parent -Income 3200 -Overnights 274
       minors = 2; cas = 0; cm = "n"; select = "Plaintiff" }
    @{ name = "eow-payer-custodial-childcare"
       p = New-Parent -Income 5200 -Premium 180 -Overnights 80
       d = New-Parent -Income 3100 -ChildCare 400 -Overnights 285
       minors = 1; cas = 0; cm = "n"; select = "Plaintiff" }
    @{ name = "equal-5050-tie"
       p = New-Parent -Income 4000 -Overnights 182.5
       d = New-Parent -Income 4000 -Overnights 182.5
       minors = 2; cas = 0; cm = "n"; select = "EitherWhenAvailable" }
    @{ name = "low-income-minimum-order"
       p = New-Parent -Income 1800 -Overnights 60
       d = New-Parent -Income 2500 -Overnights 305
       minors = 1; cas = 0; cm = "n"; select = "EitherWhenAvailable" }
    @{ name = "minwage-free-coverage-cash-medical"
       p = New-Parent -Income 2800 -Premium 0 -Overnights 120
       d = New-Parent -Income 6000 -Overnights 245
       minors = 2; cas = 0; cm = "y"; select = "Plaintiff" }
    @{ name = "cas-mixed-family"
       p = New-Parent -Income 5500 -Overnights 100
       d = New-Parent -Income 4200 -Premium 300 -Overnights 265
       minors = 1; cas = 1; cm = "n"; select = "Defendant" }
    @{ name = "cas-only-both-pay"
       p = New-Parent -Income 4700
       d = New-Parent -Income 3900
       minors = 0; cas = 2; cm = "n"; select = "EitherWhenAvailable" }
    @{ name = "non-joint-children-deduction"
       p = New-Parent -Income 6500 -NonJoint 2 -Overnights 120
       d = New-Parent -Income 3800 -NonJoint 1 -Overnights 245
       minors = 2; cas = 0; cm = "n"; select = "EitherWhenAvailable" }
    @{ name = "high-income-scale-cap"
       p = New-Parent -Income 20000 -Premium 450 -Overnights 150
       d = New-Parent -Income 15000 -Overnights 215
       minors = 3; cas = 0; cm = "n"; select = "Plaintiff" }
    @{ name = "ssva-offset"
       p = New-Parent -Income 4200 -Overnights 150 -Ssva 400
       d = New-Parent -Income 3000 -Overnights 215
       minors = 1; cas = 0; cm = "n"; select = "EitherWhenAvailable" }
    @{ name = "spousal-support-and-dues"
       p = New-Parent -Income 5000 -SpousalPaid 600 -UnionDues 75 -OwnHealth 200 -Overnights 110
       d = New-Parent -Income 4000 -SpousalReceived 600 -OwnHealth 150 -Overnights 255
       minors = 2; cas = 0; cm = "n"; select = "EitherWhenAvailable" }
    @{ name = "both-provide-childcare-both"
       p = New-Parent -Income 5800 -ChildCare 300 -Premium 150 -Overnights 100
       d = New-Parent -Income 4600 -ChildCare 500 -Premium 220 -Overnights 265
       minors = 2; cas = 0; cm = "n"; select = "Both" }
    @{ name = "contingent-cash-medical"
       p = New-Parent -Income 4800 -Premium 190 -Overnights 91
       d = New-Parent -Income 3600 -Overnights 274
       minors = 1; cas = 0; cm = "c"; select = "Plaintiff" }
    @{ name = "minimum-order-exception"
       p = New-Parent -Income 1900 -Overnights 30 -Exception $true
       d = New-Parent -Income 2400 -Overnights 335
       minors = 1; cas = 0; cm = "n"; select = "EitherWhenAvailable" }
    @{ name = "cent-precision-inputs"
       p = New-Parent -Income 4321.55 -UnionDues 45.5 -Overnights 132
       d = New-Parent -Income 3789.33 -ChildCare 333.33 -Overnights 233
       minors = 2; cas = 0; cm = "n"; select = "EitherWhenAvailable" }
)

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $wb = $excel.Workbooks.Open($WorkbookPath, 0, $true)
    $ws = $wb.Worksheets.Item("child support worksheet")

    # Several input cells are merged ranges: writing to the anchor address works directly, but
    # clearing must go through the whole merge area. Values go through the Formula setter as
    # invariant strings - the PowerShell COM adapter mis-binds Value2's variant type, and Formula
    # parses the entry exactly as if typed into the cell (numbers as numbers, "none" as text).
    function Set-Cell([string]$address, $value) {
        $ws.Range($address).Formula = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0}", $value)
    }
    function Clear-Cell([string]$address) { [void]$ws.Range($address).MergeArea.ClearContents() }
    function Read-Number([string]$address) {
        $v = $ws.Range($address).Value2
        if ($null -eq $v -or $v -isnot [double]) { return $null }
        return [math]::Round([decimal]$v, 6)
    }

    $jsonCases = foreach ($case in $cases) {
        # Reset every input, then set the case.
        foreach ($cell in $inputCells.Values) { Clear-Cell $cell }
        Set-Cell $inputCells.pname1 "Plaintiff"; Set-Cell $inputCells.pname2 "Defendant"
        Set-Cell $inputCells.compelling "No"; Set-Cell $inputCells.cmelection $case.cm
        Set-Cell $inputCells.exception1 $(if ($case.p.minimumOrderException) { "Yes" } else { "No" })
        Set-Cell $inputCells.exception2 $(if ($case.d.minimumOrderException) { "Yes" } else { "No" })

        foreach ($side in @(@($case.p, "1"), @($case.d, "2"))) {
            $parent = $side[0]; $n = $side[1]
            Set-Cell $inputCells["income$n"] ([double]$parent.monthlyIncome)
            Set-Cell $inputCells["spousalplus$n"] ([double]$parent.spousalSupportReceived)
            Set-Cell $inputCells["spousalminus$n"] ([double]$parent.spousalSupportPaid)
            Set-Cell $inputCells["uniondues$n"] ([double]$parent.unionDues)
            Set-Cell $inputCells["parentpremium$n"] ([double]$parent.ownHealthInsuranceCost)
            Set-Cell $inputCells["addch$n"] ([double]$parent.nonJointChildren)
            Set-Cell $inputCells["ccoop$n"] ([double]$parent.childCareCosts)
            if ($null -eq $parent.childrensHealthCoverageCost) { Set-Cell $inputCells["premium$n"] "none" }
            else { Set-Cell $inputCells["premium$n"] ([double]$parent.childrensHealthCoverageCost) }
            Set-Cell $inputCells["ptime$n"] ([double]$parent.averageOvernights)
            Set-Cell $inputCells["ssava$n"] ([double]$parent.socialSecurityVeteransBenefits)
        }

        Set-Cell $inputCells.jointminorch ([double]$case.minors)
        Set-Cell $inputCells.jointCAS ([double]$case.cas)

        $providerText = switch ($case.select) {
            "Plaintiff" { "Plaintiff" }
            "Defendant" { "Defendant" }
            "Both" { "Plaintiff and Defendant" }
            "EitherWhenAvailable" { "Either parent when available" }
        }
        Set-Cell $inputCells.whoprovides $providerText

        [void]$excel.CalculateFullRebuild()

        $expectedLines = [ordered]@{}
        foreach ($id in $lineCells.Keys) {
            $map = $lineCells[$id]
            $expectedLines[$id] = [ordered]@{
                plaintiff = $(if ($map.p) { Read-Number $map.p } else { $null })
                defendant = $(if ($map.d) { Read-Number $map.d } else { $null })
                combined  = $(if ($map.c) { Read-Number $map.c } else { $null })
            }
        }

        $pays1 = "$($ws.Range('D77').Value2)"; $pays2 = "$($ws.Range('E77').Value2)"

        [ordered]@{
            name = $case.name
            input = [ordered]@{
                plaintiff = $case.p; defendant = $case.d
                jointMinorChildren = $case.minors; jointChildrenAttendingSchool = $case.cas
                cashMedical = switch ($case.cm) { "y" { "Yes" } "n" { "No" } "c" { "Contingent" } }
                coverageSelection = $case.select
                orderCoverageAtHigherAmount = $false
            }
            expectedPaysForMinors = $(if ($pays1 -eq "Yes") { "Plaintiff" } elseif ($pays2 -eq "Yes") { "Defendant" } else { $null })
            expectedLines = $expectedLines
        }

        Write-Host "generated: $($case.name)"
    }

    $fixture = [ordered]@{
        source = "child_support_worksheet.xls (justice.oregon.gov, saved 6/30/2026, rules eff. 7/1/2026)"
        form = "worksheet"
        cases = @($jsonCases)
    }

    $json = $fixture | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.UTF8Encoding]::new($false))
    Write-Host "wrote $OutputPath"

    $wb.Close($false)
}
finally {
    $excel.Quit()
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}
