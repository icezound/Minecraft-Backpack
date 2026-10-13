// 从 .exe / .lnk / .url / 文件夹 里取出系统图标, 并缓存成 PNG。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MCBag
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public int bmiColors;
    }

    public static class ShellIcon
    {
        const uint SHGFI_ICON = 0x100;
        const uint SHGFI_LARGEICON = 0x0;
        const uint SHGFI_SYSICONINDEX = 0x4000;
        const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        const int SHIL_LARGE = 0;
        const int SHIL_EXTRALARGE = 2;
        const int SHIL_JUMBO = 4;
        const int ILD_TRANSPARENT = 1;
        const int DIB_RGB_COLORS = 0;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SHGetFileInfoW(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", SetLastError = true)]
        static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint ExtractIconExW(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

        [DllImport("gdi32.dll")]
        static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        private static Bitmap FromHIcon(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero) return null;
            ICONINFO info;
            if (!GetIconInfo(hIcon, out info)) return null;
            int width = 0, height = 0;
            BITMAP bm = new BITMAP();
            if (info.hbmColor != IntPtr.Zero && GetObject(info.hbmColor, Marshal.SizeOf(typeof(BITMAP)), ref bm) != 0)
            {
                width = bm.bmWidth;
                height = bm.bmHeight;
            }
            else if (info.hbmMask != IntPtr.Zero && GetObject(info.hbmMask, Marshal.SizeOf(typeof(BITMAP)), ref bm) != 0)
            {
                width = bm.bmWidth;
                height = bm.bmHeight;
            }
            if (width <= 0 || height <= 0)
            {
                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
                return null;
            }

            IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
            Bitmap result = null;
            try
            {
                BITMAPINFO bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height;   // 负数 = 自顶向下
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;

                byte[] buffer = new byte[width * height * 4];
                bool ok = info.hbmColor != IntPtr.Zero &&
                          GetDIBits(hdc, info.hbmColor, 0, (uint)height, buffer, ref bmi, DIB_RGB_COLORS) != 0;
                if (ok)
                {
                    bool hasAlpha = false;
                    for (int i = 3; i < buffer.Length; i += 4)
                    {
                        if (buffer[i] != 0) { hasAlpha = true; break; }
                    }
                    if (!hasAlpha && info.hbmMask != IntPtr.Zero)
                    {
                        byte[] mask = new byte[width * height * 4];
                        if (GetDIBits(hdc, info.hbmMask, 0, (uint)height, mask, ref bmi, DIB_RGB_COLORS) != 0)
                        {
                            for (int i = 0; i < width * height; i++)
                                buffer[i * 4 + 3] = (byte)(mask[i * 4] > 127 ? 0 : 255);
                        }
                    }
                    Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        for (int y = 0; y < height; y++)
                            Marshal.Copy(buffer, y * width * 4, (IntPtr)((long)data.Scan0 + y * data.Stride), width * 4);
                    }
                    finally { bmp.UnlockBits(data); }
                    result = bmp;
                }
            }
            finally
            {
                if (hdc != IntPtr.Zero) DeleteDC(hdc);
                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
            }
            return result;
        }

        /// <summary>用系统图像列表取高清图标 (256 / 48 / 32)。</summary>
        private static Bitmap FromImageList(string path)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX;
            if (!File.Exists(path) && !Directory.Exists(path)) flags |= SHGFI_USEFILEATTRIBUTES;
            IntPtr res = SHGetFileInfoW(path, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);
            if (res == IntPtr.Zero) return null;
            int[] sizes = new int[] { SHIL_JUMBO, SHIL_EXTRALARGE, SHIL_LARGE };
            foreach (int shil in sizes)
            {
                IImageList list;
                Guid iid = IID_IImageList;
                if (SHGetImageList(shil, ref iid, out list) != 0 || list == null) continue;
                try
                {
                    IntPtr hicon = IntPtr.Zero;
                    if (list.GetIcon(shfi.iIcon, ILD_TRANSPARENT, ref hicon) != 0 || hicon == IntPtr.Zero) continue;
                    try
                    {
                        Bitmap bmp = FromHIcon(hicon);
                        if (bmp != null) return bmp;
                    }
                    finally { DestroyIcon(hicon); }
                }
                finally { Marshal.ReleaseComObject(list); }
            }
            return null;
        }

        private static Bitmap FromExtractIcon(string path)
        {
            IntPtr large, small;
            if (ExtractIconExW(path, 0, out large, out small, 1) == 0) return null;
            try
            {
                Bitmap bmp = FromHIcon(large != IntPtr.Zero ? large : small);
                return bmp;
            }
            finally
            {
                if (large != IntPtr.Zero) DestroyIcon(large);
                if (small != IntPtr.Zero) DestroyIcon(small);
            }
        }

        public static Bitmap Placeholder(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                using (SolidBrush dark = new SolidBrush(Tex.Outline)) g.FillRectangle(dark, 0, 0, size, size);
                using (SolidBrush body = new SolidBrush(Tex.SlotFill)) g.FillRectangle(body, 1, 1, size - 2, size - 2);
                using (SolidBrush light = new SolidBrush(Tex.Hilight)) g.FillRectangle(light, 1, 1, size - 2, 4);
            }
            return bmp;
        }

        /// <summary>取得图标 (一定会返回一张图)。size 为输出像素边长。</summary>
        public static Bitmap Get(string path, int size)
        {
            Bitmap raw = null;
            try
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    raw = FromImageList(path);
                    if (raw == null) raw = FromExtractIcon(path);
                }
                if (raw == null)
                {
                    string ext = Path.GetExtension(path);
                    if (string.IsNullOrEmpty(ext)) ext = ".exe";
                    SHFILEINFO shfi = new SHFILEINFO();
                    if (SHGetFileInfoW("x" + ext, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                                       SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES) != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                    {
                        try { raw = FromHIcon(shfi.hIcon); }
                        finally { DestroyIcon(shfi.hIcon); }
                    }
                }
            }
            catch (Exception) { raw = null; }
            if (raw == null) return Placeholder(size);

            // 裁掉四周多余透明边, 再居中缩放到目标尺寸
            Bitmap cropped = CropTransparent(raw);
            if (raw != null) raw.Dispose();
            int side = Math.Max(cropped.Width, cropped.Height);
            Bitmap square = new Bitmap(side, side, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(square))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                g.DrawImage(cropped, (side - cropped.Width) / 2, (side - cropped.Height) / 2);
            }
            cropped.Dispose();
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(square, 0, 0, size, size);
            }
            square.Dispose();
            return result;
        }

        private static Bitmap CropTransparent(Bitmap src)
        {
            int minX = src.Width, minY = src.Height, maxX = -1, maxY = -1;
            BitmapData data = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[data.Stride];
                for (int y = 0; y < src.Height; y++)
                {
                    Marshal.Copy((IntPtr)((long)data.Scan0 + y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < src.Width; x++)
                    {
                        if (row[x * 4 + 3] > 8)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }
            finally { src.UnlockBits(data); }
            if (maxX < 0) return new Bitmap(src);
            Rectangle r = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return src.Clone(r, PixelFormat.Format32bppArgb);
        }

        public static string CacheKey(string path)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>确保图标已存成 PNG 缓存, 返回缓存文件完整路径。</summary>
        public static string EnsureCached(string path, string cacheDir, int size)
        {
            Directory.CreateDirectory(cacheDir);
            string file = Path.Combine(cacheDir, CacheKey(path) + "_" + size + ".png");
            if (File.Exists(file) && new FileInfo(file).Length > 0) return file;
            using (Bitmap bmp = Get(path, size))
            {
                // 先写临时文件再改名: 界面绝不会读到写了一半的图标
                string tmp = file + ".tmp";
                bmp.Save(tmp, ImageFormat.Png);
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                    File.Move(tmp, file);
                }
                catch (Exception)
                {
                    File.Copy(tmp, file, true);
                    try { File.Delete(tmp); } catch (Exception) { }
                }
            }
            return file;
        }
    }
}
