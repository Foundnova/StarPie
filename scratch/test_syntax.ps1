$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "G:\Users\2 Better\Desktop\Everything.exe"
$psi.Arguments = "-admin"
$proc = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 1

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class SyntaxTest
{
    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll")]
    public static extern void Everything_SetMax(uint dwMax);

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll")]
    public static extern bool Everything_QueryW(bool bWait);

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll")]
    public static extern uint Everything_GetNumResults();

    [DllImport(@"g:\Users\2 Better\Desktop\design\WinPieGestures\Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

    public static void Test(string q)
    {
        Everything_SetSearchW(q);
        Everything_SetMax(5);
        bool ok = Everything_QueryW(true);
        uint count = Everything_GetNumResults();
        Console.WriteLine("Query: [" + q + "] -> OK=" + ok + ", Count=" + count);
        if (count > 0)
        {
            StringBuilder sb = new StringBuilder(512);
            Everything_GetResultFullPathNameW(0, sb, 512);
            Console.WriteLine("   First: " + sb.ToString());
        }
    }
}
"@

[SyntaxTest]::Test("a")
[SyntaxTest]::Test("a !$Recycle.Bin !\\Windows\\WinSxS\\")
[SyntaxTest]::Test("!$Recycle.Bin !\\Windows\\WinSxS\\")
[SyntaxTest]::Test("ext:exe;bat")
[SyntaxTest]::Test("ext:exe")
