Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$exe = "g:\Users\2 Better\Desktop\design\scratch\Issue17Demo\bin\Debug\net8.0-windows10.0.19041.0\Issue17Demo.exe"
$proc = Start-Process $exe -PassThru
Start-Sleep -Milliseconds 1500

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$graphics = [System.Drawing.Graphics]::FromImage($bmp)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bmp.Save("g:\Users\2 Better\Desktop\design\scratch\issue17_demo_screenshot.png", [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bmp.Dispose()

Stop-Process -Id $proc.Id -Force
Write-Host "Done"
