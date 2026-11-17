# Build RodentRoads.dll and package it with ts3buildtool.
#
#   powershell -ExecutionPolicy Bypass -File tools/build.ps1
#   powershell -ExecutionPolicy Bypass -File tools/build.ps1 -Configuration Debug
#   powershell -ExecutionPolicy Bypass -File tools/build.ps1 -NoPackage   # DLL only
#
# Outputs:
#   src\RodentRoads\bin\<Configuration>\RodentRoads.dll
#   <game Mods>\Packages\RodentRoads.package   (via ts3buildtool, i.e. installed for testing)
#
# Packaging notes:
#   ts3buildtool (https://github.com/Paprotk/ts3buildtool) reads
#   src\RodentRoads\resources\nameMap.xml, embeds the DLL as an S3SA resource and
#   writes the package into the game's Mods folder. Without -ModsFolder it uses
#   %USERPROFILE%\Documents\Electronic Arts\The Sims 3\Mods (MyDocuments).
#   It deletes any existing RodentRoads package it finds there first, so this is a
#   one-shot "build + install" step.
#
#   The tool must be a built, self-contained exe (its release build embeds s3pi via
#   Costura). Either drop it in Tools\ts3buildtool.exe (the layout its README
#   assumes) or build the cloned repo into ts3buildtool\bin|obj\...\ts3buildtool.exe.

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # Skip packaging, build the DLL only.
    [switch]$NoPackage,

    # Package name (also used for the FNV seed of STBL instances later).
    [string]$ModName = 'RodentRoads'
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $Root 'src\RodentRoads'
$RefAsmRoot = Join-Path $Root '.refasm'
$DotNet2Dir = 'C:\Windows\Microsoft.NET\Framework\v2.0.50727'

if (-not (Test-Path $DotNet2Dir)) {
    throw ".NET Framework 2.0 is not installed ($DotNet2Dir missing). Enable the '.NET Framework 3.5' Windows feature."
}

# MSBuild may look for <root>\v2.0 or <root>\.NETFramework\v2.0 - provide both.
foreach ($rel in @('v2.0', '.NETFramework\v2.0')) {
    $link = Join-Path $RefAsmRoot $rel
    if (-not (Test-Path $link)) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $link) | Out-Null
        New-Item -ItemType Junction -Path $link -Target $DotNet2Dir | Out-Null
        Write-Host "created junction $rel -> $DotNet2Dir"
    }
}

$Project = Join-Path $ProjectDir 'RodentRoads.csproj'
$Dll = Join-Path $ProjectDir "bin\$Configuration\RodentRoads.dll"

Write-Host "==> dotnet build ($Configuration)"
dotnet build $Project -c $Configuration -p:TargetFrameworkRootPath="$RefAsmRoot\"
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
Write-Host "    $Dll"

if ($NoPackage) {
    Write-Host '==> packaging skipped (-NoPackage)'
    exit 0
}

Write-Host '==> packaging (ts3buildtool)'

$nameMap = Join-Path $ProjectDir 'resources\nameMap.xml'
if (-not (Test-Path $nameMap)) {
    throw "missing $nameMap - ts3buildtool needs it to know what to pack"
}

$toolCandidates = @(
    (Join-Path $Root 'Tools\ts3buildtool.exe'),
    (Join-Path $Root 'ts3buildtool\bin\Release\net472\ts3buildtool.exe'),
    (Join-Path $Root 'ts3buildtool\bin\Debug\net472\ts3buildtool.exe'),
    (Join-Path $Root 'ts3buildtool\obj\Release\net472\ts3buildtool.exe'),
    (Join-Path $Root 'ts3buildtool\obj\Debug\net472\ts3buildtool.exe')
)
$tool = $toolCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $tool) {
    throw @"
ts3buildtool.exe not found. Looked in:
$($toolCandidates -join "`n")

Get the self-contained exe from https://github.com/Paprotk/ts3buildtool/releases
and put it at Tools\ts3buildtool.exe, or build the repo in ts3buildtool\.
Or rerun with -NoPackage to build the DLL only.
"@
}

$toolArgs = @(
    "-modName=$ModName",
    "-dllPath=$Dll",
    "-projectDir=$ProjectDir"
)
& $tool @toolArgs
if ($LASTEXITCODE -ne 0) { throw 'ts3buildtool failed' }

Write-Host 'done.'
