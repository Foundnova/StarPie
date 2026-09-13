$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "G:\Users\2 Better\Desktop\Everything.exe"
$psi.UseShellExecute = $true
$proc = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 2

Write-Host "Process running: " (Get-Process Everything -ErrorAction SilentlyContinue)

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class TestEverythingLive
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll")]
    public static extern void Everything_SetMax(uint dwMax);

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll")]
    public static extern bool Everything_QueryW(bool bWait);

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll")]
    public static extern uint Everything_GetNumResults();

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll")]
    public static extern uint Everything_GetLastError();

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

    public static void Check()
    {
        IntPtr h1 = FindWindow("EVERYTHING_TASKBAR_NOTIFICATION", null);
        IntPtr h2 = FindWindow("EVERYTHING", null);
        Console.WriteLine("HWND notification: " + h1 + ", HWND main: " + h2);

        // Test 1: empty
        Everything_SetSearchW("");
        Everything_SetMax(5);
        bool q1 = Everything_QueryW(true);
        Console.WriteLine("Test 1 (empty): Query=" + q1 + ", Err=" + Everything_GetLastError() + ", Count=" + Everything_GetNumResults());

        // Test 2: 'test'
        Everything_SetSearchW("test");
        Everything_SetMax(5);
        bool q2 = Everything_QueryW(true);
        Console.WriteLine("Test 2 ('test'): Query=" + q2 + ", Err=" + Everything_GetLastError() + ", Count=" + Everything_GetNumResults());

        // Test 3: with junk exclude
        Everything_SetSearchW("test !$Recycle.Bin !\\Windows\\WinSxS\\");
        Everything_SetMax(5);
        bool q3 = Everything_QueryW(true);
        Console.WriteLine("Test 3 ('test with junk'): Query=" + q3 + ", Err=" + Everything_GetLastError() + ", Count=" + Everything_GetNumResults());

        // Test 4: empty with junk exclude
        Everything_SetSearchW("!$Recycle.Bin !\\Windows\\WinSxS\\");
        Everything_SetMax(5);
        bool q4 = Everything_QueryW(true);
        Console.WriteLine("Test 4 (junk only): Query=" + q4 + ", Err=" + Everything_GetLastError() + ", Count=" + Everything_GetNumResults());
    }
}
"@

[TestEverythingLive]::Check()
