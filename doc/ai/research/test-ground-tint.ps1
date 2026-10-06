$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$taskManaged = [Environment]::GetEnvironmentVariable('CSII_MANAGEDPATH', 'User')
if (-not $taskManaged) { throw 'CSII_MANAGEDPATH is missing.' }
if (-not (Test-Path -LiteralPath (Join-Path $PSHOME 'ref'))) {
    throw 'Run this check with PowerShell 7 (pwsh).'
}
$taskRefs = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' |
    Select-Object -ExpandProperty FullName) + @(
    (Join-Path $taskManaged 'UnityEngine.CoreModule.dll'),
    (Join-Path $taskManaged 'Unity.RenderPipelines.Core.Runtime.dll'))
[Reflection.Assembly]::LoadFrom((Join-Path $taskManaged 'UnityEngine.CoreModule.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $taskManaged 'Unity.RenderPipelines.Core.Runtime.dll')) | Out-Null
$taskHarness = Join-Path $env:TEMP ('SunGlasses-ground-tint-' + [Guid]::NewGuid() + '.cs')
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GroundTintBehaviorCheck.cs.txt') -Destination $taskHarness
    Add-Type -Path @((Join-Path $taskRoot 'Systems\GroundTintOverride.cs'), $taskHarness) -ReferencedAssemblies $taskRefs
    [GroundTintBehaviorCheck]::Run()
}
finally {
    Remove-Item -LiteralPath $taskHarness -ErrorAction SilentlyContinue
}
