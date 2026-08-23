param(
    [string]$ConnectionString = $env:Portal__SqlConnection
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'Portal__SqlConnection is required to verify restore counts.'
}

Add-Type -AssemblyName System.Data
$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
try {
    $connection.Open()
    $tables = @('Users', 'Teams', 'RoleAssignments', 'Invitations', 'SupportRequests', 'Messages', 'AuditEvents', 'CommandReceipts')
    foreach ($table in $tables) {
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT COUNT_BIG(*) FROM [$table]"
        $count = [long]$command.ExecuteScalar()
        Write-Output "$table=$count"
    }
    Write-Output 'Restore count verification completed. Compare these counts with the approved recovery checkpoint.'
}
finally {
    $connection.Dispose()
}
