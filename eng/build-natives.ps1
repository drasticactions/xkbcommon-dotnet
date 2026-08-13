#Requires -Version 7
[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path "$root/external/libxkbcommon/meson.build")) {
    throw 'external/libxkbcommon is empty: run git submodule update --init.'
}

$build = Join-Path $root "artifacts/natives/$Rid"
$out = Join-Path $root "runtimes/$Rid/native"
if (Test-Path $build) { Remove-Item -Recurse -Force $build }

meson setup $build "$root/external/libxkbcommon" `
    --buildtype=release `
    --default-library=shared `
    -Denable-x11=false `
    -Denable-wayland=false `
    -Denable-xkbregistry=false `
    -Denable-tools=false `
    -Denable-docs=false `
    -Denable-bash-completion=false
if ($LASTEXITCODE -ne 0) { throw 'meson setup failed' }

ninja -C $build
if ($LASTEXITCODE -ne 0) { throw 'ninja failed' }

New-Item -ItemType Directory -Force -Path $out | Out-Null
Get-ChildItem -Path $out -Filter *.dll -ErrorAction SilentlyContinue | Remove-Item -Force

$dll = Get-ChildItem -Path $build -Recurse -Filter 'xkbcommon*.dll' | Select-Object -First 1
if (-not $dll) { throw "meson built no xkbcommon dll in $build." }

Copy-Item $dll.FullName (Join-Path $out 'xkbcommon.dll')
Get-ChildItem $out
