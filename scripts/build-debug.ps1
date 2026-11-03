[CmdletBinding()]
param(
    [ValidateSet('Pro', 'FinOffice')]
    [string[]]$ProductLine = @('Pro', 'FinOffice'),
    [switch]$RequireCleanDependencies,
    [switch]$UiAudit
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repositoryRoot 'WawiCallProSolution.sln'
$solutionText = Get-Content -LiteralPath $solutionPath -Raw
foreach ($folderName in @('Docs', 'Scripts', 'Solution Files', 'src', 'Tools')) {
    if (-not $solutionText.Contains("= `"$folderName`", `"$folderName`"")) {
        throw "Solution-Ordner fehlt: $folderName"
    }
}
$artifactDirectory = Join-Path $repositoryRoot '.artifacts\build'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
& (Join-Path $repositoryRoot 'bootstrap.ps1') -CheckOnly -RequireClean:$RequireCleanDependencies

$solutionProjects = @(Get-Content -LiteralPath $solutionPath | ForEach-Object {
    if ($_ -match '^Project\(.*?, "([^"\r\n]+\.csproj)",') { $Matches[1] }
})
if ($solutionProjects.Count -eq 0) { throw 'Keine Projekte in der Solution gefunden.' }

foreach ($selectedProductLine in $ProductLine) {
    $excludedPattern = if ($selectedProductLine -eq 'Pro') {
        'Quantarix\.Pcfk\.|sqlite-netFx-source|WawiCallPro\.Plugins\.Lexware\.FinOffice'
    } else {
        'Quantarix\.Lexware\.|WawiCallPro\.Plugins\.Lexware\.Pro'
    }
    $selectedProjects = @($solutionProjects | Where-Object { $_ -notmatch $excludedPattern })
    $filterPath = Join-Path $artifactDirectory "WawiCallPro.$selectedProductLine.slnf"
    @{ solution = @{ path = $solutionPath; projects = $selectedProjects } } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $filterPath -Encoding utf8
    & dotnet build $filterPath -c Debug "-p:LexwareProductLine=$selectedProductLine" --nologo --verbosity minimal |
        Tee-Object -FilePath (Join-Path $artifactDirectory "Debug-$selectedProductLine.log")
    if ($LASTEXITCODE -ne 0) { throw "Debug-Build fehlgeschlagen: $selectedProductLine" }

    foreach ($hostName in @('App', 'Console')) {
        $hostDirectory = Join-Path $repositoryRoot "WawiCallPro.$hostName\bin\Debug\$selectedProductLine\net10.0-windows"
        $diagnosticText = & dotnet (Join-Path $hostDirectory "WawiCallPro.$hostName.$selectedProductLine.dll") --diagnostics
        if ($LASTEXITCODE -ne 0) { throw "Hoststart fehlgeschlagen: $hostName/$selectedProductLine" }
        $diagnostic = $diagnosticText | ConvertFrom-Json
        if ($diagnostic.Application -ne 'WawiCallPro' -or $diagnostic.ProductLine -ne $selectedProductLine) {
            throw "Falsche Laufzeitidentität: $hostName/$selectedProductLine"
        }
        Write-Output "$hostName/$selectedProductLine : $diagnosticText"
    }

    $outputDirectories = @(
        "WawiCallPro.App\bin\Debug\$selectedProductLine\net10.0-windows",
        "WawiCallPro.Console\bin\Debug\$selectedProductLine\net10.0-windows",
        "WawiCallPro.Plugins.Lexware.$selectedProductLine\bin\Debug\$selectedProductLine\net10.0-windows"
    )
    $forbiddenPrefix = if ($selectedProductLine -eq 'Pro') { 'Quantarix.Pcfk.' } else { 'Quantarix.Lexware.' }
    foreach ($relativeDirectory in $outputDirectories) {
        $outputDirectory = Join-Path $repositoryRoot $relativeDirectory
        $outputFiles = @(Get-ChildItem -LiteralPath $outputDirectory -Recurse -File)
        if ($outputFiles | Where-Object { $_.Name -like 'MMS.*.dll' -or $_.Name.StartsWith($forbiddenPrefix) }) {
            throw "Unzulässige Assembly in $outputDirectory"
        }
        foreach ($dependencyGraph in ($outputFiles | Where-Object { $_.Name -like '*.deps.json' })) {
            $graph = Get-Content -LiteralPath $dependencyGraph.FullName -Raw
            if ($graph -match '"MMS\.' -or $graph.Contains($forbiddenPrefix)) {
                throw "Unzulässige Runtime-Abhängigkeit in $($dependencyGraph.FullName)"
            }
        }
    }

    if ($UiAudit) {
        $auditDirectory = Join-Path $repositoryRoot ".artifacts\ui\$selectedProductLine"
        $appDll = Join-Path $repositoryRoot "WawiCallPro.App\bin\Debug\$selectedProductLine\net10.0-windows\WawiCallPro.App.$selectedProductLine.dll"
        $previousSurfaces = $env:LX_UI_AUDIT_SURFACES
        try {
            $env:LX_UI_AUDIT_SURFACES = 'MainForm'
            & dotnet $appDll --ui-audit $auditDirectory
            if ($LASTEXITCODE -ne 0) { throw "UI-Renderprüfung fehlgeschlagen: $selectedProductLine" }
        } finally {
            $env:LX_UI_AUDIT_SURFACES = $previousSurfaces
        }
    }
    Write-Output "Debug-Build, Hoststarts und Abhängigkeitsprüfung für $selectedProductLine erfolgreich."
}
