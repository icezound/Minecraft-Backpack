// 设置窗口: 仿原版 Minecraft 的「选项」界面 —— 两列按钮 + 滑条 + 完成。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MCBag
{
    public class SettingsForm : Form
    {
        public readonly BagApp app;
        Store store { get { return app.store; } }

        int k;
        readonly List<UiButton> buttons = new List<UiButton>();
        UiButton hoverButton;
        UiButton pressedButton;
        Rectangle sliderRect;
        Rectangle knobRect;
        bool draggingSlider;

        public static readonly string[] HotkeyPresets = new string[]
        { "ctrl+alt+e", "ctrl+alt+b", "ctrl+alt+space", "ctrl+shift+space", "f8" };

        const int PagesMin = 1, PagesMax = 9;

        public SettingsForm(BagApp app)
        {
            this.app = app;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Tex.Panel;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            Rebuild();
        }

        // ------------------------------------------------------------ 排版
        public void Rebuild()
        {
            k = store.config.scale;
            if (k > 3) k = 3;                     // 选项页不用跟着面板放到最大
            ClientSize = new Size(196 * k, 306 * k);
            buttons.Clear();
            hoverButton = null;
            pressedButton = null;

            int rowH = 20 * k;
            int gap = 2 * k;
            int colW = (ClientSize.Width - 16 * k - 4 * k) / 2;
            int top = 26 * k;

            // 第一列
            Cycle("scale", "格子大小: " + GameScaleText(), 0, 0, colW, rowH, gap, top, CycleScale);
            Cycle("dim", "压暗桌面: " + OnOff(store.config.dim), 0, 1, colW, rowH, gap, top, delegate
            {
                store.config.dim = !store.config.dim;
                store.Save();
                app.ApplyDim();
                Rebuild();
            });
            // 注意极性: 界面显示的是"保留界面", 存储的是"是否收起"
            Cycle("hide", "启动后保留界面: " + OnOff(!store.config.hideOnLaunch), 0, 2, colW, rowH, gap, top, delegate
            {
                store.config.hideOnLaunch = !store.config.hideOnLaunch;
                store.Save();
                app.Panel.Toast(store.config.hideOnLaunch ? "启动程序后会自动收起背包" : "启动程序后背包会留在原地", 2600);
                Rebuild();
            });
            Cycle("boot", "开机自动运行: " + OnOff(store.config.autostart), 0, 3, colW, rowH, gap, top, delegate
            {
                store.config.autostart = !store.config.autostart;
                store.Save();
                Store.SetAutostart(store.config.autostart);
                Rebuild();
            });

            // 第二列
            Cycle("names", "显示程序名: " + OnOff(store.config.showNames), 1, 0, colW, rowH, gap, top, delegate
            {
                store.config.showNames = !store.config.showNames;
                store.Save();
                app.ApplySettings(false);
                Rebuild();
            });
            Cycle("top", "始终在最顶端: " + OnOff(store.config.topMost), 1, 1, colW, rowH, gap, top, delegate
            {
                store.config.topMost = !store.config.topMost;
                store.Save();
                app.ApplyTopMost();
                Rebuild();
            });
            Cycle("digits", "数字键 1-9: " + OnOff(store.config.hotbarGlobal), 1, 2, colW, rowH, gap, top, delegate
            {
                store.config.hotbarGlobal = !store.config.hotbarGlobal;
                store.Save();
                app.RegisterHotkeys();
                Rebuild();
            });
            Cycle("hotkey", "唤出键: " + Compact(HotKeys.Pretty(store.config.toggleHotkey)), 1, 3, colW, rowH, gap, top, CycleHotkey);
            Cycle("hotkeyMode", "唤出键再按收起: " + OnOff(store.config.hotkeyToggles), 0, 4, colW, rowH, gap, top, delegate
            {
                store.config.hotkeyToggles = !store.config.hotkeyToggles;
                store.Save();
                app.Panel.Toast(store.config.hotkeyToggles ? "再按唤出键会把背包收起" : "再按唤出键只会把它提到最前", 2600);
                Rebuild();
            });

            int y = top + 5 * (rowH + gap) + 6 * k;
            sliderRect = new Rectangle(8 * k, y, ClientSize.Width - 16 * k, 20 * k);
            UpdateKnob();
            y += 20 * k + 10 * k;

            // 分隔线 (原版选项页下面也是这么一块一块的)
            separatorY = y;
            y += 8 * k;

            // 原本在背包底部那一栏, 现在都并进这里
            int half = (ClientSize.Width - 16 * k - 4 * k) / 2;
            Action("add", "添加程序", 8 * k, y, half, app.AddFiles);
            Action("scan", "扫描导入", 8 * k + half + 4 * k, y, half, app.ScanMenu);
            y += 24 * k;
            Action("quit", "退出程序", 8 * k, y, half, delegate { app.Quit(); });
            Action("center", "回到屏幕中央", 8 * k + half + 4 * k, y, half, delegate { app.CenterPanel(); });
            y += 24 * k;
            Action("data", "打开数据文件夹", 8 * k, y, half, delegate
            {
                try { Process.Start(store.Dir); } catch (Exception) { }
            });
            Action("reset", "恢复默认", 8 * k + half + 4 * k, y, half, AskReset);
            y += 24 * k;
            Action("skin", "换皮肤", 8 * k, y, half, app.ChangeSkin);
            Action("del", "批量删除", 8 * k + half + 4 * k, y, half, AskDelete);
            y += 24 * k;
            Action("done", "完成", 8 * k, y, ClientSize.Width - 16 * k, delegate { app.CloseSettings(); });

            UiButton close = new UiButton();
            close.Name = "close";
            close.Rect = new Rectangle(ClientSize.Width - 8 * k - 9 * k, 3 * k, 9 * k, 9 * k);
            close.Label = "✕";
            close.FontPx = Math.Max(9, (int)(3.4 * k));
            close.Click = delegate { app.CloseSettings(); };
            buttons.Add(close);

            Invalidate();
        }

        int separatorY;

        void Cycle(string name, string label, int col, int row, int colW, int rowH, int gap, int top, Action onClick)
        {
            UiButton b = new UiButton();
            b.Name = name;
            b.Label = label;
            b.Base = "normal";
            b.Rect = new Rectangle(8 * k + col * (colW + 4 * k), top + row * (rowH + gap), colW, rowH);
            b.Click = onClick;
            buttons.Add(b);
        }

        void Action(string name, string label, int x, int y, int w, Action onClick)
        {
            UiButton b = new UiButton();
            b.Name = name;
            b.Label = label;
            b.Rect = new Rectangle(x, y, w, 20 * k);
            b.Click = onClick;
            buttons.Add(b);
        }

        static string OnOff(bool value) { return value ? "开" : "关"; }

        string GameScaleText()
        {
            switch (store.config.scale)
            {
                case 2: return "小";
                case 4: return "大";
            }
            return "中";
        }

        /// <summary>把 "Ctrl + Shift + Space" 压缩成 "Ctrl+Shift+Space", 免得按钮里放不下。</summary>
        static string Compact(string pretty) { return pretty.Replace(" ", ""); }

        // ------------------------------------------------------------ 动作
        void CycleScale()
        {
            int next = store.config.scale >= 4 ? 2 : store.config.scale + 1;
            store.config.scale = next;
            store.Save();
            app.ApplySettings(true);
            Rebuild();
        }

        void CycleHotkey()
        {
            string current = store.config.toggleHotkey;
            int index = Array.IndexOf(HotkeyPresets, current);
            store.config.toggleHotkey = HotkeyPresets[(index + 1) % HotkeyPresets.Length];
            store.Save();
            app.RegisterHotkeys();
            Rebuild();
        }

        int BagItems()
        {
            int n = 0;
            foreach (Item it in store.bag) if (it != null) n++;
            return n;
        }

        /// <summary>批量删除: 选项里写明后果, 点了就直接执行 (再点"取消"也没损失)。</summary>
        void AskDelete()
        {
            int per = store.BagPerPage;
            List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();
            items.Add(new KeyValuePair<string, Action>("清空当前页 (这 " + per + " 格里的程序)", delegate
            {
                int n = store.ClearBag(true);
                store.Save();
                app.ApplySettings(true);
                Rebuild();
                app.Panel.Toast("删掉了 " + n + " 个程序", 3000);
            }));
            items.Add(new KeyValuePair<string, Action>("清空整个背包 (共 " + BagItems() + " 个程序)", delegate
            {
                int n = store.ClearBag(false);
                store.Save();
                app.ApplySettings(true);
                Rebuild();
                app.Panel.Toast("删掉了 " + n + " 个程序", 3000);
            }));
            items.Add(new KeyValuePair<string, Action>("取消", null));
            Point center = new Point(Left + Width / 2, Top + Height / 2);
            app.ShowPopup("批量删除 (只是从背包移除, 不动磁盘上的程序)", items, center);
        }

        void SetPages(int pages)
        {
            pages = Math.Max(PagesMin, Math.Min(PagesMax, pages));
            int need = (BagItems() + store.BagPerPage - 1) / store.BagPerPage;
            if (pages < need)
            {
                pages = need;
                app.Panel.Toast("后面还有装东西的页, 至少要 " + need + " 页", 3000);
            }
            store.config.bagPages = pages;
            store.ResizeSlots();
            store.Save();
            app.ApplySettings(true);
            UpdateKnob();
            Invalidate(sliderRect);
        }

        void UpdateKnob()
        {
            int rows = Math.Max(store.BagPageCount, store.BagPages);
            double fraction = (rows - PagesMin) / (double)(PagesMax - PagesMin);
            int knobW = KnobWidth();
            int span = Math.Max(1, sliderRect.Width - knobW);
            knobRect = new Rectangle(sliderRect.X + (int)Math.Round(fraction * span), sliderRect.Y, knobW, sliderRect.Height);
        }

        /// <summary>原版滑条的把手是宽的, 文字就写在把手里面。</summary>
        int KnobWidth() { return (int)(sliderRect.Width * 0.42); }

        void SliderFromX(int x)
        {
            int knobW = KnobWidth();
            int span = Math.Max(1, sliderRect.Width - knobW);
            double fraction = (x - sliderRect.X - knobW / 2.0) / span;
            fraction = Math.Max(0, Math.Min(1, fraction));
            int pages = PagesMin + (int)Math.Round(fraction * (PagesMax - PagesMin));
            SetPages(pages);
        }

        void AskReset()
        {
            List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();
            items.Add(new KeyValuePair<string, Action>("确认恢复 (只重置设置, 不动背包内容)", delegate
            {
                Config def = new Config();
                store.config.scale = def.scale;
                store.config.showNames = def.showNames;
                store.config.dim = def.dim;
                store.config.topMost = def.topMost;
                store.config.hideOnLaunch = def.hideOnLaunch;
                store.config.hotbarGlobal = def.hotbarGlobal;
                store.config.toggleHotkey = def.toggleHotkey;
                store.config.autostart = def.autostart;
                Store.SetAutostart(false);
                store.Save();
                app.ApplySettings(true);
                Rebuild();
            }));
            items.Add(new KeyValuePair<string, Action>("取消", null));
            Point center = new Point(Left + Width / 2, Top + Height / 2);
            app.ShowPopup("恢复默认设置?", items, center);
        }

        // ------------------------------------------------------------ 绘制
        Font Fit(string text, int maxWidth, int basePx, bool bold)
        {
            for (int px = basePx; px >= 9; px--)
            {
                Font f = UiFont.Get(px, bold);
                if (UiFont.Measure(text, f).Width <= maxWidth) return f;
            }
            return UiFont.Get(9, bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.DrawImageUnscaled(Tex.PanelBg(ClientSize.Width, ClientSize.Height, k), 0, 0);

            // 标题 (原版就是顶上居中一行)
            UiFont.Outlined(g, "选项", UiFont.Get(Math.Max(12, (int)(5.4 * k)), true), Tex.TextMain, Tex.TextShadow,
                            ClientSize.Width / 2, 6 * k, "center");

            int basePx = Math.Max(10, (int)(4.0 * k));
            foreach (UiButton b in buttons)
            {
                g.DrawImageUnscaled(Tex.ButtonBg(b.Rect.Width, b.Rect.Height, k, b.State), b.Rect.X, b.Rect.Y);
                Font font = Fit(b.Label, b.Rect.Width - 4 * k, b.FontPx > 0 ? b.FontPx : basePx, false);
                UiFont.Centered(g, b.Label, font, Tex.TextMain, Tex.TextShadow, b.Rect);
            }

            // 滑条
            g.DrawImageUnscaled(Tex.SliderTrack(sliderRect.Width, sliderRect.Height, k), sliderRect.X, sliderRect.Y);
            string knobState = draggingSlider || pressedKnob ? "press" : (hoverKnob ? "hover" : "normal");
            g.DrawImageUnscaled(Tex.ButtonBg(knobRect.Width, knobRect.Height, k, knobState), knobRect.X, knobRect.Y);
            string sliderText = "背包页数: " + Math.Max(store.BagPageCount, store.BagPages) + " 页";
            UiFont.Centered(g, sliderText, Fit(sliderText, knobRect.Width - 4 * k, basePx, false),
                            Tex.TextMain, Tex.TextShadow, knobRect);

            if (separatorY > 0)
                g.DrawImageUnscaled(Tex.DividerBg(ClientSize.Width - 16 * k, k), 8 * k, separatorY);
        }

        bool hoverKnob;
        bool pressedKnob;

        // ------------------------------------------------------------ 鼠标
        UiButton Hit(Point p)
        {
            foreach (UiButton b in buttons) if (b.Rect.Contains(p)) return b;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point p = e.Location;
            if (draggingWindow)
            {
                Point cur = Cursor.Position;
                Rectangle area = SystemInformation.VirtualScreen;
                int x = dragStartWindow.X + (cur.X - dragStartCursor.X);
                int y = dragStartWindow.Y + (cur.Y - dragStartCursor.Y);
                x = Math.Max(area.Left - Width + 180, Math.Min(area.Right - 180, x));
                y = Math.Max(area.Top, Math.Min(area.Bottom - 40, y));
                Location = new Point(x, y);
                return;
            }
            if (draggingSlider)
            {
                SliderFromX(p.X);
                return;
            }
            bool overKnob = knobRect.Contains(p) || sliderRect.Contains(p);
            if (overKnob != hoverKnob)
            {
                hoverKnob = overKnob;
                Invalidate(sliderRect);
            }
            Cursor = overKnob ? Cursors.SizeWE : Cursors.Default;
            UiButton hit = Hit(p);
            if (hit != hoverButton)
            {
                if (hoverButton != null && hoverButton != pressedButton) hoverButton.State = hoverButton.Base;
                hoverButton = hit;
                if (hit != null && hit != pressedButton) hit.State = "hover";
                Invalidate();
            }
            if (hit == null && InTitleBand(p)) Cursor = Cursors.SizeAll;
            base.OnMouseMove(e);
        }

        bool InTitleBand(Point p) { return p.Y >= 0 && p.Y < 24 * k; }

        bool draggingWindow;
        Point dragStartCursor;
        Point dragStartWindow;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Point p = e.Location;
            UiButton hit = Hit(p);
            if (hit != null)
            {
                pressedButton = hit;
                hit.State = "press";
                Invalidate();
                return;
            }
            if (InTitleBand(p))      // 顶部标题那一条: 按住就能拖着整个设置窗走
            {
                draggingWindow = true;
                dragStartCursor = Cursor.Position;
                dragStartWindow = Location;
                return;
            }
            if (sliderRect.Contains(p))
            {
                draggingSlider = true;
                pressedKnob = true;
                SliderFromX(p.X);
                Invalidate(sliderRect);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (draggingWindow)
            {
                draggingWindow = false;
                Cursor = InTitleBand(e.Location) ? Cursors.SizeAll : Cursors.Default;
                app.SaveSettingsPosition(Location);
                return;
            }
            draggingSlider = false;
            pressedKnob = false;
            if (pressedButton != null)
            {
                UiButton button = pressedButton;
                pressedButton = null;
                button.State = button.Base;
                if (Hit(e.Location) == button && button.Click != null) button.Click();
                Invalidate();
                return;
            }
            Invalidate(sliderRect);
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { app.CloseSettings(); return; }
            base.OnKeyDown(e);
        }
    }
}
