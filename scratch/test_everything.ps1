$bytes = [System.IO.File]::ReadAllBytes('G:\Users\2 Better\Desktop\Everything.exe')
$pe = [BitConverter]::ToInt32($bytes, 0x3c)
$m = [BitConverter]::ToUInt16($bytes, $pe + 4)
Write-Host ("Machine: {0:X4}" -f $m)
if ($m -eq 0x8664) {
    Write-Host "Everything.exe is 64-bit (x64)"
} elseif ($m -eq 0x014c) {
    Write-Host "Everything.exe is 32-bit (x86)!"
}
