$ErrorActionPreference = "Stop"
Write-Host "Restoring Chaos Inbox..."
dotnet restore .\ChaosInbox.sln
Write-Host "Running tests..."
dotnet test .\ChaosInbox.sln --no-restore
Write-Host "Starting web app on http://localhost:5088 ..."
dotnet run --project .\ChaosInbox\ChaosInbox.csproj --launch-profile http
