Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class NativeSearchBenchmark
{
    public static void Run(string query)
    {
        Stopwatch sw = Stopwatch.StartNew();
        var results = new List<string>();

        // 1. Desktop & User Profile
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloads = Path.Combine(userProfile, "Downloads");
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        string[] quickRoots = new[] { desktop, downloads, docs };

        foreach (var root in quickRoots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                var files = Directory.EnumerateFileSystemEntries(root, "*" + query + "*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    results.Add(f);
                    if (results.Count >= 50) break;
                }
            }
            catch {}
            if (results.Count >= 50) break;
        }

        sw.Stop();
        Console.WriteLine("Found " + results.Count + " items in " + sw.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
        foreach (var r in results.Take(10))
        {
            Console.WriteLine("   " + r);
        }
    }
}
"@

[NativeSearchBenchmark]::Run("StarPie")
[NativeSearchBenchmark]::Run("Everything")
[NativeSearchBenchmark]::Run("exe")
