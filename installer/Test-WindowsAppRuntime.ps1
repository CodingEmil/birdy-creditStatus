# Dot-source for isolated tests. Execute with -File for the installer probe.
# Match Microsoft.WindowsAppSDK.Runtime/2.5.1/include/WindowsAppSDK-VersionInfo.cs:
# stable framework family Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe, min 2.5.1.0.
# MddBootstrap.h: for 2.x, minor releases share the major framework family.
# Keep this requirement and Setup.iss WinAppSdkUrl in sync with the SDK package.
function Test-BirdyWindowsAppRuntime {
    param([object[]] $Packages)

    foreach ($package in $Packages) {
        try {
            if ($null -ne $package -and
                $package.Name -eq 'Microsoft.WindowsAppRuntime.2' -and
                $package.PublisherId -eq '8wekyb3d8bbwe' -and
                [string]$package.Architecture -eq 'X64' -and
                [string]$package.Status -eq 'Ok' -and
                [version]$package.Version -ge [version]'2.5.1.0') {
                return $true
            }
        }
        catch {
            # Invalid metadata cannot satisfy the requirement; inspect remaining packages.
        }
    }
    return $false
}

if ($MyInvocation.InvocationName -ne '.') {
    try {
        $packages = @(Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.2' -ErrorAction Stop)
        if (Test-BirdyWindowsAppRuntime -Packages $packages) { exit 0 }
        exit 1
    }
    catch { exit 2 }
}
