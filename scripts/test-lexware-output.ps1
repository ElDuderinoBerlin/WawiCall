param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidateSet('Pro', 'FinOffice')][string]$ProductLine,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('AnyCPU', 'x86', 'x64')][string]$Architecture = 'AnyCPU'
)
$ErrorActionPreference = 'Stop'
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$repository = Split-Path $PSScriptRoot
$pro = @('Quantarix.Lexware.Core', 'Quantarix.Lexware.APIService')
$fin = @('Quantarix.Pcfk.Core', 'Quantarix.Pcfk.DataAccess', 'System.Data.SQLite')
$required = if ($ProductLine -eq 'Pro') { $pro } else { $fin }
$forbidden = if ($ProductLine -eq 'Pro') { $fin + 'SQLite.Interop' } else { $pro }
foreach ($name in $required) {
    if (!(Test-Path -LiteralPath (Join-Path $output "$name.dll"))) { throw "Required assembly missing: $name" }
}
foreach ($file in Get-ChildItem -LiteralPath $output -File -Recurse) {
    if ($file.BaseName -in $forbidden -and $file.Extension -in @('.dll', '.pdb', '.xml')) {
        throw "Foreign edition file: $($file.FullName)"
    }
}
$otherEdition = if ($ProductLine -eq 'Pro') { 'FinOffice' } else { 'Pro' }
$otherPlugin = Join-Path $output "plugins/WawiCallPro.Plugins.Lexware.$otherEdition/WawiCallPro.Plugins.Lexware.$otherEdition.dll"
if (Test-Path -LiteralPath $otherPlugin) { throw 'Foreign edition plugin remains loadable.' }
if ($ProductLine -eq 'FinOffice') {
    if (Test-Path -LiteralPath (Join-Path $output 'SQLite.Interop.dll')) { throw 'Obsolete architecture-bound root interop copy.' }
    $architectures = if ($Architecture -eq 'AnyCPU') { @('x86', 'x64') } else { @($Architecture) }
    foreach ($arch in $architectures) {
        $platform = if ($arch -eq 'x86') { 'Win32' } else { 'x64' }
        $source = Join-Path $repository "../sqlite-netFx-source-1.0.117.0/bin/$platform/$Configuration/SQLite.Interop.dll"
        $native = Join-Path $output "runtimes/win-$arch/native/SQLite.Interop.dll"
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash) {
            throw "Own SQLite.Interop binary differs: $arch"
        }
        $compatibility = Join-Path $output "$arch/SQLite.Interop.dll"
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $compatibility -Algorithm SHA256).Hash) {
            throw "SQLite preload compatibility binary differs: $arch"
        }
        $stream = [IO.File]::OpenRead($native)
        $reader = [IO.BinaryReader]::new($stream)
        try {
            $stream.Position = 0x3c
            $peOffset = $reader.ReadInt32()
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) { throw 'Invalid PE signature.' }
            $machine = $reader.ReadUInt16()
            $expected = if ($arch -eq 'x86') { 0x014c } else { 0x8664 }
            if ($machine -ne $expected) { throw "Wrong native architecture: $arch" }
        } finally { $reader.Dispose() }
        Write-Output "Own SQLite.Interop SHA256 and PE architecture verified: $arch"
    }
}
Write-Output "Lexware output verified: $ProductLine $Configuration $Architecture ($output)"
