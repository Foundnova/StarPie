using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace IconGenerator;

class Program
{
    static unsafe void Main(string[] args)
    {
        string inputPath = @"g:\Users\2 Better\Desktop\design\attachments\cover.v3.png";
        string outputLogo = @"g:\Users\2 Better\Desktop\design\WinPieGestures\logo.png";
        string outputAssetsLogo = @"g:\Users\2 Better\Desktop\design\assets\logo.png";
        string outputLogoDark = @"g:\Users\2 Better\Desktop\design\WinPieGestures\logo_dark.png";
        string outputAssetsLogoDark = @"g:\Users\2 Better\Desktop\design\assets\logo_dark.png";
        string outputLogoLight = @"g:\Users\2 Better\Desktop\design\WinPieGestures\logo_light.png";
        string outputAssetsLogoLight = @"g:\Users\2 Better\Desktop\design\assets\logo_light.png";
        string outputIco = @"g:\Users\2 Better\Desktop\design\WinPieGestures\app_icon.ico";
        string outputAssetsIco = @"g:\Users\2 Better\Desktop\design\assets\app_icon.ico";
        string outputTrayIco = @"g:\Users\2 Better\Desktop\design\WinPieGestures\tray_icon.ico";
        string outputAssetsTrayIco = @"g:\Users\2 Better\Desktop\design\assets\tray_icon.ico";
        string outputPreviewPng = @"g:\Users\2 Better\Desktop\design\scratch\icon_clarity_preview.png";

        Console.WriteLine("Loading source image: " + inputPath);
        using var srcBmp = new Bitmap(inputPath);
        int w = srcBmp.Width;
        int h = srcBmp.Height;

        Console.WriteLine($"Image size: {w}x{h}, format: {srcBmp.PixelFormat}");

        // Convert source to 32bpp ARGB in-memory byte buffer
        byte[] srcPixels = new byte[w * h * 4];
        using (var bmp32 = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp32))
            {
                g.DrawImage(srcBmp, 0, 0, w, h);
            }

            var srcData = bmp32.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(srcData.Scan0, srcPixels, 0, srcPixels.Length);
            bmp32.UnlockBits(srcData);
        }

        // Step 1: Detect background via flood fill from 4 borders
        bool[,] isBg = new bool[w, h];
        var queue = new Queue<int>(w * 4);

        void EnqueueIfBg(int x, int y)
        {
            if (x < 0 || x >= w || y < 0 || y >= h || isBg[x, y]) return;
            int idx = (y * w + x) * 4;
            byte b = srcPixels[idx];
            byte g = srcPixels[idx + 1];
            byte r = srcPixels[idx + 2];
            double lum = 0.299 * r + 0.587 * g + 0.114 * b;
            if (lum <= 10.0)
            {
                isBg[x, y] = true;
                queue.Enqueue(y * w + x);
            }
        }

        for (int x = 0; x < w; x++)
        {
            EnqueueIfBg(x, 0);
            EnqueueIfBg(x, h - 1);
        }
        for (int y = 1; y < h - 1; y++)
        {
            EnqueueIfBg(0, y);
            EnqueueIfBg(w - 1, y);
        }

        while (queue.Count > 0)
        {
            int pos = queue.Dequeue();
            int px = pos % w;
            int py = pos / w;

            EnqueueIfBg(px + 1, py);
            EnqueueIfBg(px - 1, py);
            EnqueueIfBg(px, py + 1);
            EnqueueIfBg(px, py - 1);
        }

        Console.WriteLine("Flood fill completed. Generating anti-aliased 32bpp ARGB bitmap...");

        // Anti-aliasing radius: 2px filter (5x5 kernel)
        int kernelRadius = 2;
        int kernelSize = kernelRadius * 2 + 1;
        float totalWeights = 0f;
        float[,] weights = new float[kernelSize, kernelSize];
        for (int ky = -kernelRadius; ky <= kernelRadius; ky++)
        {
            for (int kx = -kernelRadius; kx <= kernelRadius; kx++)
            {
                float dist = MathF.Sqrt(kx * kx + ky * ky);
                float weight = MathF.Max(0f, 1f - dist / (kernelRadius + 0.5f));
                weights[kx + kernelRadius, ky + kernelRadius] = weight;
                totalWeights += weight;
            }
        }

        byte[] destPixels = new byte[w * h * 4];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = (y * w + x) * 4;
                byte b = srcPixels[idx];
                byte g = srcPixels[idx + 1];
                byte r = srcPixels[idx + 2];

                if (isBg[x, y])
                {
                    // Check if near border for anti-aliasing
                    bool nearForeground = false;
                    for (int dy = -kernelRadius; dy <= kernelRadius && !nearForeground; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            if (!isBg[nx, ny])
                            {
                                nearForeground = true;
                                break;
                            }
                        }
                    }

                    if (!nearForeground)
                    {
                        destPixels[idx] = 0;
                        destPixels[idx + 1] = 0;
                        destPixels[idx + 2] = 0;
                        destPixels[idx + 3] = 0; // Alpha = 0
                    }
                    else
                    {
                        float sum = 0f;
                        for (int dy = -kernelRadius; dy <= kernelRadius; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) continue;
                            for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w) continue;
                                if (!isBg[nx, ny])
                                {
                                    sum += weights[dx + kernelRadius, dy + kernelRadius];
                                }
                            }
                        }
                        byte alpha = (byte)Math.Clamp((int)(255f * (sum / totalWeights)), 0, 255);
                        destPixels[idx] = b;
                        destPixels[idx + 1] = g;
                        destPixels[idx + 2] = r;
                        destPixels[idx + 3] = alpha;
                    }
                }
                else
                {
                    // Inside foreground: check if near background for edge smoothing
                    bool nearBg = false;
                    for (int dy = -kernelRadius; dy <= kernelRadius && !nearBg; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            if (isBg[nx, ny])
                            {
                                nearBg = true;
                                break;
                            }
                        }
                    }

                    if (!nearBg)
                    {
                        destPixels[idx] = b;
                        destPixels[idx + 1] = g;
                        destPixels[idx + 2] = r;
                        destPixels[idx + 3] = 255;
                    }
                    else
                    {
                        float sum = 0f;
                        for (int dy = -kernelRadius; dy <= kernelRadius; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) continue;
                            for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w) continue;
                                if (!isBg[nx, ny])
                                {
                                    sum += weights[dx + kernelRadius, dy + kernelRadius];
                                }
                            }
                        }
                        byte alpha = (byte)Math.Clamp((int)(255f * (sum / totalWeights)), 0, 255);
                        destPixels[idx] = b;
                        destPixels[idx + 1] = g;
                        destPixels[idx + 2] = r;
                        destPixels[idx + 3] = alpha;
                    }
                }
            }
        }

        using var destBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var destData = destBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(destPixels, 0, destData.Scan0, destPixels.Length);
        destBmp.UnlockBits(destData);

        Console.WriteLine("Saving logo.png...");
        destBmp.Save(outputLogo, ImageFormat.Png);
        destBmp.Save(outputAssetsLogo, ImageFormat.Png);
        Console.WriteLine("Saved logo.png successfully to " + outputLogo);

        // Step 2: Build Pure Circular Wheel (100% Transparent Background, No Squircle Border)
        float cx = w / 2f;
        float cy = h / 2f;
        float wheelRadius = 450f;

        using var wheelOnlyBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        byte[] wheelPixels = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            float dy = y - cy;
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                int idx = (y * w + x) * 4;

                if (dist <= wheelRadius + 2f)
                {
                    float alphaFactor = 1f;
                    if (dist > wheelRadius - 2f)
                    {
                        alphaFactor = Math.Clamp((wheelRadius + 2f - dist) / 4f, 0f, 1f);
                    }
                    wheelPixels[idx] = destPixels[idx];
                    wheelPixels[idx + 1] = destPixels[idx + 1];
                    wheelPixels[idx + 2] = destPixels[idx + 2];
                    wheelPixels[idx + 3] = (byte)(destPixels[idx + 3] * alphaFactor);
                }
            }
        }
        var wheelData = wheelOnlyBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(wheelPixels, 0, wheelData.Scan0, wheelPixels.Length);
        wheelOnlyBmp.UnlockBits(wheelData);

        // Crop tight circular wheel (100% transparent outside wheel radius)
        int wheelCropSize = (int)(wheelRadius * 2 + 8);
        int cropX = (int)(cx - wheelRadius - 4);
        int cropY = (int)(cy - wheelRadius - 4);
        using var tightWheelBmp = new Bitmap(wheelCropSize, wheelCropSize, PixelFormat.Format32bppArgb);
        using (var gTight = Graphics.FromImage(tightWheelBmp))
        {
            gTight.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gTight.SmoothingMode = SmoothingMode.HighQuality;
            gTight.PixelOffsetMode = PixelOffsetMode.HighQuality;
            gTight.CompositingQuality = CompositingQuality.HighQuality;
            gTight.Clear(Color.Transparent);
            gTight.DrawImage(wheelOnlyBmp, new Rectangle(0, 0, wheelCropSize, wheelCropSize), new Rectangle(cropX, cropY, wheelCropSize, wheelCropSize), GraphicsUnit.Pixel);
        }

        // Generate Light Edition Wheel (optimized for pure white / light backgrounds with pearlescent center & sapphire blue pointer)
        using var lightWheelBmp = CreateLightEditionWheel(tightWheelBmp);

        // Save high-resolution logos (both Dark Edition and Light Edition)
        Console.WriteLine("Saving logo_dark.png, logo_light.png, and logo.png...");
        tightWheelBmp.Save(outputLogoDark, ImageFormat.Png);
        tightWheelBmp.Save(outputAssetsLogoDark, ImageFormat.Png);
        tightWheelBmp.Save(outputLogo, ImageFormat.Png);
        tightWheelBmp.Save(outputAssetsLogo, ImageFormat.Png);

        lightWheelBmp.Save(outputLogoLight, ImageFormat.Png);
        lightWheelBmp.Save(outputAssetsLogoLight, ImageFormat.Png);
        Console.WriteLine("Saved dual-theme logos successfully.");

        // Step 3: Multi-tier downscaling with Unsharp Masking (USM) for app_icon.ico
        // CRUCIAL: Every resolution (16 to 256) is built from tightWheelBmp (pure circular wheel on transparent background)
        // This permanently eliminates any black / squircle background box from the Windows Taskbar!
        int[] sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };
        var iconFrames = new Dictionary<int, Bitmap>();

        using var appMip256 = ResizeHighQuality(tightWheelBmp, 256, 256);
        using var appMip128 = ResizeHighQuality(appMip256, 128, 128);

        foreach (int size in sizes)
        {
            Bitmap frame;
            if (size >= 128)
            {
                frame = ResizeHighQuality(tightWheelBmp, size, size);
            }
            else if (size >= 48)
            {
                frame = ResizeHighQuality(appMip256, size, size);
                frame = ApplyUnsharpMask(frame, 0.20f);
                frame = AdjustContrast(frame, 1.08f, 1.10f);
            }
            else
            {
                // Micro / Taskbar sizes (16, 20, 24, 32, 40)
                frame = ResizeHighQuality(appMip128, size, size);
                float sharpAmount = size switch
                {
                    16 => 0.40f,
                    20 => 0.35f,
                    24 => 0.30f,
                    32 => 0.25f,
                    _ => 0.22f
                };
                frame = ApplyUnsharpMask(frame, sharpAmount);
                frame = AdjustContrast(frame, 1.15f, 1.20f);
            }
            iconFrames[size] = frame;
        }

        // Step 4a: Generate Dedicated Pure Circular Wheel Tray Icon (tray_icon.ico)
        int[] traySizes = new int[] { 16, 20, 24, 32, 48, 64 };
        var trayFrames = new Dictionary<int, Bitmap>();

        foreach (int size in traySizes)
        {
            var frame = ResizeHighQuality(appMip128, size, size);
            float sharp = size switch
            {
                16 => 0.42f,
                20 => 0.36f,
                24 => 0.30f,
                _ => 0.22f
            };
            frame = ApplyUnsharpMask(frame, sharp);
            frame = AdjustContrast(frame, 1.16f, 1.22f);
            trayFrames[size] = frame;
        }

        Console.WriteLine("Packaging dedicated tray_icon.ico...");
        WriteWin32Ico(trayFrames, outputTrayIco);
        File.Copy(outputTrayIco, outputAssetsTrayIco, true);
        Console.WriteLine("Saved tray_icon.ico to " + outputTrayIco);

        // Step 4b: Write standard Win32 multi-resolution ICO file (app_icon.ico)
        // Sizes <= 48 written as 32bpp DIB (with alpha), Sizes >= 64 written as PNG
        Console.WriteLine("Packaging standard Win32 multi-resolution ICO (100% transparent circular wheel)...");
        WriteWin32Ico(iconFrames, outputIco);
        File.Copy(outputIco, outputAssetsIco, true);
        Console.WriteLine("Saved borderless app_icon.ico to " + outputIco);

        // Step 5: Generate Comparison Preview PNG for visual verification
        GenerateClarityComparison(destBmp, iconFrames, trayFrames, outputPreviewPng);
        Console.WriteLine("Saved comparison preview to " + outputPreviewPng);

        // Clean up frames
        foreach (var kvp in iconFrames)
        {
            kvp.Value.Dispose();
        }
        foreach (var kvp in trayFrames)
        {
            kvp.Value.Dispose();
        }
    }

    static Bitmap ResizeHighQuality(Bitmap src, int targetW, int targetH)
    {
        var result = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(src, new Rectangle(0, 0, targetW, targetH));
        }
        return result;
    }

    static unsafe Bitmap ApplyUnsharpMask(Bitmap src, float amount)
    {
        int w = src.Width;
        int h = src.Height;
        var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        var srcData = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        byte* pSrc = (byte*)srcData.Scan0;
        byte* pDst = (byte*)dstData.Scan0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * srcData.Stride + x * 4;
                byte a = pSrc[idx + 3];

                if (a == 0)
                {
                    pDst[idx] = 0;
                    pDst[idx + 1] = 0;
                    pDst[idx + 2] = 0;
                    pDst[idx + 3] = 0;
                    continue;
                }

                // 3x3 Laplacian edge detection
                float bSum = 0, gSum = 0, rSum = 0;
                int count = 0;

                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = Math.Clamp(y + dy, 0, h - 1);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = Math.Clamp(x + dx, 0, w - 1);
                        int nidx = ny * srcData.Stride + nx * 4;
                        bSum += pSrc[nidx];
                        gSum += pSrc[nidx + 1];
                        rSum += pSrc[nidx + 2];
                        count++;
                    }
                }

                float bAvg = bSum / count;
                float gAvg = gSum / count;
                float rAvg = rSum / count;

                float bOrig = pSrc[idx];
                float gOrig = pSrc[idx + 1];
                float rOrig = pSrc[idx + 2];

                float bSharp = bOrig + amount * (bOrig - bAvg);
                float gSharp = gOrig + amount * (gOrig - gAvg);
                float rSharp = rOrig + amount * (rOrig - rAvg);

                pDst[idx] = (byte)Math.Clamp((int)MathF.Round(bSharp), 0, 255);
                pDst[idx + 1] = (byte)Math.Clamp((int)MathF.Round(gSharp), 0, 255);
                pDst[idx + 2] = (byte)Math.Clamp((int)MathF.Round(rSharp), 0, 255);
                pDst[idx + 3] = a;
            }
        }

        src.UnlockBits(srcData);
        result.UnlockBits(dstData);
        src.Dispose();
        return result;
    }

    static unsafe Bitmap AdjustContrast(Bitmap src, float contrast, float saturation)
    {
        int w = src.Width;
        int h = src.Height;
        var data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        byte* p = (byte*)data.Scan0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * data.Stride + x * 4;
                byte a = p[idx + 3];
                if (a == 0) continue;

                float b = p[idx] / 255f;
                float g = p[idx + 1] / 255f;
                float r = p[idx + 2] / 255f;

                // Contrast
                r = (r - 0.5f) * contrast + 0.5f;
                g = (g - 0.5f) * contrast + 0.5f;
                b = (b - 0.5f) * contrast + 0.5f;

                // Saturation
                float gray = 0.299f * r + 0.587f * g + 0.114f * b;
                r = gray + (r - gray) * saturation;
                g = gray + (g - gray) * saturation;
                b = gray + (b - gray) * saturation;

                p[idx] = (byte)Math.Clamp((int)MathF.Round(b * 255f), 0, 255);
                p[idx + 1] = (byte)Math.Clamp((int)MathF.Round(g * 255f), 0, 255);
                p[idx + 2] = (byte)Math.Clamp((int)MathF.Round(r * 255f), 0, 255);
            }
        }

        src.UnlockBits(data);
        return src;
    }

    static void WriteWin32Ico(Dictionary<int, Bitmap> frames, string icoPath)
    {
        int[] sortedSizes = frames.Keys.OrderBy(x => x).ToArray();
        var rawDataList = new List<byte[]>();

        foreach (int size in sortedSizes)
        {
            Bitmap bmp = frames[size];
            if (size <= 48)
            {
                // Uncompressed 32bpp DIB
                rawDataList.Add(CreateDibIconImage(bmp));
            }
            else
            {
                // PNG compressed
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                rawDataList.Add(ms.ToArray());
            }
        }

        using var fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICONDIR header
        bw.Write((ushort)0); // Reserved
        bw.Write((ushort)1); // Type = 1 (ICO)
        bw.Write((ushort)sortedSizes.Length); // Image count

        int offset = 6 + sortedSizes.Length * 16;

        for (int i = 0; i < sortedSizes.Length; i++)
        {
            int s = sortedSizes[i];
            byte wByte = (byte)(s >= 256 ? 0 : s);
            byte hByte = (byte)(s >= 256 ? 0 : s);
            byte colors = 0;
            byte reserved = 0;
            ushort planes = 1;
            ushort bpp = 32;
            uint bytesInRes = (uint)rawDataList[i].Length;

            bw.Write(wByte);
            bw.Write(hByte);
            bw.Write(colors);
            bw.Write(reserved);
            bw.Write(planes);
            bw.Write(bpp);
            bw.Write(bytesInRes);
            bw.Write((uint)offset);

            offset += (int)bytesInRes;
        }

        for (int i = 0; i < sortedSizes.Length; i++)
        {
            bw.Write(rawDataList[i]);
        }
    }

    static unsafe byte[] CreateDibIconImage(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;

        int headerSize = 40; // BITMAPINFOHEADER
        int pixelDataSize = w * h * 4;
        int andMaskRowStride = ((w + 31) / 32) * 4; // 32-bit aligned row
        int andMaskSize = andMaskRowStride * h;
        int totalSize = headerSize + pixelDataSize + andMaskSize;

        byte[] dib = new byte[totalSize];

        // BITMAPINFOHEADER
        using var ms = new MemoryStream(dib);
        using var bw = new BinaryWriter(ms);

        bw.Write((uint)40);        // biSize
        bw.Write((int)w);           // biWidth
        bw.Write((int)(h * 2));     // biHeight (doubled for XOR + AND mask in icons)
        bw.Write((ushort)1);        // biPlanes
        bw.Write((ushort)32);       // biBitCount
        bw.Write((uint)0);         // biCompression (BI_RGB)
        bw.Write((uint)(pixelDataSize + andMaskSize)); // biSizeImage
        bw.Write((int)0);           // biXPelsPerMeter
        bw.Write((int)0);           // biYPelsPerMeter
        bw.Write((uint)0);         // biClrUsed
        bw.Write((uint)0);         // biClrImportant

        // XOR pixel data (bottom-to-top BGRA)
        var srcData = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte* pSrc = (byte*)srcData.Scan0;

        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                int srcIdx = y * srcData.Stride + x * 4;
                bw.Write(pSrc[srcIdx]);     // B
                bw.Write(pSrc[srcIdx + 1]); // G
                bw.Write(pSrc[srcIdx + 2]); // R
                bw.Write(pSrc[srcIdx + 3]); // A
            }
        }

        bmp.UnlockBits(srcData);

        // AND mask (bottom-to-top 1bpp, 0 for opaque/visible, 1 for transparent)
        byte[] andRow = new byte[andMaskRowStride];
        for (int y = 0; y < h; y++)
        {
            bw.Write(andRow);
        }

        return dib;
    }

    static void GenerateClarityComparison(Bitmap fullSquircle, Dictionary<int, Bitmap> enhancedFrames, Dictionary<int, Bitmap> trayFrames, string previewPath)
    {
        int[] compareSizes = new int[] { 16, 20, 24, 32, 48 };
        int zoom = 4;
        int padding = 16;
        int colWidth = 48 * zoom + padding * 2;
        int totalW = colWidth * compareSizes.Length + padding;
        int totalH = 610;

        using var preview = new Bitmap(totalW, totalH, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(preview);
        g.Clear(Color.FromArgb(255, 24, 24, 27)); // Modern dark taskbar color #18181B

        using var fontTitle = new Font("Segoe UI", 11, FontStyle.Bold);
        using var fontDesc = new Font("Segoe UI", 9, FontStyle.Regular);
        using var brushWhite = new SolidBrush(Color.White);
        using var brushGray = new SolidBrush(Color.FromArgb(160, 160, 175));
        using var brushCyan = new SolidBrush(Color.FromArgb(56, 189, 248));
        using var brushPill = new SolidBrush(Color.FromArgb(255, 59, 130, 246));

        g.DrawString("StarPie 任务栏与托盘图标清晰度重构对比 (Clarity Comparison on Taskbar Background)", fontTitle, brushWhite, 16, 12);
        g.DrawString("排 1：旧版直接粗暴缩小（模糊/泛灰/细节湮灭）\n排 2：任务栏专属重构（超大轮盘/微标锐化/高对比度/DIB原生帧）\n排 3：系统托盘专属微标（纯圆星盘/零边框留白/晶莹剔透/极致清晰）", fontDesc, brushGray, 16, 36);

        for (int i = 0; i < compareSizes.Length; i++)
        {
            int s = compareSizes[i];
            int x = padding + i * colWidth;

            // Header
            g.DrawString($"{s}×{s} ({s switch { 16 => "托盘 100%", 20 => "托盘 125%", 24 => "任务栏 100%", 32 => "任务栏 125%", _ => "任务栏 200%" }})", fontDesc, brushCyan, x, 100);

            // Row 1: Naive downscale (Old)
            using var oldSmall = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var gOld = Graphics.FromImage(oldSmall))
            {
                gOld.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gOld.Clear(Color.Transparent);
                gOld.DrawImage(fullSquircle, 0, 0, s, s);
            }

            // Draw Old 1:1
            g.DrawImage(oldSmall, x, 135, s, s);
            // Draw Old zoomed
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(oldSmall, x + 55, 125, s * zoom, s * zoom);

            // Row 2: Enhanced Squircle (Taskbar)
            Bitmap newSmall = enhancedFrames[s];
            // Draw New 1:1
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(newSmall, x, 285, s, s);
            // Draw active taskbar pill underneath 1:1 on 24/32
            if (s == 24 || s == 32)
            {
                using var path = new GraphicsPath();
                float px = x + (s - 14) / 2f, py = 285 + s + 3f, pw = 14f, ph = 3f, pr = 1.5f;
                path.AddArc(px, py, pr * 2, pr * 2, 180, 90);
                path.AddArc(px + pw - pr * 2, py, pr * 2, pr * 2, 270, 90);
                path.AddArc(px + pw - pr * 2, py + ph - pr * 2, pr * 2, pr * 2, 0, 90);
                path.AddArc(px, py + ph - pr * 2, pr * 2, pr * 2, 90, 90);
                path.CloseFigure();
                g.FillPath(brushPill, path);
            }

            // Draw New zoomed
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(newSmall, x + 55, 275, s * zoom, s * zoom);

            // Row 3: Dedicated Tray Icon (Pure Wheel)
            if (trayFrames.TryGetValue(s, out Bitmap? traySmall))
            {
                // Draw Tray 1:1
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(traySmall, x, 435, s, s);
                // Draw Tray zoomed
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(traySmall, x + 55, 425, s * zoom, s * zoom);
            }
        }

        preview.Save(previewPath, ImageFormat.Png);
    }

    static unsafe Bitmap CreateLightEditionWheel(Bitmap darkWheel)
    {
        int w = darkWheel.Width;
        int h = darkWheel.Height;
        float cx = w / 2f;
        float cy = h / 2f;
        // Core radius is approx 186px when w=908 (ratio ~ 0.205)
        float coreRadius = w * 0.205f;

        var lightBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var srcData = darkWheel.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dstData = lightBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        byte* pSrc = (byte*)srcData.Scan0;
        byte* pDst = (byte*)dstData.Scan0;

        for (int y = 0; y < h; y++)
        {
            float dy = y - cy;
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                int idx = y * srcData.Stride + x * 4;

                byte b = pSrc[idx];
                byte g = pSrc[idx + 1];
                byte r = pSrc[idx + 2];
                byte a = pSrc[idx + 3];

                if (a == 0)
                {
                    pDst[idx] = 0;
                    pDst[idx + 1] = 0;
                    pDst[idx + 2] = 0;
                    pDst[idx + 3] = 0;
                    continue;
                }

                // Core region mask (smooth edge 2px)
                float coreFactor = Math.Clamp((coreRadius + 1.5f - dist) / 3f, 0f, 1f);

                if (coreFactor > 0f)
                {
                    float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                    // Pointer mask: in darkWheel, pointer is bright white/pink (lum > 125)
                    float pointerFactor = Math.Clamp((lum - 125f) / 35f, 0f, 1f);

                    // Pearlescent white/silver core background (#FFFFFF at top to #E2E8F0 at bottom)
                    float gradY = (float)y / h;
                    float bgR = 250f - 18f * gradY;
                    float bgG = 252f - 16f * gradY;
                    float bgB = 255f - 10f * gradY;

                    // Vivid Sapphire Blue pointer (#2563EB to #1D4ED8)
                    float ptR = 37f - 8f * gradY;
                    float ptG = 99f - 12f * gradY;
                    float ptB = 235f - 19f * gradY;

                    float blendedR = bgR * (1f - pointerFactor) + ptR * pointerFactor;
                    float blendedG = bgG * (1f - pointerFactor) + ptG * pointerFactor;
                    float blendedB = bgB * (1f - pointerFactor) + ptB * pointerFactor;

                    // Blend with original base according to coreFactor
                    byte finalR = (byte)Math.Clamp((int)MathF.Round(r * (1f - coreFactor) + blendedR * coreFactor), 0, 255);
                    byte finalG = (byte)Math.Clamp((int)MathF.Round(g * (1f - coreFactor) + blendedG * coreFactor), 0, 255);
                    byte finalB = (byte)Math.Clamp((int)MathF.Round(b * (1f - coreFactor) + blendedB * coreFactor), 0, 255);

                    pDst[idx] = finalB;
                    pDst[idx + 1] = finalG;
                    pDst[idx + 2] = finalR;
                    pDst[idx + 3] = a;
                }
                else
                {
                    // Outer sectors: slight saturation & contrast boost for light background clarity
                    float bF = b / 255f;
                    float gF = g / 255f;
                    float rF = r / 255f;

                    // Contrast
                    rF = (rF - 0.5f) * 1.08f + 0.5f;
                    gF = (gF - 0.5f) * 1.08f + 0.5f;
                    bF = (bF - 0.5f) * 1.08f + 0.5f;

                    // Saturation
                    float gray = 0.299f * rF + 0.587f * gF + 0.114f * bF;
                    rF = gray + (rF - gray) * 1.15f;
                    gF = gray + (gF - gray) * 1.15f;
                    bF = gray + (bF - gray) * 1.15f;

                    pDst[idx] = (byte)Math.Clamp((int)MathF.Round(bF * 255f), 0, 255);
                    pDst[idx + 1] = (byte)Math.Clamp((int)MathF.Round(gF * 255f), 0, 255);
                    pDst[idx + 2] = (byte)Math.Clamp((int)MathF.Round(rF * 255f), 0, 255);
                    pDst[idx + 3] = a;
                }
            }
        }

        darkWheel.UnlockBits(srcData);
        lightBmp.UnlockBits(dstData);
        return lightBmp;
    }
}

