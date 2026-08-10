using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using OctoTask.Core.Settings;

namespace OctoTask.Core.Native;

internal static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    public static IntPtr RenderIcon(int value, double percentage, TrayDisplayMode mode)
    {
        int size = GetIconSize();
        Bitmap? bitmap = null;
        IntPtr hbmColor = IntPtr.Zero;
        IntPtr hbmMask = IntPtr.Zero;
        IntPtr hIcon = IntPtr.Zero;

        try
        {
            bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            float scale = size / 16f;

            using var bgBrush = new SolidBrush(Color.FromArgb(28, 28, 32));
            g.FillRectangle(bgBrush, 0, 0, size, size);

            Color arcColor = GetArcColor(percentage);
            using var arcPen = new Pen(arcColor, 2.5f * scale);
            float arcInset = 2 * scale;
            float arcSize = size - 5 * scale;
            float sweepAngle = (float)(Math.Min(percentage, 100) * 3.6);
            g.DrawArc(arcPen, arcInset, arcInset, arcSize, arcSize, -90, sweepAngle);

            string text = value >= 100 ? "99" : value.ToString("F0");
            float fontSize = text.Length switch
            {
                1 => 7.5f * scale,
                2 => 6.5f * scale,
                _ => 5.5f * scale
            };

            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var textSize = g.MeasureString(text, font);
            float tx = (size - textSize.Width) / 2;
            float ty = (size - textSize.Height) / 2;
            g.DrawString(text, font, textBrush, tx, ty);

            hbmColor = bitmap.GetHbitmap(Color.FromArgb(0, 0, 0, 0));

            var maskBitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
            hbmMask = maskBitmap.GetHbitmap();

            var iconInfo = new ICONINFO
            {
                fIcon = true,
                xHotspot = 0,
                yHotspot = 0,
                hbmMask = hbmMask,
                hbmColor = hbmColor
            };

            hIcon = CreateIconIndirect(ref iconInfo);
            return hIcon;
        }
        catch
        {
            if (hIcon != IntPtr.Zero) DestroyIconIndirect(hIcon);
            return IntPtr.Zero;
        }
        finally
        {
            if (hbmColor != IntPtr.Zero) DeleteObject(hbmColor);
            if (hbmMask != IntPtr.Zero) DeleteObject(hbmMask);
            bitmap?.Dispose();
        }
    }

    internal static void DestroyIconIndirect(IntPtr hIcon)
    {
        if (hIcon != IntPtr.Zero)
            DestroyIcon(hIcon);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static int GetIconSize()
    {
        try
        {
            using var screen = Graphics.FromHwnd(IntPtr.Zero);
            float dpi = screen.DpiX;
            return dpi > 120 ? 32 : 16;
        }
        catch
        {
            return 16;
        }
    }

    private static Color GetArcColor(double percentage)
    {
        return percentage switch
        {
            > 80 => Color.FromArgb(239, 68, 68),
            > 50 => Color.FromArgb(245, 158, 11),
            _ => Color.FromArgb(59, 130, 246)
        };
    }
}
