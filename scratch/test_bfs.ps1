Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class FastBreadthSearch
{
    public static void Search(string query)
    {
        Stopwatch sw = Stopwatch.StartNew();
        var results = new List<string>();

        var ignoreDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin", "System Volume Information", "node_modules", ".git", ".vs", "AppData", "Windows", "WinSxS"
        };

        var roots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            @"G:\", @"H:\", @"I:\", @"K:\"
        };

        var queue = new Queue<(string path, int depth)>();
        foreach (var r in roots.Where(Directory.Exists))
        {
            queue.Enqueue((r, 0));
        }

        while (queue.Count > 0 && results.Count < 50)
        {
            var (currentDir, depth) = queue.Dequeue();
            try
            {
                var dirInfo = new DirectoryInfo(currentDir);
                foreach (var entry in dirInfo.EnumerateFileSystemInfos())
                {
                    if (entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(entry.FullName);
                        if (results.Count >= 50) break;
                    }

                    if (entry is DirectoryInfo subDir && depth < 3)
                    {
                        if (!ignoreDirs.Contains(subDir.Name) && !subDir.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            queue.Enqueue((subDir.FullName, depth + 1));
                        }
                    }
                }
            }
            catch {}
        }

        sw.Stop();
        Console.WriteLine("Fast BFS for '" + query + "': Found " + results.Count + " in " + sw.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
        foreach (var r in results.Take(5))
        {
            Console.WriteLine("   " + r);
        }
    }
}
"@

[FastBreadthSearch]::Search("Everything")
[FastBreadthSearch]::Search("StarPie")
[FastBreadthSearch]::Search("Solid")
