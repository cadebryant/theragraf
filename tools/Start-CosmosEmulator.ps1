# Start-CosmosEmulator.ps1
# Starts the Azure Cosmos DB Emulator if it is not already running.
# The emulator exposes its Data Explorer on port 8081 (HTTPS).

$cosmosPort = 8081

function Test-PortListening([int]$Port) {
	$connections = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
	return $connections | Where-Object { $_.Port -eq $Port }
}

if (Test-PortListening $cosmosPort) {
	Write-Host "Cosmos DB Emulator is already running (port $cosmosPort is listening). Skipping start."
	exit 0
}

$candidates = @(
	"${env:ProgramFiles}\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe",
	"${env:ProgramFiles(x86)}\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe",
	"${env:LocalAppData}\Programs\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"
)

$emulatorPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $emulatorPath) {
	Write-Warning "Azure Cosmos DB Emulator not found in any known install location."
	Write-Warning "Download it from: https://aka.ms/cosmosdb-emulator"
	exit 1
}

Write-Host "Starting Azure Cosmos DB Emulator from: $emulatorPath"
Start-Process -FilePath $emulatorPath -WindowStyle Minimized
