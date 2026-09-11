using System;
using System.IO;
using System.Windows;

namespace Issue17Demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && e.Args[0] == "--render-snapshots")
        {
            var window = new MainWindow();
            window.Show();
            window.Width = 1160;
            window.Height = 780;
            window.UpdateLayout();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string outDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..")); // scratch dir

            // 1. Mode A: Cursor Follow (Sector 6: 系统管理工具)
            window.SimulateHover(6, DisplayMode.CursorFollow, WheelLayoutMode.IconOnly);
            window.SaveSnapshot(Path.Combine(outDir, "issue17_mode_A.png"));

            // 2. Mode B: Outer Anchor (Sector 6: 系统管理工具)
            window.SimulateHover(6, DisplayMode.OuterAnchor, WheelLayoutMode.IconOnly);
            window.SaveSnapshot(Path.Combine(outDir, "issue17_mode_B.png"));

            // 3. Mode C: Center Core (PR #37 现状)
            window.SimulateHover(6, DisplayMode.CenterCore, WheelLayoutMode.IconOnly);
            window.SaveSnapshot(Path.Combine(outDir, "issue17_mode_C.png"));

            // 4. Mode D: Inline Sector (Issue #17 现状文字拥挤溢出)
            window.SimulateHover(6, DisplayMode.InlineSector, WheelLayoutMode.IconAndText);
            window.SaveSnapshot(Path.Combine(outDir, "issue17_mode_D.png"));

            Console.WriteLine("All 4 snapshots generated successfully in " + outDir);
            Shutdown(0);
            return;
        }

        var main = new MainWindow();
        main.Show();
    }
}
