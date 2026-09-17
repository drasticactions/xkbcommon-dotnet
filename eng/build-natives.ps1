#Requires -Version 7
<#
.SYNOPSIS
Builds xkbcommon.dll for win-x64 or win-arm64 into runtimes/<rid>/native.

.DESCRIPTION
A native MSVC meson build on a host of the same architecture (win-arm64 builds
on an arm64 runner). Core and compose only: X11 and the registry stay off, as
on every bundled platform. The xkb_dotnet_free shim (eng/shim) is compiled
with cl and handed to the linker through a meson native file's c_link_args;
the script fails if the DLL does not export it. Needs meson, ninja and
win_bison (choco install winflexbison3; bison >= 3.6 is a hard requirement of
libxkbcommon) on PATH, and a Visual Studio C++ toolset for the target.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path "$root/external/libxkbcommon/meson.build")) {
    throw 'external/libxkbcommon is empty: run git submodule update --init.'
}

$targetArch = if ($Rid -eq 'win-arm64') { 'arm64' } else { 'x64' }
$hostArch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }

# Always enter a dev shell for the requested target, even if cl.exe is already
# on PATH: a shell set up for another architecture would silently produce the
# wrong objects.
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
if (-not (Test-Path $vswhere)) { throw 'No vswhere.exe to find Visual Studio.' }
$component = if ($targetArch -eq 'arm64') { 'Microsoft.VisualStudio.Component.VC.Tools.ARM64' } else { 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64' }
$vs = & $vswhere -latest -products * -requires $component -property installationPath
if (-not $vs) { throw "No Visual Studio with the C++ $targetArch toolset ($component) is installed." }
Import-Module (Join-Path $vs 'Common7/Tools/Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $vs -SkipAutomaticLocation -DevCmdArguments "-arch=$targetArch -host_arch=$hostArch" | Out-Null
if (-not (Get-Command cl.exe -ErrorAction SilentlyContinue)) { throw 'Enter-VsDevShell did not put cl.exe on PATH.' }
if (-not (Get-Command win_bison.exe -ErrorAction SilentlyContinue) -and -not (Get-Command bison.exe -ErrorAction SilentlyContinue)) {
    throw 'libxkbcommon needs bison >= 3.6 (win_bison from winflexbison3) on PATH.'
}

$build = Join-Path $root "artifacts/natives/$Rid"
$out = Join-Path $root "runtimes/$Rid/native"
if (Test-Path $build) { Remove-Item -Recurse -Force $build }
New-Item -ItemType Directory -Force -Path (Split-Path $build) | Out-Null

# The shim object goes to the linker through c_link_args, so the DLL link pulls
# it in (test executables receive it too, harmlessly).
$shimObj = Join-Path (Split-Path $build) "$Rid-shim.obj"
& cl /nologo /O2 /c "$root/eng/shim/xkbcommon-dotnet-shim.c" "/Fo$shimObj"
if ($LASTEXITCODE -ne 0) { throw 'compiling the free shim failed' }

$native = "$build.ini"
$shimObjMeson = $shimObj -replace '\\', '/'
@"
[built-in options]
c_link_args = ['$shimObjMeson']
"@ | Set-Content -Path $native -Encoding ascii

# Core + compose only, config roots pinned so the baked-in default include path
# never depends on the build host (XKB_CONFIG_ROOT overrides it at runtime).
$mesonArgs = @(
    'setup', $build, "$root/external/libxkbcommon",
    '--native-file', $native,
    '--buildtype=release',
    '--default-library=shared',
    '-Denable-x11=false',
    '-Denable-wayland=false',
    '-Denable-xkbregistry=false',
    '-Denable-tools=false',
    '-Denable-docs=false',
    '-Denable-bash-completion=false',
    '-Dxkb-config-root=/usr/share/X11/xkb',
    '-Dx-locale-root=/usr/share/X11/locale'
)

meson @mesonArgs
if ($LASTEXITCODE -ne 0) { throw 'meson setup failed' }

ninja -C $build
if ($LASTEXITCODE -ne 0) { throw 'ninja failed' }

New-Item -ItemType Directory -Force -Path $out | Out-Null
Get-ChildItem -Path $out -Filter *.dll -ErrorAction SilentlyContinue | Remove-Item -Force

$dll = Get-ChildItem -Path $build -Recurse -Filter 'xkbcommon*.dll' | Select-Object -First 1
if (-not $dll) { throw "meson built no xkbcommon dll in $build." }

$target = Join-Path $out 'xkbcommon.dll'
Copy-Item $dll.FullName $target

# Confirm the PE machine type matches the requested RID.
$bytes = [System.IO.File]::ReadAllBytes($target)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
$machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
$expected = if ($targetArch -eq 'arm64') { 0xAA64 } else { 0x8664 }
if ($machine -ne $expected) { throw ("xkbcommon.dll machine type is 0x{0:X4}, expected 0x{1:X4} for {2}." -f $machine, $expected, $Rid) }

# Confirm the shim survived the link.
$exports = & dumpbin /nologo /exports $target
if ($LASTEXITCODE -ne 0) { throw 'dumpbin failed' }
if (-not ($exports | Select-String -Pattern '\bxkb_dotnet_free\b' -Quiet)) {
    throw 'xkbcommon.dll does not export xkb_dotnet_free; the shim object was dropped from the link.'
}
if (-not ($exports | Select-String -Pattern '\bxkb_context_new\b' -Quiet)) {
    throw 'xkbcommon.dll does not export xkb_context_new.'
}

Get-ChildItem $out
