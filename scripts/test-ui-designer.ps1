param([string]$OutputDirectory = '.artifacts/phase8-a4')

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
# PowerShell already uses Roslyn for Add-Type. Loading a different SDK version into that context conflicts.
Add-Type -AssemblyName Microsoft.CodeAnalysis
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$compilerVersion = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree].Assembly.GetName().Version

# Parse syntax rather than matching strings: comments, resource text and typed arrays are legitimate designer input.
$projects = @('WawiCallPro.App', 'WawiCallPro.Plugin.Windows', 'WawiCallPro.Plugins.Lexware.Pro', 'WawiCallPro.Plugins.Lexware.FinOffice')
$classes = @()
foreach ($project in $projects) {
    $files = Get-ChildItem (Join-Path $repository $project) -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and !$_.Name.EndsWith('.Designer.cs') }
    foreach ($file in $files) {
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName))
        foreach ($node in $tree.GetRoot().DescendantNodes()) {
            if ($node.GetType().Name -eq 'ObjectCreationExpressionSyntax' -and $node.Type.ToString() -match '(^|\.)(OpenFileDialog|SaveFileDialog|FolderBrowserDialog)$') {
                throw "Standard-Browse-Dialog in $($file.FullName): $($node.Type)"
            }
            if ($node.GetType().Name -eq 'MethodDeclarationSyntax' -and $node.Identifier.ValueText -eq 'InitializeComponent') {
                throw "Designer-Aufbau außerhalb der Designer-Datei: $($file.FullName)"
            }
            if ($node.GetType().Name -ne 'ClassDeclarationSyntax' -or !$node.BaseList) { continue }
            $classes += [pscustomobject]@{ Name = $node.Identifier.ValueText; Node = $node; File = $file; Bases = @($node.BaseList.Types | ForEach-Object { ($_.Type.ToString() -split '\.')[-1] }) }
        }
    }
}
$baseNames = [Collections.Generic.HashSet[string]]::new([string[]]@('Form', 'UserControl', 'XtraForm', 'XtraUserControl', 'RibbonForm'))
$surfaces = @()
do {
    $added = @($classes | Where-Object { !$baseNames.Contains($_.Name) -and @($_.Bases | Where-Object { $baseNames.Contains($_) }).Count -gt 0 })
    foreach ($surface in $added) { [void]$baseNames.Add($surface.Name); $surfaces += $surface }
} while ($added.Count -gt 0)
if ($surfaces.Count -eq 0) { throw 'Keine eigenen Oberflächen gefunden.' }
$results = foreach ($surface in $surfaces | Sort-Object Name) {
    $name = $surface.Name
    $designer = Join-Path $surface.File.DirectoryName "$name.Designer.cs"
    $resource = Join-Path $surface.File.DirectoryName "$name.resx"
    if (!(Test-Path $designer) -or !(Test-Path $resource)) { throw "Dateitripel fehlt: $name" }
    if ('partial' -notin @($surface.Node.Modifiers | ForEach-Object { $_.ValueText })) { throw "partial fehlt: $name" }
    $constructors = @($surface.Node.Members | Where-Object { $_.GetType().Name -eq 'ConstructorDeclarationSyntax' -and $_.ParameterList.Parameters.Count -eq 0 -and 'public' -in @($_.Modifiers | ForEach-Object { $_.ValueText }) })
    if ($constructors.Count -ne 1) { throw "Öffentlicher parameterloser Konstruktor fehlt: $name" }
    if ($constructors[0].ToString() -notmatch '\bInitializeComponent\s*\(') { throw "Designeraufbau fehlt im Konstruktor: $name" }
    $resourceXml = [xml][IO.File]::ReadAllText($resource)
    if ($resourceXml.DocumentElement.Name -ne 'root' -or @($resourceXml.root.resheader).Count -lt 4) { throw "Ungültige WinForms-Ressource: $name" }
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($designer))
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq Error)
    if ($errors.Count -gt 0) { throw "Designer-Syntaxfehler: $name $errors" }
    $methods = @($tree.GetRoot().DescendantNodes() | Where-Object { $_.GetType().Name -eq 'MethodDeclarationSyntax' })
    $initialize = @($methods | Where-Object { $_.Identifier.ValueText -eq 'InitializeComponent' })
    if ($initialize.Count -ne 1 -or @($methods | Where-Object { $_.Identifier.ValueText -notin @('InitializeComponent', 'Dispose') }).Count) { throw "Laufzeitmethoden in Designer-Datei: $name" }
    $forbidden = @('IfStatementSyntax', 'ForStatementSyntax', 'ForEachStatementSyntax', 'WhileStatementSyntax', 'DoStatementSyntax',
        'SwitchStatementSyntax', 'SwitchExpressionSyntax', 'ConditionalExpressionSyntax', 'ConditionalAccessExpressionSyntax',
        'SimpleLambdaExpressionSyntax', 'ParenthesizedLambdaExpressionSyntax', 'AnonymousMethodExpressionSyntax', 'LocalFunctionStatementSyntax',
        'QueryExpressionSyntax', 'AwaitExpressionSyntax', 'CollectionExpressionSyntax', 'InterpolatedStringExpressionSyntax', 'ImplicitObjectCreationExpressionSyntax')
    foreach ($node in $initialize[0].DescendantNodes()) {
        if ($node.GetType().Name -in $forbidden -or $node.RawKind -eq [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::CoalesceExpression -or
            ($node.GetType().Name -eq 'InvocationExpressionSyntax' -and $node.Expression.ToString() -eq 'nameof')) {
            throw "Nicht serialisierbarer Designer-Code in ${name}: $($node.GetType().Name) $node"
        }
        if ($node.GetType().Name -eq 'ObjectCreationExpressionSyntax' -and $node.Type.ToString() -match '(^|\.)(LayoutControl|TablePanel|OpenFileDialog|SaveFileDialog|FolderBrowserDialog)$') {
            throw "Unzulässiges Layout/Browse-Control in ${name}: $($node.Type)"
        }
    }
    [pscustomobject]@{ Name = $name; Source = [IO.Path]::GetRelativePath($repository, $surface.File.FullName); Designer = [IO.Path]::GetRelativePath($repository, $designer); Resource = [IO.Path]::GetRelativePath($repository, $resource); DesignerSyntax = 'passed'; ResourceXml = 'passed' }
}
$output = [IO.Path]::GetFullPath($OutputDirectory, $repository)
[void][IO.Directory]::CreateDirectory($output)
$results | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output 'designer-sources.json') -Encoding utf8
Write-Output "$($results.Count) Oberflächen: Dateitripel, Ressourcen-XML, parameterlose Konstruktoren und Designer-Syntax bestanden (Roslyn $compilerVersion)."
Write-Output 'Dies ersetzt weder die Laufzeit-/Renderprüfungen noch einen interaktiven Designer-Roundtrip.'
