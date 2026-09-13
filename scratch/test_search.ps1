try {
    $conn = New-Object System.Data.OleDb.OleDbConnection("Provider=Search.CollatorStore;Extended Properties='Application=Windows';")
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TOP 10 System.ItemName, System.ItemPathDisplay, System.Size, System.DateModified FROM SystemIndex WHERE SCOPE='file:' AND System.ItemName LIKE 'SolidWorks%' ORDER BY System.DateModified DESC"
    $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
    $ds = New-Object System.Data.DataSet
    $adapter.Fill($ds) | Out-Null
    Write-Host "Windows Search Results Count:" $ds.Tables[0].Rows.Count
    foreach ($row in $ds.Tables[0].Rows) {
        Write-Host "  Found:" $row["System.ItemName"] "(" $row["System.ItemPathDisplay"] ")"
    }
    $conn.Close()
} catch {
    Write-Host "Windows Search error:" $_.Exception.Message
}
