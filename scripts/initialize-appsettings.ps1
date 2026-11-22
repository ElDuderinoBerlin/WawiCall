param(
    [Parameter(Mandatory)][string]$SettingsPath,
    [Parameter(Mandatory)][string]$SolutionProps,
    [ValidateSet('Pro','FinOffice')][string]$ProductLine,
    [string]$Configuration,
    [string]$DebugDefaults,
    [int]$IncludeClientKey = 1,
    [int]$NeutralLexwareApi = 0
)
$ErrorActionPreference = 'Stop'
try {
    $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json -AsHashtable
    if (!$NeutralLexwareApi -and $Configuration -eq 'Debug' -and $ProductLine -eq 'Pro' -and $IncludeClientKey -and [string]::IsNullOrWhiteSpace($settings.LexwareApi.ClientKey)) {
        [xml]$props = Get-Content -LiteralPath $SolutionProps -Raw
        $value = [string]$props.Project.PropertyGroup.LexwareApiClientKeyPro
        $parsed = [guid]::Empty
        if (![guid]::TryParseExact($value, 'B', [ref]$parsed) -or $parsed -eq [guid]::Empty) { throw 'Invalid client-key default.' }
        $aes = [Security.Cryptography.Aes]::Create()
        try {
            $hash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('LxTranslate.LexwareClientKey.AES128.v1'))
            $aes.Key = [byte[]]$hash[0..15]
            $aes.GenerateIV()
            $data = [Text.Encoding]::UTF8.GetBytes($value)
            $encryptor = $aes.CreateEncryptor()
            try { $cipher = $encryptor.TransformFinalBlock($data, 0, $data.Length) } finally { $encryptor.Dispose(); [Array]::Clear($data) }
            $settings.LexwareApi.ClientKey = [Convert]::ToBase64String([byte[]]($aes.IV + $cipher))
        } finally { $aes.Dispose() }
    }
    if ($NeutralLexwareApi) { $settings.Remove('LexwareApi') | Out-Null }
    $settings.Remove('DebugLexware') | Out-Null
    if (!$NeutralLexwareApi -and $ProductLine -eq 'Pro' -and $Configuration -eq 'Debug' -and (Test-Path -LiteralPath $DebugDefaults)) {
        $settings.DebugLexware = Get-Content -LiteralPath $DebugDefaults -Raw | ConvertFrom-Json -AsHashtable
    }
    $settings | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $SettingsPath -Encoding utf8
} catch { throw 'appsettings.json defaults could not be prepared. Check the template and build configuration.' }
