<#
.SYNOPSIS
    Regenerates al-cs42-golden.json and al-cs42s-golden.json from the official Alabama AOC workbooks via Excel COM.

.DESCRIPTION
    Every expected value in the golden fixtures is read back from the state's own spreadsheet after typing the case's
    inputs into its unlocked input cells - the workbook, not this script, does the arithmetic. Requires desktop Excel.

    The workbooks are the "Excel" downloads that accompany the eforms.alacourt.gov PDFs:
      - form-cs-42-child-support-worksheet-in-excel-05-25-22.xlsx   (Form CS-42, Rev. 5/2022)
      - form-cs-42-s-child-support-worksheet-in-excel-3-9-23.xlsx   (Form CS-42-S, Eff. 6/2023)

.EXAMPLE
    .\Generate-GoldenCases.ps1 -Cs42Workbook ~\Downloads\form-cs-42-child-support-worksheet-in-excel-05-25-22.xlsx `
                               -Cs42SWorkbook ~\Downloads\form-cs-42-s-child-support-worksheet-in-excel-3-9-23.xlsx
#>
param(
    [Parameter(Mandatory)] [string] $Cs42Workbook,
    [Parameter(Mandatory)] [string] $Cs42SWorkbook,
    [string] $OutputDirectory = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

function New-Parent {
    param([int] $Gross, [int] $ChildSupport = 0, [int] $Alimony = 0, [int] $Childcare = 0, [int] $Healthcare = 0, [bool] $Primary = $false)
    [ordered]@{
        hasPrimaryCustody       = $Primary
        monthlyGrossIncome      = $Gross
        preexistingChildSupport = $ChildSupport
        preexistingAlimony      = $Alimony
        workRelatedChildcareCosts = $Childcare
        healthcareCoverageCosts = $Healthcare
    }
}

function New-Case {
    param([string] $Name, [int] $Children, $Plaintiff, $Defendant)
    [ordered]@{ name = $Name; numberOfChildren = $Children; plaintiff = $Plaintiff; defendant = $Defendant }
}

# Same shape of scenarios for both forms so the two fixtures stay comparable.
$cs42Cases = @(
    (New-Case 'workbook-defaults'        1 (New-Parent 1200 -Healthcare 100 -Primary $true) (New-Parent 1000 -Childcare 20))
    (New-Case 'repo-regression-4000-3000' 2 (New-Parent 4000 -Childcare 200 -Healthcare 100 -Primary $true) (New-Parent 3000))
    (New-Case 'api-docs-example-4200-5100' 2 (New-Parent 4200 -Childcare 400 -Healthcare 250 -Primary $true) (New-Parent 5100))
    (New-Case 'defendant-primary-plaintiff-pays' 2 (New-Parent 6000) (New-Parent 2500 -Healthcare 120 -Primary $true))
    (New-Case 'both-below-self-support-reserve' 1 (New-Parent 900 -Primary $true) (New-Parent 800))
    (New-Case 'zero-income-payer'        2 (New-Parent 3500 -Primary $true) (New-Parent 0))
    (New-Case 'preexisting-support-and-alimony' 3 (New-Parent 5200 -ChildSupport 400 -Alimony 300 -Healthcare 150 -Primary $true) (New-Parent 2600 -ChildSupport 200 -Childcare 350))
    (New-Case 'bracket-boundary-2224'    1 (New-Parent 1224 -Primary $true) (New-Parent 1000))
    (New-Case 'bracket-boundary-2225'    1 (New-Parent 1225 -Primary $true) (New-Parent 1000))
    (New-Case 'negative-combined-adjusted-income' 1 (New-Parent 500 -ChildSupport 800) (New-Parent 200 -Primary $true))
    (New-Case 'share-midpoint-125-875'   2 (New-Parent 1250 -Primary $true) (New-Parent 8750))
    (New-Case 'equal-shares-odd-total'   1 (New-Parent 3000 -Childcare 1 -Primary $true) (New-Parent 3000))
    (New-Case 'six-children'             6 (New-Parent 2500 -Healthcare 300 -Primary $true) (New-Parent 4200 -Childcare 500))
    (New-Case 'high-income-29000'        4 (New-Parent 15000 -Primary $true) (New-Parent 14000))
    (New-Case 'schedule-ceiling-30000'   6 (New-Parent 20000) (New-Parent 10000 -Primary $true))
)

$cs42sCases = @(
    (New-Case 'workbook-defaults'        1 (New-Parent 1200 -Healthcare 100) (New-Parent 1000 -Childcare 20))
    (New-Case 'repo-regression-4244-9173' 4 (New-Parent 4244) (New-Parent 9173 -Healthcare 195))
    (New-Case 'identical-parents-tie'    2 (New-Parent 5000 -Healthcare 100) (New-Parent 5000 -Healthcare 100))
    (New-Case 'plaintiff-pays'           2 (New-Parent 7000 -Childcare 300) (New-Parent 3000 -Healthcare 100))
    (New-Case 'near-tie'                 1 (New-Parent 3001) (New-Parent 3000))
    (New-Case 'both-low-income'          1 (New-Parent 800) (New-Parent 700))
    (New-Case 'preexisting-support-and-alimony' 3 (New-Parent 5200 -ChildSupport 400 -Alimony 300 -Healthcare 150) (New-Parent 2600 -ChildSupport 200 -Childcare 350))
    (New-Case 'zero-income-plaintiff'    2 (New-Parent 0) (New-Parent 4000 -Childcare 200))
    (New-Case 'bracket-boundary-2225'    1 (New-Parent 1225) (New-Parent 1000))
    (New-Case 'negative-combined-adjusted-income' 1 (New-Parent 500 -ChildSupport 800) (New-Parent 200))
    (New-Case 'share-midpoint-175-825'   6 (New-Parent 2520) (New-Parent 12221 -ChildSupport 341))
    (New-Case 'plaintiff-costs-heavy'    3 (New-Parent 4000 -Childcare 900 -Healthcare 300) (New-Parent 6000))
    (New-Case 'six-children'             6 (New-Parent 2500 -Healthcare 300) (New-Parent 4200 -Childcare 500))
    (New-Case 'high-income-29000'        4 (New-Parent 15000) (New-Parent 14000 -Healthcare 400))
    (New-Case 'schedule-ceiling-30000'   6 (New-Parent 20000) (New-Parent 10000))
)

# Cell maps mirror WorksheetTemplates in FairShare.Api (kept in sync by hand - they describe the same two files).
$cs42Map = [ordered]@{
    sheet    = 'Form CS-42 Worksheet'
    children = 'K12'
    plaintiff = @{ gross = 'H14'; childSupport = 'H15'; alimony = 'H16'; childcare = 'H20'; healthcare = 'H21' }
    defendant = @{ gross = 'J14'; childSupport = 'J15'; alimony = 'J16'; childcare = 'J20'; healthcare = 'J21' }
    lines = [ordered]@{
        '1'  = @('H14', 'J14', 'L14'); '1a' = @('H15', 'J15', 'L15'); '1b' = @('H16', 'J16', 'L16'); '2' = @('H17', 'J17', 'L17')
        '3'  = @('H18', 'J18', 'L18'); '4'  = @($null, $null, 'L19'); '5'  = @('H20', 'J20', 'L20'); '6' = @('H21', 'J21', 'L21')
        '7'  = @($null, $null, 'L22'); '8'  = @('H23', 'J23', $null); '9'  = @('H24', 'J24', $null); '10' = @('H25', 'J25', $null)
        '11' = @('H27', 'J27', $null); '12' = @('H28', 'J28', $null); '13' = @('H30', 'J30', $null)
    }
    percentLines = @('3')
}

$cs42sMap = [ordered]@{
    sheet    = 'Form CS-42-S'
    children = 'K12'
    plaintiff = @{ gross = 'H14'; childSupport = 'H15'; alimony = 'H16'; childcare = 'H22'; healthcare = 'H23' }
    defendant = @{ gross = 'J14'; childSupport = 'J15'; alimony = 'J16'; childcare = 'J22'; healthcare = 'J23' }
    lines = [ordered]@{
        '1'  = @('H14', 'J14', 'L14'); '1a' = @('H15', 'J15', 'L15'); '1b' = @('H16', 'J16', 'L16'); '2' = @('H17', 'J17', 'L17')
        '3'  = @('H19', 'J19', 'L19'); '4'  = @($null, $null, 'L20'); '5'  = @($null, $null, 'L21'); '6' = @('H22', 'J22', $null)
        '7'  = @('H23', 'J23', $null); '8'  = @('H24', 'J24', 'L24'); '9'  = @($null, $null, 'L25'); '10' = @('H26', 'J26', $null)
        '11' = @('H28', 'J28', $null); '12' = @('H29', 'J29', $null); '13' = @('H30', 'J30', $null); '14' = @('H32', 'J32', $null)
    }
    percentLines = @('3')
}

function Read-Cell {
    param($Sheet, [string] $Address, [bool] $Percent)
    if (-not $Address) { return $null }
    $value = $Sheet.Range($Address).Value2
    if ($null -eq $value -or $value -is [string]) { return $null }   # CS-42-S line 14 leaves the losing column as ""
    if ($Percent) { return [math]::Round([double] $value, 2) }
    return [int][math]::Round([double] $value, 0)
}

function Invoke-Workbook {
    param([string] $Path, $Map, $Cases, [string] $FormKey, [string] $Source)

    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    try {
        $wb = $excel.Workbooks.Open((Resolve-Path $Path).Path, 0, $true)
        $ws = $wb.Worksheets.Item($Map.sheet)
        $results = @()

        foreach ($case in $Cases) {
            $ws.Range($Map.children).Value2 = $case.numberOfChildren
            foreach ($side in 'plaintiff', 'defendant') {
                $p = $case[$side]; $cells = $Map[$side]
                $ws.Range($cells.gross).Value2        = $p.monthlyGrossIncome
                $ws.Range($cells.childSupport).Value2 = $p.preexistingChildSupport
                $ws.Range($cells.alimony).Value2      = $p.preexistingAlimony
                $ws.Range($cells.childcare).Value2    = $p.workRelatedChildcareCosts
                $ws.Range($cells.healthcare).Value2   = $p.healthcareCoverageCosts
            }
            $excel.Calculate()

            $lines = [ordered]@{}
            foreach ($number in $Map.lines.Keys) {
                $addr = $Map.lines[$number]
                $isPercent = $Map.percentLines -contains $number
                $lines[$number] = [ordered]@{
                    plaintiff = Read-Cell $ws $addr[0] $isPercent
                    defendant = Read-Cell $ws $addr[1] $isPercent
                    combined  = Read-Cell $ws $addr[2] $isPercent
                }
            }

            if ($FormKey -eq 'CS42') {
                # The worksheet fills both columns; the order applies to the parent without primary custody.
                $payer  = if ($case.plaintiff.hasPrimaryCustody) { 'Defendant' } else { 'Plaintiff' }
                $amount = if ($payer -eq 'Defendant') { $lines['13'].defendant } else { $lines['13'].plaintiff }
            }
            else {
                # Line 14 puts the higher line-13 amount in the paying parent's column and leaves the other blank.
                if ($null -ne $lines['14'].plaintiff) { $payer = 'Plaintiff'; $amount = $lines['14'].plaintiff }
                else                                   { $payer = 'Defendant'; $amount = $lines['14'].defendant }
                if ($amount -le 0) { $payer = ''; $amount = 0 }   # no net transfer
            }

            $results += [ordered]@{
                name             = $case.name
                numberOfChildren = $case.numberOfChildren
                plaintiff        = $case.plaintiff
                defendant        = $case.defendant
                expectedPayer    = $payer
                expectedAmount   = $amount
                expectedLines    = $lines
            }
            Write-Host ("{0,-40} {1,-10} {2,6}" -f $case.name, $payer, $amount)
        }

        $wb.Close($false)
        return [ordered]@{
            source    = $Source
            generated = 'Generate-GoldenCases.ps1 (Excel COM); values are read back from the workbook, not computed here'
            form      = $FormKey
            cases     = $results
        }
    }
    finally {
        $excel.Quit()
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    }
}

$cs42 = Invoke-Workbook -Path $Cs42Workbook -Map $cs42Map -Cases $cs42Cases -FormKey 'CS42' `
    -Source 'Alabama AOC Form CS-42 (Rev. 5/2022) Excel worksheet, form-cs-42-child-support-worksheet-in-excel-05-25-22.xlsx'
$cs42s = Invoke-Workbook -Path $Cs42SWorkbook -Map $cs42sMap -Cases $cs42sCases -FormKey 'CS42S' `
    -Source 'Alabama AOC Form CS-42-S (Eff. 6/2023) Excel worksheet, form-cs-42-s-child-support-worksheet-in-excel-3-9-23.xlsx'

$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'al-cs42-golden.json'),  ($cs42  | ConvertTo-Json -Depth 8), $utf8)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'al-cs42s-golden.json'), ($cs42s | ConvertTo-Json -Depth 8), $utf8)
Write-Host "Wrote al-cs42-golden.json and al-cs42s-golden.json to $OutputDirectory"
