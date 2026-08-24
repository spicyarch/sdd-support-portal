$ErrorActionPreference = 'Stop'

$dotnet = 'dotnet'
if (-not (Get-Command $dotnet -ErrorAction SilentlyContinue)) {
    $dotnet = 'C:\Program Files\dotnet\dotnet.exe'
}

& $dotnet restore .\src\SupportPortal.sln
& $dotnet build .\src\SupportPortal.sln --configuration Release --no-restore
& $dotnet test .\src\SupportPortal.sln --configuration Release --no-build --no-restore

if (Get-Command npx -ErrorAction SilentlyContinue) {
    & npx --yes @redocly/cli@latest lint .\specs\001-support-portal-rbac\contracts\support-portal-api.yaml
    & npx --yes @redocly/cli@latest lint .\specs\002-branding-smtp-notifications\contracts\branding-email-api.yaml
}