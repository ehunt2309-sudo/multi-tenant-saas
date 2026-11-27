# Starts the backend API and the demo website, then opens the browser.
# Put this file in the repo root (next to the "backend" and "frontend" folders).
# Optional: .\start-demo.ps1 -Seed   (fills the demo venue with sample data; safe to repeat)
param([switch]$Seed)

$root = $PSScriptRoot
$api  = Join-Path $root "backend\Platform.API\Platform.API"
$web  = Join-Path $root "frontend"

function Stop-Here($msg) {
  Write-Host ""
  Write-Host $msg -ForegroundColor Red
  Read-Host "Press Enter to close"
  exit 1
}

function Need($cmd, $name, $url) {
  if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
    Stop-Here "Missing: $name. Install it from $url, restart the computer, then double-click Start Demo again."
  }
}

function Wait-Port($port, $seconds) {
  $end = (Get-Date).AddSeconds($seconds)
  while ((Get-Date) -lt $end) {
    try {
      $c = New-Object Net.Sockets.TcpClient
      $c.Connect("localhost", $port)
      $c.Close()
      return $true
    } catch { Start-Sleep -Seconds 2 }
  }
  return $false
}

Write-Host "Checking requirements..." -ForegroundColor Cyan
Need "dotnet" ".NET 10 SDK" "https://dotnet.microsoft.com/download"
Need "node"   "Node.js (LTS)" "https://nodejs.org"
if (-not (Test-Path $api)) { Stop-Here "Can't find the backend folder. Is this file in the repo root?" }
if (-not (Test-Path $web)) { Stop-Here "Can't find the frontend folder. Is this file in the repo root?" }

# Trust the local HTTPS certificate (does nothing if already trusted)
dotnet dev-certs https --trust 2>$null | Out-Null

Write-Host "Starting the API (first start can take a minute)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$api'; dotnet run --launch-profile https"

if (-not (Wait-Port 7260 120)) {
  Stop-Here "The API did not start. Look at the API window for a red error message and send it to Ethan."
}

if (-not (Test-Path (Join-Path $web "node_modules"))) {
  Write-Host "Installing website packages (first time only)..." -ForegroundColor Cyan
  Push-Location $web
  npm install
  Pop-Location
}

if ($Seed) {
  Write-Host "Adding demo data..." -ForegroundColor Cyan
  & (Join-Path $root "docs\seed-demo-data.ps1")
}

Write-Host "Starting the website..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$web'; npm run dev -w client-a"

if (-not (Wait-Port 5173 60)) {
  Stop-Here "The website did not start. Look at the website window for a red error message."
}

Start-Process "http://localhost:5173"
Write-Host ""
Write-Host "Demo is running at http://localhost:5173" -ForegroundColor Green
Write-Host "To stop it: close the two black windows (API and website)."
