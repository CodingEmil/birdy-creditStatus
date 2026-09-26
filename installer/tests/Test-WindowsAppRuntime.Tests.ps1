# No Pester dependency; exercises the production predicate with synthetic packages only.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../Test-WindowsAppRuntime.ps1"

function Package([string] $name = 'Microsoft.WindowsAppRuntime.2',
                 [string] $version = '2.5.1.0',
                 [string] $arch = 'X64',
                 [string] $publisher = '8wekyb3d8bbwe',
                 [string] $status = 'Ok') {
    [pscustomobject]@{ Name = $name; Version = $version; Architecture = $arch; PublisherId = $publisher; Status = $status }
}

$cases = @(
    @{ Name = 'none'; Packages = @(); Expected = $false }
    @{ Name = 'old 1.x with larger numeric version'; Packages = @((Package -name 'Microsoft.WindowsAppRuntime.1.8' -version '8000.994.2142.0')); Expected = $false }
    @{ Name = 'minimum not met'; Packages = @((Package -version '2.5.0.0')); Expected = $false }
    @{ Name = 'x86 only'; Packages = @((Package -arch 'X86')); Expected = $false }
    @{ Name = 'ARM64 only'; Packages = @((Package -arch 'Arm64')); Expected = $false }
    @{ Name = 'different publisher'; Packages = @((Package -publisher 'untrusted')); Expected = $false }
    @{ Name = 'preview'; Packages = @((Package -name 'Microsoft.WindowsAppRuntime.2-preview')); Expected = $false }
    @{ Name = 'CBS package'; Packages = @((Package -name 'Microsoft.WindowsAppRuntime.CBS.2')); Expected = $false }
    @{ Name = 'future incompatible major'; Packages = @((Package -name 'Microsoft.WindowsAppRuntime.3' -version '3.0.0.0')); Expected = $false }
    @{ Name = 'damaged package'; Packages = @((Package -status 'NeedsRemediation')); Expected = $false }
    @{ Name = 'invalid version'; Packages = @((Package -version 'invalid')); Expected = $false }
    @{ Name = 'null entry'; Packages = @($null); Expected = $false }
    @{ Name = 'matching minimum'; Packages = @((Package)); Expected = $true }
    @{ Name = 'newer patch'; Packages = @((Package -version '2.5.2.0')); Expected = $true }
    @{ Name = 'compatible newer minor'; Packages = @((Package -version '2.6.0.0')); Expected = $true }
    @{ Name = 'mixed packages'; Packages = @((Package -arch 'X86'), (Package -name 'Microsoft.WindowsAppRuntime.1.8'), (Package)); Expected = $true }
)
$failures = 0
foreach ($case in $cases) {
    try {
        $actual = Test-BirdyWindowsAppRuntime -Packages $case.Packages
        if ($actual -ne $case.Expected) { throw "expected $($case.Expected), got $actual" }
        Write-Output "PASS $($case.Name)"
    }
    catch {
        $failures++
        Write-Output "FAIL $($case.Name): $_"
    }
}
# Exercise the real -File entry behavior in child processes, with Get-AppxPackage
# replaced by synthetic functions. No machine package query/install in this suite.
$probe = (Resolve-Path "$PSScriptRoot/../Test-WindowsAppRuntime.ps1").Path.Replace("'", "''")
$processCases = @(
    @{ Name = 'query returns none'; Stub = ''; Expected = 1 }
    @{ Name = 'query fails'; Stub = 'throw "synthetic query failure"'; Expected = 2 }
    @{ Name = 'query returns matching runtime'; Stub = "[pscustomobject]@{Name='Microsoft.WindowsAppRuntime.2'; Version='2.5.1.0'; Architecture='X64'; PublisherId='8wekyb3d8bbwe'; Status='Ok'}"; Expected = 0 }
)
foreach ($case in $processCases) {
    & "$PSHOME/powershell.exe" -NoProfile -NonInteractive -Command "function Get-AppxPackage { $($case.Stub) }; & '$probe'; exit `$LASTEXITCODE"
    if ($LASTEXITCODE -ne $case.Expected) {
        $failures++
        Write-Output "FAIL $($case.Name): expected exit $($case.Expected), got $LASTEXITCODE"
    }
    else { Write-Output "PASS $($case.Name)" }
}
if ($failures -gt 0) { throw "$failures runtime probe tests failed" }
Write-Output "$($cases.Count + $processCases.Count) runtime probe tests passed"
