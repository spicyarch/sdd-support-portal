param(
    [string]$ConnectionString = $env:Portal__SqlConnection
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'Portal__SqlConnection is required to apply Azure SQL migrations.'
}

$dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { 'dotnet' } else { 'C:\Program Files\dotnet\dotnet.exe' }
& $dotnet ef database update --project .\src\SupportPortal.Infrastructure --startup-project .\src\SupportPortal.Api --connection $ConnectionString
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
