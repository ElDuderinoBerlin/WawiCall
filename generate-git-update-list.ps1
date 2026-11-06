<#
.SYNOPSIS
Erzeugt eine kompakte Markdown-Uebersicht der Git-Aenderungen eines Kalenderjahres.

.EXAMPLE
.\generate-git-update-list.ps1

.EXAMPLE
.\generate-git-update-list.ps1 -Year 2025 -OutputPath .\Git-Update-2025.md

.EXAMPLE
.\generate-git-update-list.ps1 -IncludeMaintenance

.EXAMPLE
.\generate-git-update-list.ps1 -UseAzureTranslator -AzureTranslatorRegion germanywestcentral
#>

[CmdletBinding()]
param(
    [ValidateRange(2000, 2100)]
    [int]$Year = (Get-Date).Year,

    [string]$OutputPath,

    [switch]$IncludeDate,

    [switch]$IncludeMaintenance,

    [switch]$UseAzureTranslator,

    [string]$AzureTranslatorEndpoint = 'https://api.cognitive.microsofttranslator.com/',

    [string]$AzureTranslatorRegion = $env:AZURE_TRANSLATOR_REGION,

    [string]$DocumentTitle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$solutionRoot = $PSScriptRoot

if ( [string]::IsNullOrWhiteSpace($OutputPath))
{
    $OutputPath = Join-Path $solutionRoot "Git-Update-$Year.md"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath))
{
    $OutputPath = Join-Path $solutionRoot $OutputPath
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$startDate = '{0}-01-01' -f $Year
$endDate = '{0}-01-01' -f ($Year + 1)
$fieldSeparator = [char]31

if ( [string]::IsNullOrWhiteSpace($DocumentTitle))
{
    $DocumentTitle = "Git-Update $Year"
}
else
{
    $DocumentTitle = $DocumentTitle.Replace(
            '{YEAR}',
            $Year.ToString([Globalization.CultureInfo]::InvariantCulture)
    )
}

function Test-IsMaintenanceCommit
{
    param([string]$Subject)

    if ($IncludeMaintenance)
    {
        return $false
    }

    $maintenancePatterns = @(
        '(?i)^style(?:\([^)]+\))?:',
        '(?i)^chore\((?:assets|branding)\):',
        '(?i)\bnormalize(?:d)? (?:line endings?|formatting)\b',
        '(?i)\bline endings?\b',
        '(?i)\bupdate (?:application )?icon assets?\b',
        '(?i)\bupdate screenshots?\b',
        '(?i)^wip\b'
    )

    foreach ($pattern in $maintenancePatterns)
    {
        if ($Subject -match $pattern)
        {
            return $true
        }
    }

    return $false
}

function Get-ChangeCategory
{
    param([string]$Subject)

    switch -Regex ($Subject)
    {
        '(?i)^(feat(?:\([^)]+\))?:|add(?:ed)?\b|new\b)' {
            return 'Neue Funktionen'
        }
        '(?i)^(fix(?:\([^)]+\))?:|bug(?:fix(?:es)?)?\b|fixed\b|.*\bbug fixes?\b)' {
            return 'Fehlerbehebungen'
        }
        '(?i)^docs?(?:\([^)]+\))?:' {
            return 'Dokumentation'
        }
        '(?i)^tests?(?:\([^)]+\))?:' {
            return 'Tests und Qualität'
        }
        '(?i)^(perf|refactor|build|ci|release|deploy|config|chore)(?:\([^)]+\))?:' {
            return 'Technik und Betrieb'
        }
        default {
            return 'Verbesserungen'
        }
    }
}

function Get-CleanTitle
{
    param([string]$Subject)

    $title = $Subject `
         -replace '(?i)^(feat|fix|perf|refactor|build|docs?|tests?|ci|release|deploy|config|chore|style)(\([^)]+\))?!?:\s*', '' `
         -replace '^[\s\-\*\u2022]+', ''

    $title = $title.Trim()
    if ( [string]::IsNullOrWhiteSpace($title))
    {
        return $Subject.Trim()
    }

    return $title
}

function Get-CompactGermanTitle
{
    param(
        [string]$Subject,
        [string]$Category
    )

    switch -Regex ($Subject)
    {
        '(?i)transfer batch|batch configuration|transfer batches' {
            return 'Stapelverarbeitung für Artikelübertragungen konfigurierbar gemacht'
        }
        '(?i)article name|name sanitizer' {
            return 'Bereinigung und Verarbeitung von Lexware-Artikelnamen verbessert'
        }
        '(?i)language auto.?detect|language detect' {
            return 'Automatische Spracherkennung erweitert'
        }
        '(?i)document extraction|document text' {
            return 'Texterkennung aus Dokumenten für die Übersetzungsprüfung ergänzt'
        }
        '(?i)print.?form.*backup|registration backup|lxreports|layout\.ini|lxtarget' {
            return 'Sicherung geänderter Lexware-Druckformulardaten verbessert'
        }
        '(?i)print.?form.*pretranslation|static text pretranslation' {
            return 'Vorübersetzung statischer Druckformulartexte ergänzt'
        }
        '(?i)print.?form.*package|form package|formularpaket' {
            return 'Verwaltung und Export von Druckformularpaketen verbessert'
        }
        '(?i)print.?form support|docs\(print-forms\)|supported print' {
            return 'Unterstützung und Dokumentation der Lexware-Druckformulare präzisiert'
        }
        '(?i)word.*foreground|word window|topmost' {
            return 'Erzeugte Word-Dokumente zuverlässiger in den Vordergrund gebracht'
        }
        '(?i)word style|styles in lexware print|gray contrast' {
            return 'Konfigurierbare Formatvorlagen für Word-Dokumente erweitert'
        }
        '(?i)column size|openxml|document layout' {
            return 'Spaltenberechnung und Layout erzeugter Word-Dokumente korrigiert'
        }
        '(?i)language selection cancellation|language cancellation' {
            return 'Abbruch der Lexware-Sprachauswahl zuverlässig gespeichert'
        }
        '(?i)foreground owner|foreground window|translation windows' {
            return 'Übersetzungsfenster zuverlässig am Lexware-Fenster verankert'
        }
        '(?i)timer|wait form' {
            return 'Zeitsteuerung und Übersetzungs-Wartefenster stabilisiert'
        }
        '(?i)crash reporting' {
            return 'Automatische Erfassung und Anzeige schwerwiegender Programmfehler ergänzt'
        }
        '(?i)thread.?safe|disposed|safe ui' {
            return 'UI-Thread-Aufrufe gegen bereits geschlossene Fenster abgesichert'
        }
        '(?i)window layout|layout bounds|form save and restore' {
            return 'Speicherung und Wiederherstellung von Fensterpositionen verbessert'
        }
        '(?i)license reset' {
            return 'Zurücksetzen der Anwendungslizenz ergänzt'
        }
        '(?i)background license|offline license' {
            return 'Hintergrund- und Offline-Lizenzprüfung erweitert'
        }
        '(?i)velopack|release upload|release process' {
            return 'Velopack-Paketierung und Release-Upload automatisiert'
        }
        '(?i)nuget|package update' {
            return 'Aktualisierung von NuGet-Abhängigkeiten automatisiert'
        }
        '(?i)start page' {
            return 'Konfigurierbare Startseite ergänzt'
        }
        '(?i)skin|theme|theming' {
            return 'Theme- und Skin-Verhalten vereinheitlicht'
        }
        '(?i)server logging|proxy' {
            return 'Serverprotokollierung und API-Proxy-Konfiguration verbessert'
        }
        '(?i)no[- ]?translate|translation exception|nicht.?übersetz' {
            return 'Import und Export der Nicht-übersetzen-Begriffe verbessert'
        }
        '(?i)article[- ]?transfer|article translation|transfer batch|article name' {
            return 'Artikelübersetzungen zwischen Lexware-Firmen verbessert'
        }
        '(?i)rückübersetz|rueckuebersetz|back.?translation|translation check|language auto.?detect' {
            return 'Rückübersetzungsprüfung und automatische Spracherkennung erweitert'
        }
        '(?i)print.?form|druckformular|formularpaket|lxreports|layout\.ini|lxtarget' {
            return 'Druckformular-Verwaltung und zugehörige Datensicherung verbessert'
        }
        '(?i)\bword\b|wordgenerator|openxml|column size|document layout' {
            return 'Word-Export und Dokumentlayout verbessert'
        }
        '(?i)language selection|foreground owner|translation window|wait form|timer' {
            return 'Sprachauswahl und Fenstersteuerung stabilisiert'
        }
        '(?i)crash|fatal|unhandled|thread.?safe|disposed|exception handler' {
            return 'Fehlerbehandlung und Schutz vor UI-Abstürzen verbessert'
        }
        '(?i)licen[cs]e|lizenz' {
            return 'Lizenzprüfung und Lizenzverwaltung erweitert'
        }
        '(?i)velopack|release|nuget|package update|build' {
            return 'Build-, Paket- und Release-Prozess verbessert'
        }
        '(?i)docs?|readme|documentation|edition|handbuch' {
            return 'Benutzer- und Entwicklerdokumentation aktualisiert'
        }
        '(?i)skin|theme|theming|window layout|start page|\bui\b|icon' {
            return 'Benutzeroberfläche, Themes und Fensterverhalten verbessert'
        }
        '(?i)\bapi\b|proxy|server logging|configuration|\bconfig\b' {
            return 'Konfiguration und API-Kommunikation verbessert'
        }
        '(?i)\btests?\b|test coverage' {
            return 'Automatisierte Testabdeckung erweitert'
        }
    }

    switch ($Category)
    {
        'Neue Funktionen' {
            return 'Weitere Funktionen ergänzt'
        }
        'Fehlerbehebungen' {
            return 'Allgemeine Fehlerbehebungen umgesetzt'
        }
        'Dokumentation' {
            return 'Benutzer- und Entwicklerdokumentation aktualisiert'
        }
        'Tests und Qualität' {
            return 'Automatisierte Testabdeckung erweitert'
        }
        'Technik und Betrieb' {
            return 'Technische Grundlagen und Wartbarkeit verbessert'
        }
        default {
            return 'Weitere funktionale und technische Verbesserungen umgesetzt'
        }
    }
}

function Get-CompactEnglishTitle
{
    param(
        [string]$Subject,
        [string]$Category
    )

    switch -Regex ($Subject)
    {
        '(?i)transfer batch|batch configuration|transfer batches' {
            return 'Made batch processing for article transfers configurable'
        }
        '(?i)article name|name sanitizer' {
            return 'Improved sanitization and processing of Lexware article names'
        }
        '(?i)language auto.?detect|language detect' {
            return 'Extended automatic language detection'
        }
        '(?i)document extraction|document text' {
            return 'Added document text extraction for translation checks'
        }
        '(?i)print.?form.*backup|registration backup|lxreports|layout\.ini|lxtarget' {
            return 'Improved backups of modified Lexware print form data'
        }
        '(?i)print.?form.*pretranslation|static text pretranslation' {
            return 'Added pretranslation of static print form text'
        }
        '(?i)print.?form.*package|form package|formularpaket' {
            return 'Improved management and export of print form packages'
        }
        '(?i)print.?form support|docs\(print-forms\)|supported print' {
            return 'Clarified support and documentation for Lexware print forms'
        }
        '(?i)word.*foreground|word window|topmost' {
            return 'Improved foreground activation of generated Word documents'
        }
        '(?i)word style|styles in lexware print|gray contrast' {
            return 'Extended configurable styles for generated Word documents'
        }
        '(?i)column size|openxml|document layout' {
            return 'Fixed column calculation and layout of generated Word documents'
        }
        '(?i)language selection cancellation|language cancellation' {
            return 'Reliably persisted cancellation of Lexware language selection'
        }
        '(?i)foreground owner|foreground window|translation windows' {
            return 'Reliably anchored translation windows to the Lexware window'
        }
        '(?i)timer|wait form' {
            return 'Stabilized timing and translation wait windows'
        }
        '(?i)crash reporting' {
            return 'Added automatic reporting and display of fatal application errors'
        }
        '(?i)thread.?safe|disposed|safe ui' {
            return 'Protected UI thread calls against already closed windows'
        }
        '(?i)window layout|layout bounds|form save and restore' {
            return 'Improved saving and restoration of window positions'
        }
        '(?i)license reset' {
            return 'Added application license reset'
        }
        '(?i)background license|offline license' {
            return 'Extended background and offline license validation'
        }
        '(?i)velopack|release upload|release process' {
            return 'Automated Velopack packaging and release uploads'
        }
        '(?i)nuget|package update' {
            return 'Automated updates of NuGet dependencies'
        }
        '(?i)start page' {
            return 'Added a configurable start page'
        }
        '(?i)skin|theme|theming' {
            return 'Unified theme and skin behavior'
        }
        '(?i)server logging|proxy' {
            return 'Improved server logging and API proxy configuration'
        }
        '(?i)no[- ]?translate|translation exception|nicht.?übersetz' {
            return 'Improved import and export of no-translate terms'
        }
        '(?i)article[- ]?transfer|article translation|transfer batch|article name' {
            return 'Improved article translations between Lexware companies'
        }
        '(?i)rückübersetz|rueckuebersetz|back.?translation|translation check|language auto.?detect' {
            return 'Extended back-translation checks and automatic language detection'
        }
        '(?i)print.?form|druckformular|formularpaket|lxreports|layout\.ini|lxtarget' {
            return 'Improved print form management and related data backups'
        }
        '(?i)\bword\b|wordgenerator|openxml|column size|document layout' {
            return 'Improved Word export and document layout'
        }
        '(?i)language selection|foreground owner|translation window|wait form|timer' {
            return 'Stabilized language selection and window management'
        }
        '(?i)crash|fatal|unhandled|thread.?safe|disposed|exception handler' {
            return 'Improved error handling and protection against UI crashes'
        }
        '(?i)licen[cs]e|lizenz' {
            return 'Extended license validation and license management'
        }
        '(?i)velopack|release|nuget|package update|build' {
            return 'Improved build, packaging, and release process'
        }
        '(?i)docs?|readme|documentation|edition|handbuch' {
            return 'Updated user and developer documentation'
        }
        '(?i)skin|theme|theming|window layout|start page|\bui\b|icon' {
            return 'Improved user interface, themes, and window behavior'
        }
        '(?i)\bapi\b|proxy|server logging|configuration|\bconfig\b' {
            return 'Improved configuration and API communication'
        }
        '(?i)\btests?\b|test coverage' {
            return 'Extended automated test coverage'
        }
    }

    switch ($Category)
    {
        'Neue Funktionen' {
            return 'Added further functionality'
        }
        'Fehlerbehebungen' {
            return 'Implemented general bug fixes'
        }
        'Dokumentation' {
            return 'Updated user and developer documentation'
        }
        'Tests und Qualität' {
            return 'Extended automated test coverage'
        }
        'Technik und Betrieb' {
            return 'Improved technical foundations and maintainability'
        }
        default {
            return 'Implemented further functional and technical improvements'
        }
    }
}

function Get-AzureTranslatorKey
{
    if (-not [string]::IsNullOrWhiteSpace($env:AZURE_TRANSLATOR_KEY))
    {
        return $env:AZURE_TRANSLATOR_KEY
    }

    $secureKey = Read-Host 'Azure Translator Key' -AsSecureString
    $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    try
    {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    }
    finally
    {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
    }
}

function Invoke-AzureTitleTranslation
{
    param(
        [string[]]$Texts,
        [string]$Endpoint,
        [string]$Region
    )

    if ($Texts.Count -eq 0)
    {
        return @()
    }

    $key = Get-AzureTranslatorKey
    if ( [string]::IsNullOrWhiteSpace($key))
    {
        throw 'Es wurde kein Azure-Translator-Key angegeben.'
    }

    $headers = @{
        'Ocp-Apim-Subscription-Key' = $key
    }
    if (-not [string]::IsNullOrWhiteSpace($Region))
    {
        $headers['Ocp-Apim-Subscription-Region'] = $Region
    }

    $uri = "$($Endpoint.TrimEnd('/') )/translate?api-version=3.0&to=de"
    $translatedTexts = [System.Collections.Generic.List[string]]::new()
    $spinnerFrames = @('|', '/', '-', '\')
    $currentText = 0
    $progressActivity = 'Git-Update wird erstellt'

    try
    {
        foreach ($text in $Texts)
        {
            $currentText++
            $spinnerFrame = $spinnerFrames[($currentText - 1) % $spinnerFrames.Count]
            $percentComplete = [math]::Floor((($currentText - 1) / $Texts.Count) * 100)
            Write-Progress `
                -Activity $progressActivity `
                -Status "$spinnerFrame Übersetze Thema $currentText von $( $Texts.Count )" `
                -PercentComplete $percentComplete

            $requestItems = @(
                @{
                    Text = $text
                }
            )
            $requestBody = ConvertTo-Json -InputObject $requestItems -Depth 3
            $response = Invoke-RestMethod `
                -Method Post `
                -Uri $uri `
                -Headers $headers `
                -ContentType 'application/json; charset=utf-8' `
                -Body $requestBody

            $responseItems = @($response)
            if (
            $responseItems.Count -eq 0 -or
                    $null -eq $responseItems[0].translations -or
                    $responseItems[0].translations.Count -eq 0
            )
            {
                throw "Azure Translator hat kein Ergebnis für den Text geliefert: $text"
            }

            $translatedTexts.Add([string]$responseItems[0].translations[0].text)
        }
    }
    finally
    {
        Write-Progress -Activity $progressActivity -Completed
        $key = $null
    }

    return $translatedTexts.ToArray()
}

function Get-CompactCategory
{
    param(
        [string]$Title,
        [string]$FallbackCategory
    )

    switch -Regex ($Title)
    {
        '(?i)dokumentation' {
            return 'Dokumentation'
        }
        '(?i)testabdeckung' {
            return 'Tests und Qualität'
        }
        '(?i)build|paket|release|konfiguration|api-kommunikation|technische grundlagen' {
            return 'Technik und Betrieb'
        }
        '(?i)stabilisiert|abstürz|fehlerbehebungen' {
            return 'Fehlerbehebungen'
        }
        '(?i)erweitert|funktionen ergänzt' {
            return 'Neue Funktionen'
        }
        default {
            return $FallbackCategory
        }
    }
}

Push-Location $solutionRoot
try
{
    $isGitRepository = & git rev-parse --is-inside-work-tree 2> $null
    if ($LASTEXITCODE -ne 0 -or $isGitRepository -ne 'true')
    {
        throw "Der Solution-Ordner ist kein Git-Repository: $solutionRoot"
    }

    $logLines = @(
        & git log `
            --no-merges `
            "--since=$startDate" `
            "--until=$endDate" `
            --date=short `
            '--pretty=format:%h%x1f%ad%x1f%s'
    )

    if ($LASTEXITCODE -ne 0)
    {
        throw 'Die Git-Historie konnte nicht gelesen werden.'
    }
}
finally
{
    Pop-Location
}

$entries = @()
foreach ($line in $logLines)
{
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('{"type":'))
    {
        continue
    }

    $parts = $line.Split($fieldSeparator)
    if ($parts.Count -lt 3)
    {
        continue
    }

    $subject = $parts[2].Trim()
    if (Test-IsMaintenanceCommit -Subject $subject)
    {
        continue
    }

    $originalCategory = Get-ChangeCategory -Subject $subject
    if ($UseAzureTranslator)
    {
        $compactTitle = Get-CompactEnglishTitle -Subject $subject -Category $originalCategory
    }
    else
    {
        $compactTitle = Get-CompactGermanTitle -Subject $subject -Category $originalCategory
    }

    $entries += [pscustomobject]@{
        Hash = $parts[0].Trim()
        Date = [datetime]::ParseExact($parts[1].Trim(), 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
        Category = $originalCategory
        Title = $compactTitle
    }
}

$entries = @($entries | Sort-Object Date -Descending)

if ($UseAzureTranslator -and $entries.Count -gt 0)
{
    $sourceTitles = @(
    $entries |
            ForEach-Object Title |
            Select-Object -Unique
    )
    $translatedTitles = @(
        Invoke-AzureTitleTranslation `
            -Texts $sourceTitles `
            -Endpoint $AzureTranslatorEndpoint `
            -Region $AzureTranslatorRegion
    )
    $translationMap = @{ }

    for ($index = 0; $index -lt $sourceTitles.Count; $index++) {
        $translationMap[$sourceTitles[$index]] = $translatedTitles[$index]
    }

    foreach ($entry in $entries)
    {
        $entry.Title = $translationMap[$entry.Title]
        $entry.Category = Get-CompactCategory -Title $entry.Title -FallbackCategory $entry.Category
    }
}
else
{
    foreach ($entry in $entries)
    {
        $entry.Category = Get-CompactCategory -Title $entry.Title -FallbackCategory $entry.Category
    }
}

$uniqueEntries = @()
$seenTitles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $entries)
{
    if ( $seenTitles.Add($entry.Title))
    {
        $uniqueEntries += $entry
    }
}
$entries = $uniqueEntries

$categoryOrder = @(
    'Neue Funktionen',
    'Verbesserungen',
    'Fehlerbehebungen',
    'Technik und Betrieb',
    'Tests und Qualität',
    'Dokumentation'
)
$categoryDescriptions = @{
    'Neue Funktionen' = 'Neue Möglichkeiten und erweiterte Arbeitsabläufe.'
    'Verbesserungen' = 'Optimierungen bestehender Funktionen und Abläufe.'
    'Fehlerbehebungen' = 'Korrekturen für mehr Stabilität und Zuverlässigkeit.'
    'Technik und Betrieb' = 'Verbesserungen an Build, Konfiguration und technischer Basis.'
    'Tests und Qualität' = 'Erweiterungen zur Absicherung der Softwarequalität.'
    'Dokumentation' = 'Aktualisierte Informationen für Anwender und Entwicklung.'
}

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add("# $DocumentTitle")
$markdown.Add('')
$markdown.Add('> Die wichtigsten Neuerungen, Verbesserungen und technischen Änderungen kompakt zusammengefasst.')
$markdown.Add('')
$markdown.Add('---')
$markdown.Add('')

if ($entries.Count -eq 0)
{
    $markdown.Add('_Für diesen Zeitraum wurden keine relevanten Änderungen gefunden._')
}
else
{
    foreach ($category in $categoryOrder)
    {
        $categoryEntries = @($entries | Where-Object Category -eq $category)
        if ($categoryEntries.Count -eq 0)
        {
            continue
        }

        $markdown.Add("## $category")
        $markdown.Add('')
        $markdown.Add("_$( $categoryDescriptions[$category] )_")
        $markdown.Add('')

        foreach ($entry in $categoryEntries)
        {
            if ($IncludeDate)
            {
                $dateText = $entry.Date.ToString('dd.MM.yyyy')
                $markdown.Add("- **${dateText}:** $( $entry.Title )")
            }
            else
            {
                $markdown.Add("- $( $entry.Title )")
            }
        }

        $markdown.Add('')
    }
}

if (-not $IncludeMaintenance)
{
    $markdown.Add('---')
    $markdown.Add('')
    $markdown.Add('_Automatisch aus der Git-Historie erstellt. Reine Merge-, Formatierungs-, Screenshot-, Icon- und Asset-Wartungscommits wurden ausgeblendet._')
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory))
{
    $null = New-Item -ItemType Directory -Path $outputDirectory -Force
}

$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllLines($OutputPath, $markdown, $utf8WithoutBom)

Write-Host "Markdown-Datei erstellt: $OutputPath"
