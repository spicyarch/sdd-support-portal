param(
    [string]$ConnectionString = $env:Portal__SqlConnection
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'Portal__SqlConnection is required to verify Azure SQL health.'
}

Add-Type -AssemblyName System.Data
$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT 1'
    if ($command.ExecuteScalar() -ne 1) { throw 'Azure SQL health query returned an unexpected result.' }
    Write-Output 'Azure SQL health check passed.'
}
finally {
    $connection.Dispose()
}
