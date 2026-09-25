. "$PSScriptRoot\..\scripts\Common.ps1"
$dotnet = Find-DevTool 'dotnet'
& $dotnet build "$PSScriptRoot\StartupGate\StartupGate.csproj" --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Startup harness build failed.' }
$harness = "$PSScriptRoot\StartupGate\bin\Release\net8.0\StartupGate.dll"
$normal = & $dotnet $harness no-update
if ($LASTEXITCODE -ne 0 -or $normal -notcontains 'GAMEPLAY_ENABLED') { throw 'Normal startup was blocked.' }
$blocked = & $dotnet $harness ready
if ($LASTEXITCODE -ne 0 -or $blocked -contains 'GAMEPLAY_ENABLED') { throw 'Ready update did not terminate startup.' }
if (!($blocked | Where-Object { $_ -like 'Error:*' })) { throw 'Restart instruction was not logged.' }
Write-Host 'PASS: normal startup registers gameplay; a prepared update logs restart instructions and exits before gameplay registration.'
