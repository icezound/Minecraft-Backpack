// 用代码画出 Minecraft 风格的像素贴图 (不引用游戏原素材)。
// 所有尺寸以 "MC 像素" 为单位, 再乘 k 倍放大, 保持像素颗粒感。
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MCBag
{
    public static class Tex
    {
        public static readonly Color Panel = Color.FromArgb(198, 198, 198);
        public static readonly Color Outline = Color.FromArgb(55, 55, 55);
        public static readonly Color Shadow = Color.FromArgb(85, 85, 85);
        public static readonly Color Hilight = Color.FromArgb(255, 255, 255);
        public static readonly Color SlotFill = Color.FromArgb(139, 139, 139);
        public static readonly Color SlotEdgeDark = Color.FromArgb(84, 84, 84);
        public static readonly Color SlotEdgeLight = Color.FromArgb(172, 172, 172);
        public static readonly Color Btn = Color.FromArgb(139, 139, 139);
        public static readonly Color BtnHover = Color.FromArgb(166, 182, 214);
        public static readonly Color BtnPress = Color.FromArgb(110, 110, 110);
        public static readonly Color BtnBlack = Color.FromArgb(20, 20, 20);
        public static readonly Color TipInner = Color.FromArgb(16, 0, 16);
        public static readonly Color TooltipBorder = Color.FromArgb(86, 26, 165);
        public static readonly Color TextMain = Color.FromArgb(255, 255, 255);
        public static readonly Color TextShadow = Color.FromArgb(60, 60, 60);
        public static readonly Color TextDim = Color.FromArgb(170, 170, 170);
        public static readonly Color TextGray = Color.FromArgb(122, 122, 122);
        public static readonly Color TextGrayShadow = Color.FromArgb(228, 228, 228);
        public static readonly Color Warn = Color.FromArgb(200, 70, 70);
        public static readonly Color Link = Color.FromArgb(150, 205, 255);

        private static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();

        public static Bitmap Get(string key, Func<Bitmap> make)
        {
            Bitmap bmp;
            if (cache.TryGetValue(key, out bmp)) return bmp;
            bmp = make();
            cache[key] = bmp;
            return bmp;
        }

        // 画一圈边框: 上/左一套颜色, 下/右另一套, 形成凹陷或凸起感
        private static void Ring(Graphics g, Rectangle box, int inset, int thick, Color top, Color left, Color bottom, Color right)
        {
            Rectangle r = new Rectangle(box.X + inset, box.Y + inset, box.Width - inset * 2, box.Height - inset * 2);
            if (r.Width <= 0 || r.Height <= 0) return;
            Fill(g, new Rectangle(r.X, r.Y, r.Width, thick), top);
            Fill(g, new Rectangle(r.X, r.Bottom - thick, r.Width, thick), bottom);
            Fill(g, new Rectangle(r.X, r.Y, thick, r.Height), left);
            Fill(g, new Rectangle(r.Right - thick, r.Y, thick, r.Height), right);
        }

        private static void Fill(Graphics g, Rectangle r, Color c)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, r);
        }

        public static Bitmap PanelBg(int width, int height, int k)
        {
            return Get("panel:" + width + ":" + height + ":" + k, delegate
            {
                Bitmap bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    Rectangle box = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    Fill(g, box, Outline);
                    Ring(g, box, k, k, Hilight, Hilight, Shadow, Shadow);
                    Fill(g, new Rectangle(2 * k, 2 * k, bmp.Width - 4 * k, bmp.Height - 4 * k), Panel);
                }
                return bmp;
            });
        }

        public static Bitmap Slot(int k, string state)
        {
            return Get("slot:" + k + ":" + state, delegate
            {
                int size = 18 * k;
                Bitmap bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    Rectangle box = new Rectangle(0, 0, size, size);
                    Fill(g, box, SlotFill);
                    Ring(g, box, 0, k, SlotEdgeDark, SlotEdgeDark, SlotEdgeLight, SlotEdgeLight);
                    if (state == "hover")
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(62, 255, 255, 255))) g.FillRectangle(b, box);
                    }
                    else if (state == "drag")
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 255, 255, 255))) g.FillRectangle(b, box);
                    }
                }
                return bmp;
            });
        }

        public static Bitmap ButtonBg(int width, int height, int k, string state)
        {
            return Get("btn:" + width + ":" + height + ":" + k + ":" + state, delegate
            {
                Bitmap bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    Rectangle box = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    Color body = Btn;
                    if (state == "hover") body = BtnHover;
                    else if (state == "press") body = BtnPress;
                    else if (state == "active") body = BtnHover;
                    Fill(g, box, BtnBlack);
                    if (state == "press") Ring(g, box, k, k, Shadow, Shadow, Hilight, Hilight);
                    else Ring(g, box, k, k, Hilight, Hilight, Shadow, Shadow);
                    Fill(g, new Rectangle(2 * k, 2 * k, bmp.Width - 4 * k, bmp.Height - 4 * k), body);
                }
                return bmp;
            });
        }

        public static Bitmap TooltipBg(int width, int height, int k)
        {
            return Get("tip:" + width + ":" + height + ":" + k, delegate
            {
                Bitmap bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    Rectangle box = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    Fill(g, box, TooltipBorder);
                    Fill(g, new Rectangle(k, k, bmp.Width - 2 * k, bmp.Height - 2 * k), TipInner);
                }
                return bmp;
            });
        }

        public static Bitmap DividerBg(int width, int k)
        {
            return Get("div:" + width + ":" + k, delegate
            {
                Bitmap bmp = new Bitmap(Math.Max(1, width), 2 * k);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    Fill(g, new Rectangle(0, 0, bmp.Width, k), Outline);
                    Fill(g, new Rectangle(0, k, bmp.Width, k), Hilight);
                }
                return bmp;
            });
        }

        /// <summary>角色展示用的方框: 比格子更大的一处凹槽。</summary>
        public static Bitmap FrameBg(int width, int height, int k)
        {
            return Get("frame:" + width + ":" + height + ":" + k, delegate
            {
                Bitmap bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    Rectangle box = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    Fill(g, box, SlotEdgeDark);
                    Ring(g, box, 0, k, Color.FromArgb(70, 70, 70), Color.FromArgb(70, 70, 70),
                         SlotEdgeLight, SlotEdgeLight);
                    // 内部比普通格子稍暗, 顶部再压一条阴影, 看着更"凹"
                    Fill(g, new Rectangle(k, k, bmp.Width - 2 * k, bmp.Height - 2 * k), Color.FromArgb(124, 124, 124));
                    Fill(g, new Rectangle(k, k, bmp.Width - 2 * k, 2 * k), Color.FromArgb(104, 104, 104));
                    Fill(g, new Rectangle(k, 3 * k, 2 * k, bmp.Height - 4 * k), Color.FromArgb(110, 110, 110));
                }
                return bmp;
            });
        }

        /// <summary>原版风格的齿轮图标 (设置入口)。自己按像素点算出来的, 不是游戏素材。
        /// 先在 12x12 上画好, 再按 k 倍 NEAREST 放大, 所以边缘是干净的像素。</summary>
        public static Bitmap Gear(int k, string state)
        {
            return Get("gear3:" + k + ":" + state, delegate
            {
                const int g = 12;
                Color body = Color.FromArgb(66, 66, 66);
                Color lit = Color.FromArgb(108, 108, 108);
                Color hole = state == "hover" ? Color.FromArgb(150, 170, 205) : Color.FromArgb(120, 120, 120);
                Bitmap small = new Bitmap(g, g);
                for (int y = 0; y < g; y++)
                {
                    for (int x = 0; x < g; x++)
                    {
                        double dx = x - 5.5, dy = y - 5.5;
                        double r = Math.Sqrt(dx * dx + dy * dy);
                        bool tooth = false;
                        if (r > 4.6 && r <= 5.7)
                        {
                            double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                            double m = Math.Abs(ang) % 45.0;
                            if (m > 22.5) m = 45.0 - m;
                            tooth = m <= 12.0;
                        }
                        if (r <= 2.3)
                            small.SetPixel(x, y, hole);
                        else if (r <= 4.6 || tooth)
                            small.SetPixel(x, y, (dx + dy < -3.0) ? lit : body);   // 左上一小块提亮
                    }
                }
                int size = g * k;
                Bitmap bmp = new Bitmap(size, size);
                using (Graphics gr = Graphics.FromImage(bmp))
                {
                    gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    gr.Clear(Color.Transparent);
                    gr.DrawImage(small, new Rectangle(0, 0, size, size), new Rectangle(0, 0, g, g), GraphicsUnit.Pixel);
                }
                small.Dispose();
                return bmp;
            });
        }

        /// <summary>CPU / GPU 的小图标 (自己画的像素图)。</summary>
        public static Bitmap ChipIcon(int k, bool gpu)
        {
            return Get("chip:" + k + ":" + gpu, delegate
            {
                int w = gpu ? 14 : 11, h = gpu ? 10 : 11;
                Bitmap small = new Bitmap(w, h);
                Color body = Color.FromArgb(96, 150, 200);
                Color edge = Color.FromArgb(48, 80, 120);
                Color pin = Color.FromArgb(170, 170, 170);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        small.SetPixel(x, y, body);
                for (int x = 0; x < w; x++) { small.SetPixel(x, 0, edge); small.SetPixel(x, h - 1, edge); }
                for (int y = 0; y < h; y++) { small.SetPixel(0, y, edge); small.SetPixel(w - 1, y, edge); }
                if (gpu)
                {
                    for (int x = 2; x < w - 2; x++) for (int y = 2; y < h - 2; y++) small.SetPixel(x, y, Color.FromArgb(150, 150, 150));
                    for (int x = 3; x < w - 3; x++) small.SetPixel(x, 2, Color.FromArgb(210, 210, 210));
                    for (int x = 0; x < w; x += 3) { small.SetPixel(x, h - 1, pin); small.SetPixel(x, 0, pin); }
                }
                else
                {
                    for (int x = 3; x < 8; x++) for (int y = 3; y < 8; y++) small.SetPixel(x, y, Color.FromArgb(150, 190, 230));
                    for (int i = 2; i < 9; i += 2) { small.SetPixel(i, 0, pin); small.SetPixel(i, h - 1, pin); small.SetPixel(0, i, pin); small.SetPixel(w - 1, i, pin); }
                }
                Bitmap bmp = new Bitmap(w * k, h * k);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.Clear(Color.Transparent);
                    g.DrawImage(small, new Rectangle(0, 0, w * k, h * k), new Rectangle(0, 0, w, h), GraphicsUnit.Pixel);
                }
                small.Dispose();
                return bmp;
            });
        }

        /// <summary>合成栏那个小箭头。</summary>
        public static Bitmap ArrowIcon(int k, bool left, bool disabled)
        {
            return Get("parrow:" + k + ":" + left + ":" + disabled, delegate
            {
                Bitmap small = new Bitmap(7, 9);
                Color c = disabled ? Color.FromArgb(178, 178, 178) : Color.FromArgb(72, 72, 72);
                for (int y = 0; y < 9; y++)
                {
                    int dx = 6 - Math.Abs(y - 4);
                    int x1 = left ? 6 - dx : dx;
                    int x2 = left ? 7 - dx : dx - 1;
                    if (x1 >= 0 && x1 < 7) small.SetPixel(x1, y, c);
                    if (x2 >= 0 && x2 < 7) small.SetPixel(x2, y, c);
                }
                Bitmap bmp = new Bitmap(7 * k, 9 * k);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.Clear(Color.Transparent);
                    g.DrawImage(small, new Rectangle(0, 0, 7 * k, 9 * k), new Rectangle(0, 0, 7, 9), GraphicsUnit.Pixel);
                }
                small.Dispose();
                return bmp;
            });
        }

        public static Bitmap CraftArrowIcon(int k)
        {
            return Get("carrow:" + k, delegate
            {
                // 原版那种粗箭头: 一根横杆 + 实心三角头
                Bitmap small = new Bitmap(16, 14);
                Color c = Color.FromArgb(150, 150, 150);
                for (int x = 0; x <= 10; x++)
                {
                    small.SetPixel(x, 6, c);
                    small.SetPixel(x, 7, c);
                    small.SetPixel(x, 8, c);
                    small.SetPixel(x, 9, c);
                }
                for (int i = 0; i <= 5; i++)
                {
                    for (int y = 7 - i; y <= 8 + i; y++)
                    {
                        if (y >= 0 && y < 14) small.SetPixel(10 + i, y, c);
                    }
                }
                Bitmap bmp = new Bitmap(16 * k, 14 * k);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.Clear(Color.Transparent);
                    g.DrawImage(small, new Rectangle(0, 0, 16 * k, 14 * k), new Rectangle(0, 0, 16, 14), GraphicsUnit.Pixel);
                }
                small.Dispose();
                return bmp;
            });
        }

        /// <summary>原版风格的滑条轨道。</summary>
        public static Bitmap SliderTrack(int width, int height, int k)
        {
            return Get("track:" + width + ":" + height + ":" + k, delegate
            {
                Bitmap bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    Rectangle box = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    Fill(g, box, Color.FromArgb(140, 140, 140));
                    // 凹槽: 上/左深, 下/右亮
                    Ring(g, box, 0, k, SlotEdgeDark, SlotEdgeDark, Hilight, Hilight);
                    Fill(g, new Rectangle(k, k, bmp.Width - 2 * k, bmp.Height - 2 * k), Color.FromArgb(130, 130, 130));
                }
                return bmp;
            });
        }

        public static void ClearCache()
        {
            foreach (Bitmap b in cache.Values) b.Dispose();
            cache.Clear();
        }
    }
}
