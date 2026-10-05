$ErrorActionPreference = 'Stop'
$InstallDir = if ($env:DOTNET_INSTALL_DIR) { $env:DOTNET_INSTALL_DIR } else { Join-Path $HOME '.dotnet' }
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$installer = Join-Path $env:TEMP 'dotnet-install.ps1'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
& $installer -Channel 8.0 -InstallDir $InstallDir -NoPath
& $installer -Channel 9.0 -InstallDir $InstallDir -NoPath
& (Join-Path $InstallDir 'dotnet.exe') --list-sdks
