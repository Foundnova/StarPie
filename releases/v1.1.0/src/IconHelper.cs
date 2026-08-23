using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WinPieGestures
{
    public static class IconHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static BitmapSource GetIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                // Expand environment variables if any
                string resolvedPath = Environment.ExpandEnvironmentVariables(path);

                // If path is not found directly on disk, SHGetFileInfo might fail unless we use USEFILEATTRIBUTES
                // Check if file exists, if not we can try passing the attribute flag, or return null
                if (!System.IO.File.Exists(resolvedPath) && !System.IO.Directory.Exists(resolvedPath))
                {
                    // Fallback to system search path or check if it's a known exe name (e.g. notepad.exe)
                    // We can attempt to resolve it or just call SHGetFileInfo with attributes
                    SHFILEINFO shinfoAttr = new SHFILEINFO();
                    IntPtr hImgAttr = SHGetFileInfo(resolvedPath, 256, ref shinfoAttr, (uint)Marshal.SizeOf(shinfoAttr), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
                    if (shinfoAttr.hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                                shinfoAttr.hIcon,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions()
                            );
                            bmpSrc.Freeze();
                            return bmpSrc;
                        }
                        finally
                        {
                            DestroyIcon(shinfoAttr.hIcon);
                        }
                    }
                    return null;
                }

                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hImg = SHGetFileInfo(resolvedPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);

                if (shinfo.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze(); // Crucial for cross-thread access in WPF
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to extract icon for '{path}': {ex.Message}");
            }

            return null;
        }
    }
}
