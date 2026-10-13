// 角色皮肤: 用 64x64 的皮肤贴图渲染出游戏里那种会慢慢转动的立体小像。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MCBag
{
    /// <summary>皮肤里的一个部件 (头/身/手臂/腿), 带标准 UV 坐标。</summary>
    public class SkinPart
    {
        public string Name;
        public int W, H, D;
        public double CenterX;      // 在角色坐标里的横向位置 (0 = 身体中线)
        public double BottomY;      // 部件下沿 (0 = 脚底)
        public Rectangle Front, Back, Left, Right, Top;
        public Point OverlayShift;  // 第二层贴图 (帽子/外套) 相对本层的偏移

        public SkinPart(string name, int w, int h, int d, double cx, double bottomY,
                        Rectangle front, Rectangle back, Rectangle left, Rectangle right, Rectangle top,
                        Point overlayShift)
        {
            Name = name;
            W = w;
            H = h;
            D = d;
            CenterX = cx;
            BottomY = bottomY;
            Front = front;
            Back = back;
            Left = left;
            Right = right;
            Top = top;
            OverlayShift = overlayShift;
        }
    }

    public class Skin
    {
        public Bitmap Texture;      // 统一成 64x64
        public bool Slim;           // 3 像素细手臂的皮肤
        public string Source = "内置默认形象";

        public static readonly List<SkinPart> Parts = new List<SkinPart>
        {
            //           名字        宽 高 深  中心x  底部y  正面                背面                左侧                右侧                顶面                 第二层偏移
            new SkinPart("右腿", 4, 12, 4, -2, 0,  R(4, 20, 4, 12),  R(12, 20, 4, 12), R(8, 20, 4, 12),  R(0, 20, 4, 12),  R(4, 16, 4, 4),  new Point(0, 16)),
            new SkinPart("左腿", 4, 12, 4,  2, 0,  R(20, 52, 4, 12), R(28, 52, 4, 12), R(24, 52, 4, 12), R(16, 52, 4, 12), R(20, 48, 4, 4), new Point(-16, 0)),
            new SkinPart("右臂", 4, 12, 4, -6, 12, R(44, 20, 4, 12), R(52, 20, 4, 12), R(48, 20, 4, 12), R(40, 20, 4, 12), R(44, 16, 4, 4), new Point(0, 16)),
            new SkinPart("左臂", 4, 12, 4,  6, 12, R(36, 52, 4, 12), R(44, 52, 4, 12), R(40, 52, 4, 12), R(32, 52, 4, 12), R(36, 48, 4, 4), new Point(16, 0)),
            new SkinPart("身体", 8, 12, 4,  0, 12, R(20, 20, 8, 12), R(32, 20, 8, 12), R(28, 20, 4, 12), R(16, 20, 4, 12), R(20, 16, 8, 4), new Point(0, 16)),
            new SkinPart("头",   8,  8, 8,  0, 24, R(8, 8, 8, 8),    R(24, 8, 8, 8),    R(16, 8, 8, 8),   R(0, 8, 8, 8),    R(8, 0, 8, 8),   new Point(32, 0)),
        };

        static Rectangle R(int x, int y, int w, int h)
        {
            return new Rectangle(x, y, w, h);
        }

        // ---------------------------------------------------------- 载入
        public static Skin Load(string path, out string error)
        {
            error = null;
            try
            {
                using (Bitmap raw = new Bitmap(path))
                {
                    if (raw.Width != 64 || (raw.Height != 64 && raw.Height != 32))
                    {
                        error = "皮肤图必须是 64x64 或 64x32 的 PNG";
                        return null;
                    }
                    Skin skin = new Skin();
                    skin.Texture = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(skin.Texture))
                    {
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(raw, new Rectangle(0, 0, 64, 64), new Rectangle(0, 0, raw.Width, raw.Height), GraphicsUnit.Pixel);
                    }
                    if (raw.Height == 32)
                    {
                        LegacyToModern(skin.Texture);
                        // 单像素手臂的旧皮肤, 取几个点判断是不是"细手臂"
                    }
                    skin.Slim = DetectSlim(skin.Texture);
                    skin.Source = Path.GetFileName(path);
                    return skin;
                }
            }
            catch (Exception exc)
            {
                error = "读不了这个文件: " + exc.Message;
                return null;
            }
        }

        /// <summary>旧的 64x32 皮肤: 左手左腿是镜像的, 补到新版的 UV 位置上。</summary>
        static void LegacyToModern(Bitmap tex)
        {
            Copy(tex, R(44, 20, 4, 12), R(36, 52, 4, 12));
            Copy(tex, R(52, 20, 4, 12), R(44, 52, 4, 12));
            Copy(tex, R(48, 20, 4, 12), R(40, 52, 4, 12));
            Copy(tex, R(40, 20, 4, 12), R(32, 52, 4, 12));
            Copy(tex, R(44, 16, 4, 4), R(36, 48, 4, 4));
            Copy(tex, R(4, 20, 4, 12), R(20, 52, 4, 12));
            Copy(tex, R(12, 20, 4, 12), R(28, 52, 4, 12));
            Copy(tex, R(8, 20, 4, 12), R(24, 52, 4, 12));
            Copy(tex, R(0, 20, 4, 12), R(16, 52, 4, 12));
            Copy(tex, R(4, 16, 4, 4), R(20, 48, 4, 4));
        }

        static void Copy(Bitmap tex, Rectangle from, Rectangle to)
        {
            using (Graphics g = Graphics.FromImage(tex))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(tex, to, from, GraphicsUnit.Pixel);
            }
        }

        /// <summary>细手臂皮肤的判定: 手臂前 4x4 的第一个像素通常是透明的。</summary>
        static bool DetectSlim(Bitmap tex)
        {
            try
            {
                // 细手臂皮肤在右臂正面左下方有一列透明像素
                for (int y = 20; y < 32; y++)
                {
                    if (tex.GetPixel(46, y).A == 0) return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        // ---------------------------------------------------------- 找皮肤
        /// <summary>找一个能用的皮肤: 启动器里指定的 > 玩家自己放的 > 游戏皮肤缓存。</summary>
        public static string AutoFind()
        {
            List<string> roots = new List<string>();
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            roots.Add(Path.Combine(roaming, ".minecraft"));
            roots.Add(Path.Combine(roaming, "minecraft"));
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, ".minecraft"));
            return AutoFindIn(roots);
        }

        public static string AutoFindIn(IEnumerable<string> minecraftRoots)
        {
            List<string> roots = new List<string>();
            foreach (string root in minecraftRoots)
            {
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) roots.Add(root);
            }
            // 1) 启动器配置里写明的皮肤文件 (HMCL / PCL2 / 官方启动器都可能留一手)
            foreach (string root in roots)
            {
                string fromConfig = FindInConfig(root);
                if (fromConfig != null) return fromConfig;
            }
            // 2) 玩家自己丢在 skins 文件夹里的
            foreach (string root in roots)
            {
                string[] sub = new string[] { "skins", "皮肤" };
                foreach (string name in sub)
                {
                    string dir = Path.Combine(root, name);
                    string found = NewestSkinIn(dir);
                    if (found != null) return found;
                }
            }
            // 3) 游戏自己下载的皮肤缓存 (数量可能很多, 取最近用到的那个)
            foreach (string root in roots)
            {
                string dir = Path.Combine(root, "assets", "skins");
                string found = NewestSkinIn(dir);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>翻启动器留下的 json, 看有没有直接写着皮肤图片路径。</summary>
        static string FindInConfig(string root)
        {
            List<string> files = new List<string>();
            try
            {
                files.AddRange(Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly));
                foreach (string sub in new string[] { "hmcl", "PCL", "launcher" })
                {
                    string dir = Path.Combine(root, sub);
                    if (Directory.Exists(dir))
                        files.AddRange(Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly));
                }
            }
            catch (Exception) { }
            foreach (string file in files)
            {
                string text;
                try
                {
                    FileInfo info = new FileInfo(file);
                    if (info.Length > 512 * 1024) continue;
                    text = File.ReadAllText(file);
                }
                catch (Exception) { continue; }
                foreach (System.Text.RegularExpressions.Match match in
                         System.Text.RegularExpressions.Regex.Matches(text, @"[A-Za-z]:[\\/][^""\r\n]{2,200}?\.png"))
                {
                    string candidate = match.Value.Replace("/", "\\");
                    while (candidate.Contains("\\\\")) candidate = candidate.Replace("\\\\", "\\");
                    try
                    {
                        if (File.Exists(candidate) && IsSkinFile(candidate)) return candidate;
                    }
                    catch (Exception) { }
                }
            }
            return null;
        }

        static string NewestSkinIn(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        if (info.Length < 100 || info.Length > 200000) continue;
                        if (info.LastWriteTime <= bestTime) continue;
                        if (!IsSkinFile(file)) continue;
                        best = file;
                        bestTime = info.LastWriteTime;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return best;
        }

        public static bool IsSkinFile(string path)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(path))
                {
                    return bmp.Width == 64 && (bmp.Height == 64 || bmp.Height == 32);
                }
            }
            catch (Exception) { return false; }
        }

        // ---------------------------------------------------------- 默认形象
        /// <summary>没有皮肤文件时, 用代码画一个方块小人 (自绘, 不是游戏原素材)。</summary>
        public static Skin Default()
        {
            Skin skin = new Skin();
            skin.Texture = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            skin.Source = "内置默认形象";
            Color hair = Color.FromArgb(255, 78, 51, 33);
            Color hairDark = Color.FromArgb(255, 58, 37, 24);
            Color face = Color.FromArgb(255, 232, 190, 150);
            Color faceDark = Color.FromArgb(255, 208, 165, 128);
            Color shirt = Color.FromArgb(255, 78, 140, 196);
            Color shirtDark = Color.FromArgb(255, 58, 106, 152);
            Color pants = Color.FromArgb(255, 74, 82, 140);
            Color pantsDark = Color.FromArgb(255, 56, 62, 108);
            Color pants2 = Color.FromArgb(255, 66, 74, 128);
            Color shoe = Color.FromArgb(255, 84, 84, 88);
            Color eyeWhite = Color.FromArgb(255, 245, 245, 250);
            Color eyeIris = Color.FromArgb(255, 58, 74, 128);
            Color mouth = Color.FromArgb(255, 150, 100, 88);

            using (Graphics g = Graphics.FromImage(skin.Texture))
            {
                g.Clear(Color.Transparent);
                // 头: 四周头发, 正面露脸
                Fill(g, R(8, 0, 8, 8), hair);            // 头顶
                Fill(g, R(0, 8, 8, 8), hairDark);        // 右脸侧
                Fill(g, R(16, 8, 8, 8), hairDark);       // 左脸侧
                Fill(g, R(24, 8, 8, 8), hair);           // 后脑
                Fill(g, R(16, 0, 8, 8), hairDark);       // 下巴 -> 脖子
                Fill(g, R(8, 8, 8, 8), face);            // 正脸
                Fill(g, R(8, 8, 8, 2), hair);            // 刘海
                Fill(g, R(8, 10, 1, 1), hairDark);
                Fill(g, R(15, 10, 1, 1), hairDark);
                Fill(g, R(9, 11, 2, 2), eyeWhite);       // 眼白
                Fill(g, R(13, 11, 2, 2), eyeWhite);
                Fill(g, R(10, 11, 1, 2), eyeIris);       // 瞳孔
                Fill(g, R(14, 11, 1, 2), eyeIris);
                Fill(g, R(11, 14, 2, 1), faceDark);      // 鼻子阴影
                Fill(g, R(11, 15, 2, 1), mouth);         // 嘴
                // 身体
                Fill(g, R(20, 16, 8, 4), shirt);         // 肩
                Fill(g, R(20, 20, 8, 12), shirt);        // 前胸
                Fill(g, R(32, 20, 8, 12), shirtDark);    // 后背
                Fill(g, R(16, 20, 4, 12), shirtDark);    // 右侧
                Fill(g, R(28, 20, 4, 12), shirtDark);    // 左侧
                Fill(g, R(20, 20, 8, 2), shirtDark);     // 领口
                Fill(g, R(22, 30, 4, 1), shirtDark);     // 下摆
                // 手臂: 上袖下皮肤
                ArmPart(g, R(44, 20, 4, 12), R(40, 20, 4, 12), R(48, 20, 4, 12), R(52, 20, 4, 12), shirt, shirtDark, face);
                ArmPart(g, R(36, 52, 4, 12), R(32, 52, 4, 12), R(40, 52, 4, 12), R(44, 52, 4, 12), shirt, shirtDark, face);
                // 腿: 上裤下鞋
                LegPart(g, R(4, 20, 4, 12), R(0, 20, 4, 12), R(8, 20, 4, 12), R(12, 20, 4, 12), pants, pantsDark, shoe);
                LegPart(g, R(20, 52, 4, 12), R(16, 52, 4, 12), R(24, 52, 4, 12), R(28, 52, 4, 12), pants2, pantsDark, shoe);
            }
            return skin;
        }

        static void ArmPart(Graphics g, Rectangle front, Rectangle right, Rectangle left, Rectangle back,
                            Color sleeve, Color sleeveDark, Color hand)
        {
            Fill(g, front, sleeve);
            Fill(g, R(front.X, front.Bottom - 4, front.Width, 4), hand);
            Fill(g, back, sleeveDark);
            Fill(g, R(back.X, back.Bottom - 4, back.Width, 4), hand);
            Fill(g, right, sleeveDark);
            Fill(g, R(right.X, right.Bottom - 4, right.Width, 4), hand);
            Fill(g, left, sleeveDark);
            Fill(g, R(left.X, left.Bottom - 4, left.Width, 4), hand);
        }

        static void LegPart(Graphics g, Rectangle front, Rectangle right, Rectangle left, Rectangle back,
                            Color pants, Color pantsDark, Color shoe)
        {
            Fill(g, front, pants);
            Fill(g, R(front.X, front.Bottom - 4, front.Width, 4), shoe);
            Fill(g, back, pantsDark);
            Fill(g, R(back.X, back.Bottom - 4, back.Width, 4), shoe);
            Fill(g, right, pantsDark);
            Fill(g, R(right.X, right.Bottom - 4, right.Width, 4), shoe);
            Fill(g, left, pantsDark);
            Fill(g, R(left.X, left.Bottom - 4, left.Width, 4), shoe);
        }

        static void Fill(Graphics g, Rectangle r, Color color)
        {
            using (SolidBrush b = new SolidBrush(color)) g.FillRectangle(b, r);
        }

        // ---------------------------------------------------------- 渲染
        /// <summary>把角色画在 box 里; angleDeg 是绕竖轴转的角度, breathe 是呼吸幅度(皮肤像素)。</summary>
        public static void Render(Graphics g, Rectangle box, Skin skin, double angleDeg, double breathe)
        {
            if (skin == null || skin.Texture == null) return;
            // 角色高 32 个皮肤像素, 宽 16, 留点余量
            double scale = Math.Min(box.Width / 19.0, box.Height / 35.0);
            if (scale <= 0.5) return;
            InterpolationMode oldMode = g.InterpolationMode;
            PixelOffsetMode oldOffset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            double angle = angleDeg * Math.PI / 180.0;
            double cos = Math.Abs(Math.Cos(angle));
            double sin = Math.Abs(Math.Sin(angle));
            bool leanLeft = Math.Sin(angle) < 0;

            double centerX = box.X + box.Width / 2.0;
            double groundY = box.Y + box.Height - 3 - breathe * scale;

            // 地面投影
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(46, 0, 0, 0)))
            {
                double shadowW = 11 * cos * scale;
                g.FillEllipse(shadow, (float)(centerX - shadowW / 2), (float)(groundY - 1 * scale),
                              (float)shadowW, (float)(2.2 * scale));
            }

            foreach (SkinPart part in Parts)
            {
                DrawPart(g, skin, part, centerX, groundY, scale, cos, sin, leanLeft);
            }
            g.InterpolationMode = oldMode;
            g.PixelOffsetMode = oldOffset;
        }

        static void DrawPart(Graphics g, Skin skin, SkinPart part, double centerX, double groundY,
                             double scale, double cos, double sin, bool leanLeft)
        {
            double frontW = part.W * cos * scale;
            double sideW = part.D * sin * scale;
            double totalW = frontW + sideW;
            double px = centerX + part.CenterX * cos * scale;
            double left = px - totalW / 2;
            double top = groundY - (part.BottomY + part.H) * scale;
            double h = part.H * scale;

            bool showSide = sin > 0.02;
            Rectangle sideSrc = (part.Name == "头" || part.Name == "身体")
                ? (leanLeft ? part.Left : part.Right)
                : (leanLeft ? part.Left : part.Right);
            if (leanLeft)
            {
                // 看到左侧: 侧面贴在正面左边
                if (showSide) DrawQuad(g, skin, sideSrc, left, top, sideW, h);
                DrawQuad(g, skin, part.Front, left + sideW, top, frontW, h);
            }
            else
            {
                DrawQuad(g, skin, part.Front, left, top, frontW, h);
                if (showSide) DrawQuad(g, skin, sideSrc, left + frontW, top, sideW, h);
            }

            // 帽子 / 外套 / 裤子那种第二层贴图 (皮肤里有内容才画)
            Rectangle overlayFront = Shift(part.Front, part.OverlayShift);
            if (!HasPixels(skin.Texture, overlayFront)) return;
            Rectangle overlaySide = Shift(sideSrc, part.OverlayShift);
            if (leanLeft)
            {
                if (showSide && HasPixels(skin.Texture, overlaySide)) DrawQuad(g, skin, overlaySide, left, top, sideW, h);
                DrawQuad(g, skin, overlayFront, left + sideW, top, frontW, h);
            }
            else
            {
                DrawQuad(g, skin, overlayFront, left, top, frontW, h);
                if (showSide && HasPixels(skin.Texture, overlaySide)) DrawQuad(g, skin, overlaySide, left + frontW, top, sideW, h);
            }
        }

        static Rectangle Shift(Rectangle r, Point shift)
        {
            return new Rectangle(r.X + shift.X, r.Y + shift.Y, r.Width, r.Height);
        }

        static bool HasPixels(Bitmap tex, Rectangle r)
        {
            if (r.Right > tex.Width || r.Bottom > tex.Height) return false;
            BitmapData data = tex.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buffer = new byte[r.Width * r.Height * 4];
                for (int y = 0; y < r.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(
                        (IntPtr)((long)data.Scan0 + y * data.Stride), buffer, y * r.Width * 4, r.Width * 4);
                for (int i = 3; i < buffer.Length; i += 4) if (buffer[i] > 8) return true;
                return false;
            }
            finally { tex.UnlockBits(data); }
        }

        static void DrawQuad(Graphics g, Skin skin, Rectangle src, double x, double y, double w, double h)
        {
            if (w < 0.4 || h < 0.4 || src.Width <= 0 || src.Height <= 0) return;
            if (src.Right > skin.Texture.Width || src.Bottom > skin.Texture.Height) return;
            g.DrawImage(skin.Texture, new RectangleF((float)x, (float)y, (float)w, (float)h), src, GraphicsUnit.Pixel);
        }
    }
}
