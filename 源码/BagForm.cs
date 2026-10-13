// 背包主界面: 仿 Minecraft 的物品栏。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MCBag
{
    public class BagForm : Form
    {
        public readonly BagApp app;
        public Store store { get { return app.store; } }

        public PanelLayout layout;
        Bitmap chrome;
        bool showNames = true;

        readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();
        readonly List<UiButton> buttons = new List<UiButton>();

        // 鼠标状态
        bool holdIsHotbar;
        int holdIndex = -1;
        Item holdItem;
        bool hasHover;
        bool hoverHotbar;
        int hoverIndex = -1;
        UiButton pressedButton;

        string toastText;
        DateTime toastUntil = DateTime.MinValue;
        readonly Timer toastTimer;
        readonly Timer skinTimer;
        readonly Timer dragTimer;          // 拖图标时 60fps 跟随鼠标
        readonly Timer sensorTimer;        // 温度采样
        double skinAngle;
        public Skin skin;

        // 拖着窗口跑 & 手里拿着图标
        bool draggingWindow;
        int page;                          // 当前显示第几页 (0 开始)
        Point dragStartCursor;
        Point dragStartWindow;
        bool gearPending;                  // 按住齿轮: 没移动就算点击, 移动了就当拖窗口
        Point gearPress;
        Point holdDraw;                    // 手上那个图标当前画在哪 (客户区坐标, 图标中心)

        public BagForm(BagApp app)
        {
            this.app = app;
            showNames = store.config.showNames;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Tex.Panel;
            Text = "我的世界背包";
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AllowDrop = true;

            toastTimer = new Timer();
            toastTimer.Interval = 200;
            toastTimer.Tick += delegate
            {
                if (toastText != null && DateTime.Now > toastUntil)
                {
                    toastText = null;
                    Invalidate();
                }
            };
            toastTimer.Start();

            skinTimer = new Timer();
            skinTimer.Interval = 70;
            skinTimer.Tick += delegate
            {
                skinAngle += 0.9;
                Invalidate(layout.SkinBox);
            };

            // 拖图标: 定时读当前鼠标位置, 只重画图标(旧+新)那一小块, 所以既跟手又不卡
            dragTimer = new Timer();
            dragTimer.Interval = 16;
            dragTimer.Tick += delegate
            {
                if (holdItem == null) { dragTimer.Stop(); return; }
                Point target = PointToClient(Cursor.Position);
                if (target == holdDraw) return;
                Rectangle old = HeldRect();
                holdDraw = target;
                Invalidate(Rectangle.Union(old, HeldRect()));
            };
            skin = app.skin;

            // 温度: 每 4 秒到后台问一次, 不卡界面
            sensorTimer = new Timer();
            sensorTimer.Interval = 4000;
            sensorTimer.Tick += delegate
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    Sensors.Refresh();
                    try
                    {
                        if (Visible) BeginInvoke((Action)delegate { Invalidate(layout.TempCpu); Invalidate(layout.TempGpu); });
                    }
                    catch (Exception) { }
                });
            };

            Rebuild();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (skinTimer != null)
            {
                if (Visible) skinTimer.Start();
                else skinTimer.Stop();
            }
        }

        // 背包成为当前窗口时, 数字键 1-9 才用来启动快捷栏;
        // 切到别的程序去以后就还给那个程序, 不会误触发。
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            app.SetPanelActive(true);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            app.SetPanelActive(false);
        }

        // ------------------------------------------------------------ 布局
        public void Rebuild()
        {
            showNames = store.config.showNames;
            skin = app.skin;
            if (page >= store.BagPageCount) page = 0;      // 页数变少就回第一页
            layout = new PanelLayout(store.config.scale, store.BagRows, showNames);
            ClientSize = new Size(layout.Width, layout.Height);
            BuildChrome();
            BuildButtons();
            ClearIconCache();
            Invalidate();
        }

        void BuildChrome()
        {
            if (chrome != null) chrome.Dispose();
            chrome = new Bitmap(layout.Width, layout.Height);
            using (Graphics g = Graphics.FromImage(chrome))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImageUnscaled(Tex.PanelBg(layout.Width, layout.Height, layout.K), 0, 0);
                Bitmap slot = Tex.Slot(layout.K, "normal");
                for (int i = 0; i < Store.Cols * layout.Rows; i++)
                {
                    Rectangle r = layout.SlotRect(false, i);
                    g.DrawImageUnscaled(slot, r.X, r.Y);
                }
                for (int i = 0; i < Store.HotbarSize; i++)
                {
                    Rectangle r = layout.SlotRect(true, i);
                    g.DrawImageUnscaled(slot, r.X, r.Y);
                }
                g.DrawImageUnscaled(Tex.FrameBg(layout.SkinBox.Width, layout.SkinBox.Height, layout.K),
                                    layout.SkinBox.X, layout.SkinBox.Y);
                g.DrawImageUnscaled(Tex.Slot(layout.K, "normal"), layout.Offhand.X, layout.Offhand.Y);
                for (int cx = 0; cx < 2; cx++)
                    for (int cy = 0; cy < 2; cy++)
                        g.DrawImageUnscaled(Tex.Slot(layout.K, "normal"),
                                            layout.CraftGrid.X + cx * layout.Cell, layout.CraftGrid.Y + cy * layout.Cell);
                g.DrawImageUnscaled(Tex.Slot(layout.K, "normal"), layout.CraftResult.X, layout.CraftResult.Y);
                g.DrawImageUnscaled(Tex.CraftArrowIcon(layout.K), layout.CraftArrow.X, layout.CraftArrow.Y);
                for (int i = 0; i < 4; i++)   // 左侧一列标准尺寸的格子 (原版 18 像素)
                    g.DrawImageUnscaled(Tex.Slot(layout.K, "normal"), 8 * layout.K, (18 + i * 18) * layout.K);
            }
        }

        void BuildButtons()
        {
            buttons.Clear();
            // 左上角齿轮 -> 打开设置 (和顶部其它按钮同一套风格)
            UiButton gear = new UiButton();
            gear.Name = "gear";
            gear.Rect = layout.GearRect;
            gear.Label = "";
            gear.Icon = Tex.Gear(layout.K, "normal");
            gear.PlainIcon = true;
            gear.Click = app.OpenSettings;
            buttons.Add(gear);

            UiButton close = new UiButton();
            close.Name = "close";
            close.Rect = layout.CloseRect;
            close.Label = "✕";
            close.FontPx = Math.Max(11, (int)(5.4 * layout.K));
            close.Click = delegate { app.HidePanel(); };
            buttons.Add(close);

            // 翻页箭头 (网格右上方)
            UiButton prev = new UiButton();
            prev.Name = "prev";
            prev.Rect = layout.PagePrev;
            prev.Label = "";
            prev.PlainIcon = true;
            prev.Click = delegate { TurnPage(-1); };
            buttons.Add(prev);
            UiButton next = new UiButton();
            next.Name = "next";
            next.Rect = layout.PageNext;
            next.Label = "";
            next.PlainIcon = true;
            next.Click = delegate { TurnPage(1); };
            buttons.Add(next);

            UiButton skinButton = new UiButton();
        }

        // ------------------------------------------------------------ 图标
        Bitmap IconFor(Item item, int px)
        {
            if (item == null) return null;
            string file = store.EnsureIcon(item);
            if (file == null) return null;
            string key = file + "@" + px;
            Bitmap bmp;
            if (iconCache.TryGetValue(key, out bmp)) return bmp;
            try
            {
                using (Bitmap src = new Bitmap(file))
                {
                    bmp = new Bitmap(px, px);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, px, px);
                    }
                }
            }
            catch (Exception) { bmp = null; }
            if (bmp != null) iconCache[key] = bmp;   // 读失败就别缓存, 下次重画再试
            return bmp;
        }

        public void ClearIconCache()
        {
            foreach (Bitmap b in iconCache.Values) if (b != null) b.Dispose();
            iconCache.Clear();
        }

        // ------------------------------------------------------------ 提示条
        public void Toast(string text, int ms)
        {
            toastText = text;
            toastUntil = DateTime.Now.AddMilliseconds(ms);
            Invalidate();
        }

        // ------------------------------------------------------------ 绘制
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            Rectangle clip = e.ClipRectangle;
            if (clip.Width > 0 && clip.Height > 0 &&
                clip.Width <= layout.SkinBox.Width && clip.Height <= layout.SkinBox.Height &&
                layout.SkinBox.Contains(clip))
            {
                DrawSkin(g);      // 动画只重画角色那一小块
                return;
            }
            g.DrawImageUnscaled(chrome, 0, 0);
            DrawHover(g);
            DrawItems(g);
            DrawSkin(g);
            DrawTexts(g);
            DrawButtons(g);
            DrawTooltip(g);
            DrawHeld(g);
            DrawToast(g);
        }

        /// <summary>角色展示方框: 游戏里那种站着慢慢转身的小人。</summary>
        void DrawSkin(Graphics g)
        {
            if (skin == null) return;
            Rectangle box = layout.SkinBox;
            g.DrawImageUnscaled(Tex.FrameBg(box.Width, box.Height, layout.K), box.X, box.Y);
            Rectangle inner = new Rectangle(box.X + 2 * layout.K, box.Y + 2 * layout.K,
                                            box.Width - 4 * layout.K, box.Height - 4 * layout.K);
            double sway = Math.Sin(skinAngle * Math.PI / 180.0) * 34;     // 左右摆动的角度
            double breathe = Math.Cos(skinAngle * 2 * Math.PI / 180.0) * 0.35;
            Skin.Render(g, inner, skin, sway, breathe);
        }

        void DrawHover(Graphics g)
        {
            if (dragOver && dragHot >= 0)
            {
                Rectangle d = layout.SlotRect(dragIsHotbar, dragHot);
                g.DrawImageUnscaled(Tex.Slot(layout.K, "hover"), d.X, d.Y);
                return;
            }
            if (!hasHover || holdItem != null) return;
            Rectangle r = layout.SlotRect(hoverHotbar, hoverIndex);
            g.DrawImageUnscaled(Tex.Slot(layout.K, "hover"), r.X, r.Y);
        }

        protected override void WndProc(ref Message m)
        {
            if (app != null && app.HandleWindowMessage(ref m)) return;
            base.WndProc(ref m);
        }

        void DrawItems(Graphics g)
        {
            // 只画当前页的格子: 翻页就是把这一页整块滚到列表最前面 (见 Store.RotateBag),
            // 所以这里按"页内序号"遍历, 超出这一页的物品绝不能画上去, 否则会压到快捷栏上。
            int per = store.BagPerPage;
            for (int i = 0; i < per && i < store.bag.Count; i++)
            {
                Item item = store.bag[i];
                if (item == null) continue;
                if (holdItem != null && !holdIsHotbar && i == holdIndex) continue;
                DrawItem(g, false, i, item);
            }
            List<Item> hotbar = store.Slots(true);
            for (int i = 0; i < hotbar.Count; i++)
            {
                Item item = hotbar[i];
                if (item == null) continue;
                if (holdItem != null && holdIsHotbar && i == holdIndex) continue;
                DrawItem(g, true, i, item);
            }
        }

        void DrawItem(Graphics g, bool hot, int index, Item item)
        {
            int k = layout.K;
            Rectangle r = layout.SlotRect(hot, index);
            int px = hot ? layout.HotIconPx : layout.IconPx;
            int textH = showNames ? layout.NameFontPx + 3 : 0;
            int slack = Math.Max(0, (r.Height - 2 * k - textH) - px);
            int iconX = r.X + (r.Width - px) / 2;
            int iconY = r.Y + k + slack / 2;
            bool missing = !File.Exists(item.path) && !Directory.Exists(item.path);

            Bitmap icon = IconFor(item, px);
            if (icon != null) g.DrawImage(icon, iconX, iconY, px, px);
            if (missing)
            {
                using (SolidBrush shade = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                    g.FillRectangle(shade, iconX, iconY, px, px);
                UiFont.Outlined(g, "!", UiFont.Get(layout.NameFontPx + 1, true), Color.FromArgb(255, 96, 96),
                                Color.FromArgb(40, 0, 0), iconX + px, iconY, "right");
            }
            if (!showNames) return;
            string name = Shorten(item.name, r.Width - 2 * k);
            UiFont.Outlined(g, name, UiFont.Get(layout.NameFontPx, false),
                            missing ? Color.FromArgb(210, 170, 170) : Color.White, Color.FromArgb(60, 60, 60),
                            r.X + r.Width / 2, r.Bottom - layout.NameFontPx - 3, "center");
        }

        string Shorten(string text, int maxWidth)
        {
            if (text == null) return "";
            Font font = UiFont.Get(layout.NameFontPx, false);
            if (UiFont.Measure(text, font).Width <= maxWidth) return text;
            for (int cut = text.Length - 1; cut > 0; cut--)
            {
                string candidate = text.Substring(0, cut) + "…";
                if (UiFont.Measure(candidate, font).Width <= maxWidth) return candidate;
            }
            return "";
        }

        void DrawTexts(Graphics g)
        {
            int k = layout.K;
            UiFont.Outlined(g, "背包", UiFont.Get(Math.Max(12, (int)(5.2 * k)), true), Tex.TextMain, Tex.TextShadow,
                            layout.GearRect.Right + 2 * k, 5 * k, "left");
            // 右上角的「合成」+ 左侧状态栏 (CPU / GPU 温度)
            UiFont.Outlined(g, "合成", UiFont.Get(Math.Max(10, (int)(4.2 * k)), false), Tex.TextMain, Tex.TextShadow,
                            layout.CraftGrid.X + layout.Cell, layout.CraftLabelY, "center");
        }

        void DrawTemp(Graphics g, Rectangle box, bool cpu)
        {
            int k = layout.K;
            Font font = UiFont.Get(Math.Max(9, (int)(3.2 * k)), true);
            Bitmap icon = Tex.ChipIcon(k, !cpu);
            int iconY = box.Y + 3 * k;
            int textH = Math.Max(9, (int)(3.2 * k)) + 2;
            int iconArea = box.Height - 3 * k - textH - 2 * k;
            g.DrawImage(icon, box.X + (box.Width - icon.Width) / 2,
                        iconY + Math.Max(0, (iconArea - icon.Height) / 2), icon.Width, icon.Height);
            string text = cpu ? Sensors.CpuText : Sensors.GpuText;
            UiFont.Outlined(g, text, font, Tex.TextMain, Color.FromArgb(40, 40, 40),
                            box.X + box.Width / 2, box.Bottom - textH - 2 * k, "center");
        }

        void DrawButtons(Graphics g)
        {
            Font font = UiFont.Get(Math.Max(10, (int)(4.3 * layout.K)), false);
            Font small = UiFont.Get(Math.Max(9, (int)(3.4 * layout.K)), true);
            foreach (UiButton b in buttons)
            {
                if (b.PlainIcon)
                {
                    // 小图标按钮: 没有底框, 只有图标 (悬停变亮, 按下往下挪一像素)
                    Bitmap icon;
                    if (b.Name == "prev" || b.Name == "next")
                    {
                        int pages = store.BagPageCount;
                        if (pages <= 1) continue;                   // 只有一页就不用显示箭头
                        bool atEdge = b.Name == "prev" ? page <= 0 : page >= pages - 1;
                        icon = Tex.ArrowIcon(layout.K, b.Name == "prev", atEdge);
                    }
                    else icon = b.Name == "gear" ? Tex.Gear(layout.K, b.State) : b.Icon;
                    if (icon == null) continue;
                    int dy = b.State == "press" ? Math.Max(1, layout.K / 3) : 0;
                    g.DrawImage(icon, b.Rect.X + (b.Rect.Width - icon.Width) / 2,
                                b.Rect.Y + (b.Rect.Height - icon.Height) / 2 + dy, icon.Width, icon.Height);
                    continue;
                }
                g.DrawImageUnscaled(Tex.ButtonBg(b.Rect.Width, b.Rect.Height, layout.K, b.State), b.Rect.X, b.Rect.Y);
                if (b.Icon != null)
                {
                    g.DrawImage(b.Icon, b.Rect.X + (b.Rect.Width - b.Icon.Width) / 2,
                                b.Rect.Y + (b.Rect.Height - b.Icon.Height) / 2, b.Icon.Width, b.Icon.Height);
                    continue;
                }
                Color fill = b.State == "disabled" ? Tex.TextDim : Tex.TextMain;
                Font labelFont = b.FontPx > 0 ? UiFont.Get(b.FontPx, true) : (b.Name == "close" ? small : font);
                UiFont.Centered(g, b.Label, labelFont, fill, Tex.TextShadow, b.Rect);
            }
        }

        void DrawTooltip(Graphics g)
        {
            if (!hasHover || holdItem != null) return;
            Item item = store.Get(hoverHotbar, hoverIndex);
            if (item == null) return;
            int k = layout.K;
            List<string> texts = new List<string>();
            List<Font> fonts = new List<Font>();
            List<Color> colors = new List<Color>();
            texts.Add(item.name);
            fonts.Add(UiFont.Get(Math.Max(10, (int)(4.1 * k)), false));
            colors.Add(Tex.TextMain);
            bool missing = !File.Exists(item.path) && !Directory.Exists(item.path);
            if (missing)
            {
                texts.Add("这个位置的文件不见了");
                fonts.Add(UiFont.Get(layout.NameFontPx, false));
                colors.Add(Color.FromArgb(255, 130, 130));
            }
            else
            {
                texts.Add(Shorten(item.path, 46 * k));
                fonts.Add(UiFont.Get(layout.NameFontPx, false));
                colors.Add(Tex.TextDim);
            }
            texts.Add(KindLabel(item.kind) + " · 双击启动");
            fonts.Add(UiFont.Get(layout.NameFontPx, false));
            colors.Add(Tex.Link);

            int pad = 3 * k;
            int tw = 0, th = 2 * pad;
            for (int i = 0; i < texts.Count; i++)
            {
                Size size = UiFont.Measure(texts[i], fonts[i]);
                if (size.Width > tw) tw = size.Width;
                th += size.Height + 3;
            }
            tw += 2 * pad;
            Rectangle r = layout.SlotRect(hoverHotbar, hoverIndex);
            // 提示框放在格子的右边 (像 MC 那样不挡物品), 右边放不下就换到左边
            int tx = r.Right + 4 * k;
            if (tx + tw > layout.Width - 4) tx = r.X - tw - 4 * k;
            tx = Math.Max(4, Math.Min(layout.Width - tw - 4, tx));
            int ty = r.Y + r.Height / 2 - th / 2;
            ty = Math.Max(4, Math.Min(layout.Height - th - 4, ty));
            g.DrawImageUnscaled(Tex.TooltipBg(tw, th, k), tx, ty);
            int cursor = ty + pad;
            for (int i = 0; i < texts.Count; i++)
            {
                UiFont.Outlined(g, texts[i], fonts[i], colors[i], Color.FromArgb(24, 24, 24), tx + pad, cursor, "left");
                cursor += UiFont.Measure(texts[i], fonts[i]).Height + 3;
            }
        }

        public static string KindLabel(string kind)
        {
            switch (kind)
            {
                case "lnk": return "快捷方式";
                case "url": return "网址";
                case "exe": return "程序";
                case "bat":
                case "cmd": return "脚本";
                case "folder": return "文件夹";
            }
            return "文件";
        }

        void DrawHeld(Graphics g)
        {
            if (holdItem == null) return;
            int px = layout.IconPx;
            Bitmap icon = IconFor(holdItem, px);
            if (icon == null) return;
            g.DrawImage(icon, holdDraw.X - px / 2, holdDraw.Y - px / 2, px, px);
        }

        Rectangle HeldRect()
        {
            int px = layout.IconPx;
            return new Rectangle(holdDraw.X - px / 2 - 2, holdDraw.Y - px / 2 - 2, px + 4, px + 4);
        }

        void DrawToast(Graphics g)
        {
            if (toastText == null) return;
            int k = layout.K;
            Font font = UiFont.Get(Math.Max(9, (int)(3.5 * k)), false);
            int tw = Math.Min(UiFont.Measure(toastText, font).Width + 8 * k, layout.Width - 2 * layout.Margin);
            string text = Shorten(toastText, tw - 8 * k);
            Size size = UiFont.Measure(text, font);
            int th = size.Height + 6 * k;
            int x = (layout.Width - tw) / 2;      // 底部居中显示, 整行都够宽
            int y = layout.Height - th - 4 * k;   // 贴在面板最下面
            g.DrawImageUnscaled(Tex.TooltipBg(tw, th, k), x, y);
            UiFont.Outlined(g, text, font, Tex.TextMain, Color.FromArgb(24, 24, 24), x + tw / 2, y + th / 2 - size.Height / 2, "center");
        }

        public void TestHover(bool hot, int index)
        {
            hasHover = true;
            hoverHotbar = hot;
            hoverIndex = index;
            Invalidate();
        }

        public void TestHold(bool hot, int index, Point at)
        {
            Item item = store.Get(hot, index);
            if (item == null) return;
            holdItem = item;
            holdIsHotbar = hot;
            holdIndex = index;
            holdDraw = at;
            hasHover = false;
            Invalidate();
        }

        // ------------------------------------------------------------ 鼠标
        UiButton HitButton(Point p)
        {
            if (holdItem != null) return null;
            foreach (UiButton b in buttons) if (b.Rect.Contains(p)) return b;
            return null;
        }

        /// <summary>更新鼠标下的格子高亮, 只重画受影响的那一两个格子 (拖东西时这样才不卡)。</summary>
        void SetHover(Point p)
        {
            bool hot;
            int index;
            bool ok = layout.SlotAt(p, out hot, out index);
            bool changed = ok != hasHover || (ok && (hot != hoverHotbar || index != hoverIndex));
            if (!changed) return;
            Rectangle oldRect = hasHover ? layout.SlotRect(hoverHotbar, hoverIndex) : Rectangle.Empty;
            hasHover = ok;
            hoverHotbar = hot;
            hoverIndex = index;
            Rectangle newRect = ok ? layout.SlotRect(hot, index) : Rectangle.Empty;
            if (oldRect.Width > 0 && newRect.Width > 0) Invalidate(Rectangle.Union(oldRect, newRect));
            else if (oldRect.Width > 0) Invalidate(oldRect);
            else if (newRect.Width > 0) Invalidate(newRect);
        }

        /// <summary>顶部标题条 (除了关闭按钮) 按住就能拖着整个窗口走。</summary>
        bool InTitleStrip(Point p)
        {
            return layout.TitleStrip.Contains(p) && !layout.CloseRect.Contains(p);
        }

        static Point ClampToScreen(Size size, Point want)
        {
            Rectangle area = SystemInformation.VirtualScreen;
            int minX = area.Left - size.Width + 180;    // 至少留 180 像素在屏幕里
            int maxX = area.Right - 180;
            int minY = area.Top;
            int maxY = area.Bottom - 40;
            return new Point(Math.Max(minX, Math.Min(maxX, want.X)),
                             Math.Max(minY, Math.Min(maxY, want.Y)));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point p = e.Location;
            if (gearPending)
            {
                // 按住齿轮挪动超过几个像素, 就转成"拖窗口"; 否则松开时算点了一下
                Point cur = Cursor.Position;
                if (Math.Abs(cur.X - gearPress.X) + Math.Abs(cur.Y - gearPress.Y) > 5)
                {
                    gearPending = false;
                    if (pressedButton != null) pressedButton.State = pressedButton.Base;
                    pressedButton = null;
                    draggingWindow = true;
                    dragStartCursor = cur;
                    dragStartWindow = Location;
                }
                return;
            }
            if (draggingWindow)
            {
                Point cursor = Cursor.Position;
                Location = ClampToScreen(Size, new Point(dragStartWindow.X + (cursor.X - dragStartCursor.X),
                                                         dragStartWindow.Y + (cursor.Y - dragStartCursor.Y)));
                return;
            }
            Cursor = InTitleStrip(p) ? Cursors.SizeAll : Cursors.Default;
            UiButton button = HitButton(p);
            bool needPaint = false;
            foreach (UiButton b in buttons)
            {
                string want = b.Base;
                if (b == button && b != pressedButton) want = "hover";
                if (b.State != want && b != pressedButton) { b.State = want; needPaint = true; }
            }
            if (holdItem != null)
            {
                // 手上的图标由 60fps 定时器跟随, 这里只更新格子高亮
                SetHover(p);
                if (needPaint) Invalidate();
                return;
            }
            SetHover(p);
            if (needPaint) Invalidate();
            Invalidate();          // 提示框要跟着鼠标挪, 这一层仍整块重画 (只在没拿东西时)
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Point p = e.Location;
            if (e.Button == MouseButtons.Right)
            {
                if (InTitleStrip(p)) app.ShowWindowMenu(PointToScreen(p));
                else ShowContextMenu(p);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (app.ClosePopup()) return;
            // 先看按钮 (齿轮/✕/换皮肤都在顶部条附近, 不能被"拖窗口"抢走点击)
            UiButton button = HitButton(p);
            if (button != null)
            {
                pressedButton = button;
                button.State = "press";
                if (button.Name == "gear")
                {
                    gearPending = true;
                    gearPress = Cursor.Position;
                }
                Invalidate();
                return;
            }
            if (InTitleStrip(p))
            {
                draggingWindow = true;
                dragStartCursor = Cursor.Position;
                dragStartWindow = Location;
                return;
            }
            if (holdItem != null)
            {
                bool hot;
                int index;
                if (layout.SlotAt(p, out hot, out index)) DropHeld(hot, index);
                else ReturnHeld();
                return;
            }
            bool hh;
            int hi;
            if (layout.SlotAt(p, out hh, out hi))
            {
                Item item = store.Get(hh, hi);
                if (item != null)
                {
                    holdItem = item;
                    holdIsHotbar = hh;
                    holdIndex = hi;
                    holdDraw = p;
                    dragTimer.Start();
                    Invalidate();
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            gearPending = false;
            if (draggingWindow)
            {
                draggingWindow = false;
                Cursor = InTitleStrip(e.Location) ? Cursors.SizeAll : Cursors.Default;
                app.SaveWindowPosition(Location);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                if (pressedButton != null)
                {
                    UiButton button = pressedButton;
                    pressedButton = null;
                    button.State = button.Base;
                    if (HitButton(e.Location) == button && button.Click != null) button.Click();
                    Invalidate();
                    return;
                }
                if (holdItem != null)
                {
                    bool hot;
                    int index;
                    if (layout.SlotAt(e.Location, out hot, out index))
                    {
                        if (!(hot == holdIsHotbar && index == holdIndex)) DropHeld(hot, index);
                    }
                    else
                    {
                        ReturnHeld();
                    }
                    Invalidate();
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Item item = null;
            if (holdItem != null)
            {
                item = holdItem;
                ReturnHeld();
            }
            else
            {
                bool hot;
                int index;
                if (layout.SlotAt(e.Location, out hot, out index)) item = store.Get(hot, index);
            }
            if (item != null) app.Launch(item);
            base.OnMouseDoubleClick(e);
        }

        void DropHeld(bool hot, int index)
        {
            if (!(hot == holdIsHotbar && index == holdIndex))
            {
                store.Swap(holdIsHotbar, holdIndex, hot, index);
                store.Save();
            }
            ReturnHeld();
        }

        void ReturnHeld()
        {
            if (holdItem == null) return;
            Rectangle was = HeldRect();
            holdItem = null;
            holdIndex = -1;
            dragTimer.Stop();
            Invalidate(was);      // 把手上的图标擦掉
        }

        public void CancelHold()
        {
            if (holdItem != null) ReturnHeld();
        }

        /// <summary>翻页: 整块滚动背包 + 重画 (拖拽/右键逻辑完全不用动)。</summary>
        public void TurnPage(int delta)
        {
            int pages = store.BagPageCount;
            if (pages <= 1) return;
            int target = page + delta;
            if (target < 0 || target >= pages)      // 到头了就停住, 不再绕回第一页
            {
                Invalidate();
                return;
            }
            if (holdItem != null) ReturnHeld();
            page = target;
            store.RotateBag(delta);
            store.Save();
            hasHover = false;
            hoverIndex = -1;
            hasHover = false;      // 翻页后鼠标下的格子已经变了, 直接清掉高亮和提示
            Invalidate();
        }

        void ShowContextMenu(Point p)
        {
            bool hot;
            int index;
            if (!layout.SlotAt(p, out hot, out index)) return;
            Item item = store.Get(hot, index);
            List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();
            string title;
            if (item != null)
            {
                title = item.name;
                Item captured = item;
                bool capHot = hot;
                int capIndex = index;
                items.Add(new KeyValuePair<string, Action>("打开", delegate { app.Launch(captured); }));
                items.Add(new KeyValuePair<string, Action>("打开所在文件夹", delegate { Store.Reveal(captured.path); }));
                items.Add(new KeyValuePair<string, Action>("放进快捷栏", delegate { PutInHotbar(capHot, capIndex); }));
                items.Add(new KeyValuePair<string, Action>(null, null));
                items.Add(new KeyValuePair<string, Action>("从背包移除", delegate { RemoveSlot(capHot, capIndex); }));
                items.Add(new KeyValuePair<string, Action>("取消", null));
            }
            else
            {
                title = "空位";
                items.Add(new KeyValuePair<string, Action>("添加程序…", app.AddFiles));
                items.Add(new KeyValuePair<string, Action>("扫描桌面快捷方式", delegate { app.ImportFrom("desktop"); }));
                items.Add(new KeyValuePair<string, Action>("扫描开始菜单", delegate { app.ImportFrom("startmenu"); }));
                items.Add(new KeyValuePair<string, Action>("取消", null));
            }
            app.ShowPopup(title, items, PointToScreen(p));
        }

        public void RemoveSlot(bool hot, int index)
        {
            if (store.Get(hot, index) == null) return;
            store.Set(hot, index, null);
            store.Save();
            Invalidate();
        }

        void PutInHotbar(bool hot, int index)
        {
            if (hot) return;
            Item item = store.Get(hot, index);
            if (item == null) return;
            int empty = store.FirstEmpty(true);
            if (empty < 0)
            {
                Toast("快捷栏满了, 先腾一格出来", 2600);
                return;
            }
            store.Set(hot, index, null);
            store.Set(true, empty, item);
            store.Save();
            Invalidate();
            Toast("已经放到快捷栏第 " + (empty + 1) + " 格", 2600);
        }

        // ------------------------------------------------------------ 键盘
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.E)
            {
                app.HidePanel();
                e.Handled = true;
                return;
            }
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int index = e.KeyCode - Keys.D1;
                Item item = store.Get(true, index);
                if (item != null) app.Launch(item);
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Delete && hasHover)
            {
                RemoveSlot(hoverHotbar, hoverIndex);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        // ------------------------------------------------------------ 拖文件进来
        bool dragOver;
        int dragHot = -1;
        bool dragIsHotbar;

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                dragOver = true;
                Invalidate();
            }
            base.OnDragEnter(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                Point client = PointToClient(new Point(e.X, e.Y));
                bool hot;
                int index;
                bool ok = layout.SlotAt(client, out hot, out index);
                int newIndex = ok ? index : -1;
                if (newIndex != dragHot || hot != dragIsHotbar)
                {
                    dragHot = newIndex;
                    dragIsHotbar = hot;
                    Invalidate();
                }
            }
            base.OnDragOver(e);
        }

        protected override void OnDragLeave(EventArgs e)
        {
            dragOver = false;
            dragHot = -1;
            Invalidate();
            base.OnDragLeave(e);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            dragOver = false;
            dragHot = -1;
            string[] files = null;
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                files = e.Data.GetData(DataFormats.FileDrop) as string[];
            Invalidate();
            if (files == null || files.Length == 0) return;
            // 拖进来的如果是皮肤图, 就当成换皮肤
            List<string> items = new List<string>();
            foreach (string file in files)
            {
                if (string.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase) &&
                    Skin.IsSkinFile(file))
                {
                    app.SetSkin(file);
                }
                else
                {
                    items.Add(file);
                }
            }
            if (items.Count > 0) app.ImportPaths(items.ToArray(), "拖进来的程序");
            base.OnDragDrop(e);
        }
    }
}
