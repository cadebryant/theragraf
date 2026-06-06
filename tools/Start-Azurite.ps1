# Start-Azurite.ps1
# Starts Azurite (using the Visual Studio bundled binary) if it is not already running.
# Azurite ports: 10000 (Blob), 10001 (Queue), 10002 (Table)

param(
	[string]$DataLocation = "$PSScriptRoot\..\Theragraf.Functions\.azurite"
)

$blobPort  = 10000
$queuePort = 10001
$tablePort = 10002

function Test-PortListening([int]$Port) {
	$connections = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
	return $connections | Where-Object { $_.Port -eq $Port }
}

if (Test-PortListening $blobPort) {
	Write-Host "Azurite is already running (port $blobPort is listening). Skipping start."
	exit 0
}

# Resolve the VS-bundled azurite.exe — prefer the active VS install
$candidates = @(
	"${env:ProgramFiles}\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe",
	"${env:ProgramFiles}\Microsoft Visual Studio\18\Insiders\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe",
	"${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe",
	"${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe",
	"${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe"
)

$azuritePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $azuritePath) {
	Write-Warning "Azurite executable not found in any known Visual Studio install location."
	Write-Warning "Install it globally with: npm install -g azurite"
	exit 1
}

# Ensure the data directory exists
$DataLocation = [System.IO.Path]::GetFullPath($DataLocation)
if (-not (Test-Path $DataLocation)) {
	New-Item -ItemType Directory -Path $DataLocation | Out-Null
}

Write-Host "Starting Azurite from: $azuritePath"
Write-Host "Data location: $DataLocation"

Start-Process -FilePath $azuritePath `
	-ArgumentList "--skipApiVersionCheck", "--location", "`"$DataLocation`"" `
	-WindowStyle Minimized
