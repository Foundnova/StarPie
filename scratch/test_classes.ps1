$bytes = [System.IO.File]::ReadAllBytes('WinPieGestures\Everything64.dll')
$str = [System.Text.Encoding]::Unicode.GetString($bytes)
[regex]::Matches($str, 'EVERYTHING[A-Za-z0-9_]*') | ForEach-Object { $_.Value } | Sort-Object -Unique
$strA = [System.Text.Encoding]::ASCII.GetString($bytes)
[regex]::Matches($strA, 'EVERYTHING[A-Za-z0-9_]*') | ForEach-Object { $_.Value } | Sort-Object -Unique
