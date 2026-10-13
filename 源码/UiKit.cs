// 界面小工具: 字体、描边文字、布局计算、按钮、MC 风格弹窗。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MCBag
{
    public static class UiFont
    {
        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        static readonly Dictionary<string, Size> measures = new Dictionary<string, Size>();
        static string family;

        public static string Family
        {
            get
            {
                if (family == null) family = PickFamily();
                return family;
            }
        }

        static string PickFamily()
        {
            string[] candidates = new string[] { "Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", "黑体", "SimHei", "宋体" };
            try
            {
                using (InstalledFontCollection col = new InstalledFontCollection())
                {
                    foreach (string want in candidates)
                        foreach (FontFamily f in col.Families)
                            if (string.Equals(f.Name, want, StringComparison.OrdinalIgnoreCase)) return f.Name;
                }
            }
            catch (Exception) { }
            return "Microsoft YaHei UI";
        }

        public static Font Get(int px, bool bold)
        {
            string key = px + (bold ? "b" : "n");
            Font font;
            if (cache.TryGetValue(key, out font)) return font;
            font = new Font(Family, px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            cache[key] = font;
            return font;
        }

        public static Size Measure(string text, Font font)
        {
            if (text == null) text = "";
            string key = font.SizeInPoints.ToString() + "/" + font.Style + "/" + text;
            Size size;
            if (measures.TryGetValue(key, out size)) return size;
            size = TextRenderer.MeasureText(text, font, new Size(int.MaxValue / 2, int.MaxValue / 2),
                                            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            measures[key] = size;
            return size;
        }

        public static void ClearCache()
        {
            measures.Clear();
        }

        /// <summary>带描边的文字 (MC 风格), align 用 Left/Center/Right, 返回占用的宽度。</summary>
        public static int Outlined(Graphics g, string text, Font font, Color fill, Color outline, int x, int y, string align)
        {
            return Outlined(g, text, font, fill, outline, x, y, align, true);
        }

        public static int Outlined(Graphics g, string text, Font font, Color fill, Color outline, int x, int y, string align, bool doOutline)
        {
            Size size = Measure(text, font);
            int left = x;
            if (align == "center") left = x - size.Width / 2;
            else if (align == "right") left = x - size.Width;
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            if (doOutline)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        TextRenderer.DrawText(g, text, font, new Point(left + dx, y + dy), outline, flags);
                    }
                }
            }
            TextRenderer.DrawText(g, text, font, new Point(left, y), fill, flags);
            return size.Width;
        }

        /// <summary>把文字画在矩形正中 (带四个方向的描边)。</summary>
        public static void Centered(Graphics g, string text, Font font, Color fill, Color outline, Rectangle area)
        {
            Size size = Measure(text, font);
            int x = area.X + (area.Width - size.Width) / 2;
            int y = area.Y + (area.Height - size.Height) / 2;
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        TextRenderer.DrawText(g, text, font, new Point(x + dx, y + dy), outline, flags);
            TextRenderer.DrawText(g, text, font, new Point(x, y), fill, flags);
        }
    }

    /// <summary>所有格子和按钮的位置 (单位: 屏幕像素)。</summary>
    public class PanelLayout
    {
        public int K, Rows, Cell, Margin, Width, Height;
        public int BagTop, BagBottom, HotbarTop, HotbarBottom;
        public int IconPx, NameFontPx, HotIconPx, HotCell, HotbarX;
        public int GridX;                       // 背包网格左边距 (左边留给角色展示)
        public Rectangle SkinBox;               // 角色展示方框
        public Rectangle TitleStrip;            // 顶部标题条: 按住这里可以拖着窗口跑
        public Rectangle GearRect;              // 左上角的齿轮 (打开设置)
        public Rectangle CloseRect;
        public Rectangle TempCpu, TempGpu;      // 左侧状态栏: CPU / GPU 温度
        public Rectangle PagePrev, PageNext;    // 翻页箭头 (网格右上方)
        public Rectangle Offhand;               // 模型右下那个小格
        public Rectangle CraftGrid, CraftResult, CraftArrow;
        public int CraftLabelY;
        public const int SideX = 8;             // 左右内容的边距 (MC 像素)

        public PanelLayout(int k, int rows, bool showNames)
        {
            K = k;
            Rows = rows;
            Cell = 18 * k;
            Margin = SideX * k;
            GridX = SideX * k;
            Width = 176 * k;                     // 和原版一样宽
            // 顶部: 左边状态栏 + 角色模型 + 右上「合成」
            TempCpu = new Rectangle(SideX * k, 18 * k, 20 * k, 26 * k);
            TempGpu = new Rectangle(SideX * k, 46 * k, 20 * k, 26 * k);
            SkinBox = new Rectangle(32 * k, 18 * k, 52 * k, 72 * k);
            Offhand = new Rectangle(88 * k, 72 * k, Cell, Cell);
            CraftLabelY = 19 * k;
            CraftGrid = new Rectangle(98 * k, 28 * k, 2 * Cell, 2 * Cell);
            // 结果格与合成格的垂直中心对齐, 箭头正对结果格中间 (和原版一致)
            int craftMidY = CraftGrid.Y + CraftGrid.Height / 2;
            CraftArrow = new Rectangle(136 * k, craftMidY - 7 * k, 16 * k, 14 * k);
            CraftResult = new Rectangle(154 * k, craftMidY - Cell / 2, Cell, Cell);
            PagePrev = new Rectangle(136 * k, 70 * k, 16 * k, 16 * k);
            PageNext = new Rectangle(152 * k, 70 * k, 16 * k, 16 * k);
            // 背包网格 + 快捷栏 (参照图里快捷栏和背包格子一样大)
            BagTop = 96 * k;
            BagBottom = BagTop + rows * Cell;
            HotCell = Cell;
            HotbarX = GridX;
            HotbarTop = BagBottom + 4 * k;
            HotbarBottom = HotbarTop + HotCell;
            Height = HotbarBottom + 8 * k;
            CloseRect = new Rectangle(Width - 2 * k - 12 * k, 1 * k, 12 * k, 12 * k);
            TitleStrip = new Rectangle(0, 0, Width, BagTop);
            GearRect = new Rectangle(2 * k, 1 * k, 12 * k, 12 * k);
            NameFontPx = Math.Max(8, (int)(2.6 * k));
            IconPx = Math.Max(16, Cell - 2 * k - (showNames ? NameFontPx + 3 : 0));
            HotIconPx = Math.Max(16, HotCell - 2 * k - (showNames ? NameFontPx + 3 : 0));
        }

        public Rectangle SlotRect(bool hotbar, int index)
        {
            if (hotbar) return new Rectangle(HotbarX + index * HotCell, HotbarTop, HotCell, HotCell);
            int row = index / Store.Cols;
            int col = index % Store.Cols;
            return new Rectangle(GridX + col * Cell, BagTop + row * Cell, Cell, Cell);
        }

        public bool SlotAt(Point p, out bool hotbar, out int index)
        {
            hotbar = false;
            index = -1;
            if (p.Y >= HotbarTop && p.Y < HotbarBottom)
            {
                int hcol = (p.X - HotbarX) / HotCell;
                if (p.X >= HotbarX && hcol >= 0 && hcol < Store.HotbarSize)
                {
                    hotbar = true;
                    index = hcol;
                    return true;
                }
                return false;
            }
            if (p.X < GridX || p.X >= GridX + Store.Cols * Cell) return false;
            if (p.Y >= BagTop && p.Y < BagBottom)
            {
                int row = (p.Y - BagTop) / Cell;
                int col = (p.X - GridX) / Cell;
                if (row < 0 || row >= Rows || col < 0 || col >= Store.Cols) return false;
                hotbar = false;
                index = row * Store.Cols + col;
                return true;
            }
            return false;
        }
    }

    public class UiButton
    {
        public string Name;
        public Rectangle Rect;
        public string Label;
        public Action Click;
        public string State = "normal";
        public string Base = "normal";
        public bool On = false;
        public System.Drawing.Bitmap Icon;     // 有图标就不画文字 (比如齿轮)
        public bool PlainIcon = false;         // 只画图标, 不画按钮底框 (原版那种小图标按钮)
        public int FontPx = 0;                 // 0 = 用默认字号
    }

    /// <summary>MC 风格的小弹窗 (右键菜单 / 选择框)。</summary>
    public class PopupForm : Form
    {
        readonly List<UiButton> buttons = new List<UiButton>();
        readonly int k;
        readonly int titleH;
        string title;
        int hoverIndex = -1;
        int pressedIndex = -1;

        public PopupForm(string title, List<KeyValuePair<string, Action>> items, int k, Point center, Size parentSize)
        {
            this.k = k;
            this.title = title;
            int rowH = 20 * k;
            int gap = 2 * k;
            int count = 0;
            foreach (KeyValuePair<string, Action> item in items) if (item.Key != null) count++;
            titleH = string.IsNullOrEmpty(title) ? 6 * k : 20 * k;
            int width = 96 * k;
            foreach (KeyValuePair<string, Action> item in items)
            {
                if (item.Key == null) continue;
                int w = UiFont.Measure(item.Key, UiFont.Get(Math.Max(11, (int)(4.3 * k)), false)).Width + 16 * k;
                if (w > width) width = w;
            }
            int height = titleH + count * (rowH + gap) - gap + 8 * k;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Tex.Panel;
            ClientSize = new Size(width, height);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            int y = titleH;
            foreach (KeyValuePair<string, Action> item in items)
            {
                if (item.Key == null) continue;
                UiButton b = new UiButton();
                b.Name = "b" + buttons.Count;
                b.Rect = new Rectangle(4 * k, y, width - 8 * k, rowH);
                b.Label = item.Key;
                b.Click = item.Value;
                buttons.Add(b);
                y += rowH + gap;
            }

            int x = center.X - width / 2;
            int yy = center.Y - height / 2;
            if (parentSize.Width > 0)
            {
                Screen screen = Screen.FromPoint(center);
                Rectangle area = screen.WorkingArea;
                x = Math.Max(area.Left + 4, Math.Min(area.Right - width - 4, x));
                yy = Math.Max(area.Top + 4, Math.Min(area.Bottom - height - 4, yy));
            }
            Location = new Point(x, yy);
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            MouseMove += new MouseEventHandler(OnMove);
            MouseDown += new MouseEventHandler(OnDown);
            MouseUp += new MouseEventHandler(OnUp);
            Paint += new PaintEventHandler(OnPaint);
        }

        int Hit(Point p)
        {
            for (int i = 0; i < buttons.Count; i++) if (buttons[i].Rect.Contains(p)) return i;
            return -1;
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            int hit = Hit(e.Location);
            if (hit != hoverIndex)
            {
                if (hoverIndex >= 0 && pressedIndex != hoverIndex) buttons[hoverIndex].State = "normal";
                hoverIndex = hit;
                if (hit >= 0 && pressedIndex != hit) buttons[hit].State = "hover";
                Invalidate();
            }
        }

        void OnDown(object sender, MouseEventArgs e)
        {
            int hit = Hit(e.Location);
            if (hit < 0) { Close(); return; }
            pressedIndex = hit;
            buttons[hit].State = "press";
            Invalidate();
        }

        void OnUp(object sender, MouseEventArgs e)
        {
            if (pressedIndex < 0) return;
            int index = pressedIndex;
            pressedIndex = -1;
            buttons[index].State = "hover";
            if (Hit(e.Location) != index) { Invalidate(); return; }
            Action action = buttons[index].Click;
            Close();
            if (action != null) action();
        }

        void OnPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(Tex.PanelBg(ClientSize.Width, ClientSize.Height, k), 0, 0);
            if (!string.IsNullOrEmpty(title))
                UiFont.Outlined(e.Graphics, title, UiFont.Get(Math.Max(11, (int)(4.2 * k)), true), Tex.TextMain, Tex.TextShadow, ClientSize.Width / 2, 4 * k, "center");
            foreach (UiButton b in buttons)
            {
                e.Graphics.DrawImageUnscaled(Tex.ButtonBg(b.Rect.Width, b.Rect.Height, k, b.State), b.Rect.X, b.Rect.Y);
                UiFont.Centered(e.Graphics, b.Label, UiFont.Get(Math.Max(11, (int)(4.3 * k)), false), Tex.TextMain, Tex.TextShadow, b.Rect);
            }
        }
    }
}
