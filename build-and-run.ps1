# Build and run LlaModem
# Cleans, restores, builds, and runs the project

Write-Host "=== LlaModem Build and Run ===" -ForegroundColor Cyan

# Clean
Write-Host "`n[1/4] Cleaning builds..." -ForegroundColor Yellow
dotnet clean
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to clean build" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Restore
Write-Host "`n[2/4] Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to restore packages" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Build
Write-Host "`n[3/4] Building project..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build project" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Run
Write-Host "`n[4/4] Running project..." -ForegroundColor Yellow
dotnet run

exit $LASTEXITCODE
