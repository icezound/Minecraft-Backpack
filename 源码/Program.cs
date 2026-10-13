// 程序入口: 无边框遮罩 + 背包窗口 + 托盘 + 全局快捷键。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MCBag
{
    /// <summary>出错时记一笔, 方便排查。</summary>
    public static class Log
    {
        /// <summary>日志目录, 由 BagApp 设置成实际的数据目录。</summary>
        public static string Dir;

        public static void Write(string message)
        {
            try
            {
                string dir = string.IsNullOrEmpty(Dir) ? Path.Combine(Store.AppDir(), "data") : Dir;
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "log.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + Environment.NewLine,
                    System.Text.Encoding.UTF8);
            }
            catch (Exception) { }
        }
    }

    /// <summary>半透明全屏遮罩 (点了就关背包)。</summary>
    public class DimForm : Form
    {
        public DimForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            Opacity = 0.45;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;   // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000020;   // WS_EX_TRANSPARENT: 鼠标点穿过去, 不挡住别的程序
                return cp;
            }
        }
    }

    public class BagApp : ApplicationContext
    {
        public const int HK_TOGGLE = 1;
        public const int HK_HOTBAR_BASE = 10;
        public const int HK_DIGIT_BASE = 30;      // 背包打开时临时占用数字键 1-9

        public Store store;
        public BagForm Panel;
        public SettingsForm Settings;
        public bool ToggleHotkeyOk = true;
        public Skin skin;
        bool digitHotkeysOn;

        DimForm dim;
        NotifyIcon tray;
        PopupForm popup;
        int showMessage;

        [DllImport("user32.dll")] static extern IntPtr SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string message);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("kernel32.dll")] static extern int GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);
        [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public BagApp(string dataDir)
        {
            store = new Store(dataDir);
            Log.Dir = store.Dir;
            skin = store.ResolveSkin();
            showMessage = RegisterWindowMessage("MinecraftBag_ShowPanel_Message");
            Panel = new BagForm(this);
            Panel.HandleCreated += delegate { RegisterHotkeys(); };
            dim = new DimForm();
            BuildTray();
            RegisterHotkeys();
        }

        // ---------------------------------------------------------- 托盘
        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "我的世界背包";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowPanel(); };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开背包", null, delegate { ShowPanel(); });
            menu.Items.Add("设置", null, delegate { ShowPanel(); OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Quit(); });
            tray.ContextMenuStrip = menu;
            Icon icon = MakeIcon();
            Panel.Icon = icon;
        }

        static Icon MakeIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.Clear(Color.Transparent);
                using (SolidBrush dark = new SolidBrush(Color.FromArgb(45, 30, 12))) g.FillRectangle(dark, 2, 4, 28, 25);
                using (SolidBrush body = new SolidBrush(Color.FromArgb(139, 90, 43))) g.FillRectangle(body, 3, 5, 26, 23);
                using (SolidBrush light = new SolidBrush(Color.FromArgb(178, 122, 63))) g.FillRectangle(light, 3, 5, 26, 3);
                using (SolidBrush band = new SolidBrush(Color.FromArgb(92, 58, 26))) g.FillRectangle(band, 3, 14, 26, 3);
                using (SolidBrush metal = new SolidBrush(Color.FromArgb(200, 200, 200))) g.FillRectangle(metal, 14, 13, 4, 6);
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(255, 214, 120)))
                {
                    g.FillRectangle(shine, 16, 17, 2, 4);
                }
            }
            IntPtr handle = bmp.GetHicon();
            Icon result = (Icon)Icon.FromHandle(handle).Clone();
            NativeDestroyIcon(handle);
            bmp.Dispose();
            return result;
        }

        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
        static void NativeDestroyIcon(IntPtr h) { try { DestroyIcon(h); } catch (Exception) { } }

        // ---------------------------------------------------------- 显示
        public void ShowPanel()
        {
            ClosePopup();
            ApplyTopMost();
            Panel.Location = PanelLocation();
            Panel.Show();
            ShowWindow(Panel.Handle, 9);      // SW_RESTORE: 被"显示桌面/最小化"藏起来也能恢复
            ApplyDim();                 // 必须在面板显示之后, 它要按当前可见状态决定显不显示
            Panel.BringToFront();
            Panel.Activate();
            ForceForeground(Panel.Handle);
            Panel.Focus();
            ForceTopmostFlash(Panel.Handle);      // 强行提到最前 (全屏程序压着也能出来)
            RegisterDigitHotkeys(true);           // 打开时数字键直接启动快捷栏
            // 万一没抢到前台焦点 (比如被别的程序压着), 就把数字键放掉,
            // 免得你在别的程序里打字时误启动快捷栏里的程序
            Timer verify = new Timer();
            verify.Interval = 400;
            verify.Tick += delegate
            {
                verify.Stop();
                verify.Dispose();
                if (!Panel.Visible) return;
                RegisterDigitHotkeys(GetForegroundWindow() == Panel.Handle);
            };
            verify.Start();
        }

        /// <summary>背包窗口是不是当前活动窗口; 只有它是活动窗口时才占用数字键。</summary>
        public void SetPanelActive(bool active)
        {
            if (!Panel.Visible) return;
            RegisterDigitHotkeys(active);
        }

        /// <summary>鼠标在哪块屏幕上, 背包就显示在哪块屏幕。</summary>
        public static Screen CurrentScreen()
        {
            try { return Screen.FromPoint(Cursor.Position); }
            catch (Exception) { return Screen.PrimaryScreen; }
        }

        /// <summary>面板该出现在哪: 上次拖到的位置还在屏幕里就用它, 否则回到屏幕中央。</summary>
        Point PanelLocation()
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            int x = store.config.winX;
            int y = store.config.winY;
            if (x != -1 || y != -1)
            {
                if (x >= vs.Left - Panel.Width + 180 && x <= vs.Right - 180 &&
                    y >= vs.Top && y <= vs.Bottom - 40)
                    return new Point(x, y);
            }
            Rectangle area = CurrentScreen().Bounds;
            return new Point(area.Left + Math.Max(0, (area.Width - Panel.Width) / 2),
                             area.Top + Math.Max(0, (area.Height - Panel.Height) / 2 - 20));
        }

        /// <summary>用户把窗口拖到哪就记下来, 下次打开还在那儿。</summary>
        public void SaveWindowPosition(Point location)
        {
            store.config.winX = location.X;
            store.config.winY = location.Y;
            store.Save();
        }

        /// <summary>短暂挂到最高层, 保证被任何窗口压着也能冒出来; 设置里没开"始终在最顶端"就立刻还原。</summary>
        void ForceTopmostFlash(IntPtr hwnd)
        {
            try
            {
                const uint NOMOVE = 0x0002, NOSIZE = 0x0001, SHOW = 0x0040;
                SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, NOMOVE | NOSIZE | SHOW);
                if (!store.config.topMost)
                    SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, NOMOVE | NOSIZE);
            }
            catch (Exception) { }
        }

        /// <summary>回到屏幕中央 (并清掉记下来的位置)。</summary>
        public void CenterPanel()
        {
            store.config.winX = -1;
            store.config.winY = -1;
            store.Save();
            Panel.Location = PanelLocation();
        }

        /// <summary>把"始终在最顶端"的开关应用到所有窗口。</summary>
        public void ApplyTopMost()
        {
            bool top = store.config.topMost;
            try
            {
                Panel.TopMost = top;
                dim.TopMost = top;
                if (Settings != null) Settings.TopMost = top;
                if (popup != null) popup.TopMost = top;
            }
            catch (Exception) { }
        }

        /// <summary>按设置显示/隐藏那层压暗背景 (它就是"周围变暗"的效果)。</summary>
        public void ApplyDim()
        {
            try
            {
                if (!Panel.Visible) return;
                if (store.config.dim)
                {
                    dim.Bounds = CurrentScreen().Bounds;
                    dim.Show();
                    dim.SendToBack();
                    Panel.BringToFront();
                }
                else
                {
                    dim.Hide();
                }
            }
            catch (Exception) { }
        }

        public void HidePanel()
        {
            ClosePopup();
            CloseSettings();
            Panel.CancelHold();
            Panel.Hide();
            dim.Hide();
            RegisterDigitHotkeys(false);
        }

        public bool PanelVisible()
        {
            return Panel.Visible;
        }

        public void Toggle()
        {
            if (Panel.Visible) HidePanel();
            else ShowPanel();
        }

        /// <summary>唤出: 没开就打开, 已经开着就强行提到最前 (不管当前是哪个窗口、是否全屏)。</summary>
        public void Summon()
        {
            ShowPanel();
        }

        public static void ForceForeground(IntPtr hwnd)
        {
            try
            {
                IntPtr foreground = GetForegroundWindow();
                int mine = GetCurrentThreadId();
                if (foreground != IntPtr.Zero && foreground != hwnd)
                {
                    int other = GetWindowThreadProcessId(foreground, IntPtr.Zero);
                    if (other != 0 && other != mine)
                    {
                        AttachThreadInput(other, mine, true);
                        SetForegroundWindow(hwnd);
                        AttachThreadInput(other, mine, false);
                    }
                    else
                    {
                        SetForegroundWindow(hwnd);
                    }
                }
                else
                {
                    SetForegroundWindow(hwnd);
                }
                BringWindowToTop(hwnd);
                SetFocus(hwnd);
            }
            catch (Exception) { }
        }

        // ---------------------------------------------------------- 快捷键
        public void RegisterHotkeys()
        {
            IntPtr hwnd;
            try { hwnd = Panel.Handle; }
            catch (Exception) { return; }
            for (int i = 0; i <= 9; i++) HotKeys.Unregister(hwnd, HK_HOTBAR_BASE + i);
            HotKeys.Unregister(hwnd, HK_TOGGLE);
            ToggleHotkeyOk = HotKeys.Register(hwnd, HK_TOGGLE, store.config.toggleHotkey);
            if (!ToggleHotkeyOk)
            {
                // 被别的软件占用了, 自动换成第一个能用的组合
                foreach (string candidate in SettingsForm.HotkeyPresets)
                {
                    if (string.Equals(candidate, store.config.toggleHotkey, StringComparison.OrdinalIgnoreCase)) continue;
                    if (HotKeys.Register(hwnd, HK_TOGGLE, candidate))
                    {
                        store.config.toggleHotkey = candidate;
                        if (!Program.SelfTest) store.Save();   // 自测时不要动用户的设置
                        ToggleHotkeyOk = true;
                        Panel.Toast("原快捷键被别的软件占用了, 已换成 " + HotKeys.Pretty(candidate), 5000);
                        break;
                    }
                }
            }
            if (store.config.hotbarGlobal)
            {
                for (int i = 1; i <= 9; i++) HotKeys.Register(hwnd, HK_HOTBAR_BASE + i, "ctrl+alt+" + i);
            }
        }

        public void OnHotkey(int id)
        {
            if (id == HK_TOGGLE)
            {
                Log.Write("唤出键: 切换模式=" + store.config.hotkeyToggles + " · 当前可见=" + Panel.Visible);
                if (store.config.hotkeyToggles) Toggle();
                else Summon();                 // 默认: 只负责叫出来, 不切换隐藏
                return;
            }
            if (id > HK_DIGIT_BASE && id <= HK_DIGIT_BASE + Store.HotbarSize)
            {
                LaunchHotbar(id - HK_DIGIT_BASE - 1);
                return;
            }
            int index = id - HK_HOTBAR_BASE - 1;
            if (index >= 0 && index < Store.HotbarSize)
            {
                LaunchHotbar(index);
            }
        }

        void LaunchHotbar(int index)
        {
            Item item = store.Get(true, index);
            if (item != null)
            {
                Launch(item);
            }
            else
            {
                Panel.Toast("快捷栏第 " + (index + 1) + " 格还是空的, 把常用的从背包拖过来", 2600);
            }
        }

        /// <summary>背包开着的时候, 让数字键 1-9 直接启动快捷栏 (不用管焦点在哪)。</summary>
        void RegisterDigitHotkeys(bool on)
        {
            IntPtr hwnd;
            try { hwnd = Panel.Handle; }
            catch (Exception) { return; }
            if (on == digitHotkeysOn) return;      // 状态没变就别折腾
            digitHotkeysOn = on;
            int count = 0;
            for (int i = 1; i <= Store.HotbarSize; i++)
            {
                HotKeys.Unregister(hwnd, HK_DIGIT_BASE + i);
                if (!on) continue;
                bool ok = HotKeys.RegisterRaw(hwnd, HK_DIGIT_BASE + i, 0, 0x30 + i);
                if (!ok) Log.Write("数字键 " + i + " 注册失败 (可能被别的软件占用)");
                else count++;
            }
            Log.Write(on ? ("背包成为当前窗口: 数字键 1-9 已接管 (" + count + " 个)")
                         : "背包不再是当前窗口: 数字键 1-9 已还给别的程序");
        }

        public bool HandleWindowMessage(ref Message m)
        {
            if (m.Msg == HotKeys.WM_HOTKEY)
            {
                OnHotkey(m.WParam.ToInt32());
                return true;
            }
            if (showMessage != 0 && m.Msg == showMessage)
            {
                Log.Write("收到唤起消息, 打开背包");
                ShowPanel();
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------- 启动程序
        public void Launch(Item item)
        {
            if (item == null) return;
            string path = item.path;
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Panel.Toast("这个快捷方式指向的文件不见了", 3000);
                return;
            }
            if (store.config.hideOnLaunch) HidePanel();
            Timer timer = new Timer();
            timer.Interval = 150;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                try { Store.Launch(path); }
                catch (Exception exc)
                {
                    ShowPanel();
                    Panel.Toast("启动失败: " + exc.Message, 4000);
                }
            };
            timer.Start();
        }

        // ---------------------------------------------------------- 弹窗
        public void ShowPopup(string title, List<KeyValuePair<string, Action>> items, Point screenPoint)
        {
            ClosePopup();
            popup = new PopupForm(title, items, store.config.scale, screenPoint, Panel.Size);
            popup.Show(Panel);
            popup.TopMost = store.config.topMost;
            popup.Activate();
        }

        public bool ClosePopup()
        {
            if (popup == null) return false;
            PopupForm old = popup;
            popup = null;
            try { old.Close(); } catch (Exception) { }
            return true;
        }

        public PopupForm CurrentPopup { get { return popup; } }

        /// <summary>在窗口顶部右键弹出的"窗口菜单" (压暗/置顶/回中央这些都在这里)。</summary>
        public void ShowWindowMenu(Point screenPoint)
        {
            List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();
            items.Add(new KeyValuePair<string, Action>(
                (store.config.dim ? "关闭" : "打开") + "「打开时压暗桌面」", delegate
                {
                    store.config.dim = !store.config.dim;
                    store.Save();
                    ApplyDim();
                    if (Settings != null) Settings.Rebuild();
                }));
            items.Add(new KeyValuePair<string, Action>(
                (store.config.topMost ? "关闭" : "打开") + "「始终在最顶端」", delegate
                {
                    store.config.topMost = !store.config.topMost;
                    store.Save();
                    ApplyTopMost();
                    if (Settings != null) Settings.Rebuild();
                }));
            items.Add(new KeyValuePair<string, Action>(
                (store.config.hideOnLaunch ? "关闭" : "打开") + "「启动后收起背包」", delegate
                {
                    store.config.hideOnLaunch = !store.config.hideOnLaunch;
                    store.Save();
                    if (Settings != null) Settings.Rebuild();
                }));
            items.Add(new KeyValuePair<string, Action>(null, null));
            items.Add(new KeyValuePair<string, Action>("回到屏幕中央", CenterPanel));
            items.Add(new KeyValuePair<string, Action>("打开设置…", OpenSettings));
            items.Add(new KeyValuePair<string, Action>("取消", null));
            ShowPopup("窗口", items, screenPoint);
        }

        // ---------------------------------------------------------- 添加 / 导入
        public void AddFiles()
        {
            ClosePopup();
            if (!Panel.Visible) ShowPanel();
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要装进背包的程序";
                dialog.Multiselect = true;
                dialog.Filter = "程序 / 快捷方式|*.exe;*.lnk;*.bat;*.cmd;*.url;*.msi|所有文件|*.*";
                List<string> desktops = ShellPaths.DesktopDirs();
                if (desktops.Count > 0) dialog.InitialDirectory = desktops[0];
                if (dialog.ShowDialog(Panel) == DialogResult.OK)
                    ImportPaths(dialog.FileNames, "你选择的程序");
            }
            ForceForeground(Panel.Handle);
        }

        /// <summary>换皮肤: 挑一张 64x64 的 PNG。</summary>
        public void ChangeSkin()
        {
            ClosePopup();
            if (!Panel.Visible) ShowPanel();
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选一张 64x64 的 Minecraft 皮肤";
                dialog.Filter = "皮肤图片|*.png";
                string auto = Skin.AutoFind();
                if (!string.IsNullOrEmpty(auto))
                    dialog.InitialDirectory = Path.GetDirectoryName(auto);
                if (dialog.ShowDialog(Panel) == DialogResult.OK) SetSkin(dialog.FileName);
            }
            ForceForeground(Panel.Handle);
        }

        public bool SetSkin(string path)
        {
            Skin loaded = store.SetSkin(path);
            if (loaded == null)
            {
                Panel.Toast("这张图不是 64x64 或 64x32 的皮肤文件", 3800);
                return false;
            }
            skin = loaded;
            Panel.skin = loaded;
            Panel.Invalidate();
            Panel.Toast("皮肤换好了: " + loaded.Source, 2600);
            return true;
        }

        public void ScanMenu()
        {
            ClosePopup();
            List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();
            items.Add(new KeyValuePair<string, Action>("导入桌面上的快捷方式", delegate { ImportFrom("desktop"); }));
            items.Add(new KeyValuePair<string, Action>("导入开始菜单里的程序", delegate { ImportFrom("startmenu"); }));
            items.Add(new KeyValuePair<string, Action>("桌面 + 开始菜单都要", delegate { ImportFrom("both"); }));
            items.Add(new KeyValuePair<string, Action>("取消", null));
            Point center = new Point(Panel.Left + Panel.Width / 2, Panel.Top + Panel.Height / 2);
            ShowPopup("从哪里找程序?", items, center);
        }

        public void ImportFrom(string source)
        {
            ClosePopup();
            List<string> paths = new List<string>();
            string label;
            if (source == "desktop")
            {
                paths = ShellPaths.Scan(ShellPaths.DesktopDirs(), false);
                label = "桌面快捷方式";
            }
            else if (source == "startmenu")
            {
                paths = ShellPaths.Scan(ShellPaths.StartMenuDirs(), true);
                label = "开始菜单程序";
            }
            else
            {
                paths.AddRange(ShellPaths.Scan(ShellPaths.DesktopDirs(), false));
                paths.AddRange(ShellPaths.Scan(ShellPaths.StartMenuDirs(), true));
                label = "桌面和开始菜单";
            }
            List<string> fresh = new List<string>();
            foreach (string path in paths)
            {
                bool hot;
                int index;
                if (!store.FindPath(path, out hot, out index)) fresh.Add(path);
            }
            if (fresh.Count == 0)
            {
                Panel.Toast("没有找到新的程序", 2600);
                return;
            }
            ImportPaths(fresh.ToArray(), label);
        }

        /// <summary>分批导入, 每批之间让界面喘口气。</summary>
        public void ImportPaths(string[] paths, string label)
        {
            List<string> queue = new List<string>();
            foreach (string path in paths)
            {
                if (File.Exists(path) || Directory.Exists(path)) queue.Add(path);
            }
            if (queue.Count == 0)
            {
                Panel.Toast("没有可导入的文件", 2600);
                return;
            }
            EnsureCapacity(queue.Count);
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in store.AllItems()) names.Add(item.name);

            int cursor = 0, added = 0, dup = 0, full = 0;
            Panel.Toast("正在导入" + label + "…", 60000);
            Timer timer = new Timer();
            timer.Interval = 15;
            timer.Tick += delegate
            {
                int budget = 6;
                while (budget-- > 0 && cursor < queue.Count)
                {
                    string path = queue[cursor++];
                    bool hot;
                    int index;
                    if (store.FindPath(path, out hot, out index)) { dup++; continue; }
                    Item item = Store.MakeItem(path);
                    if (names.Contains(item.name)) { dup++; continue; }
                    int slot = store.FirstEmpty(false);
                    if (slot < 0)
                    {
                        full += queue.Count - cursor + 1;
                        cursor = queue.Count;
                        break;
                    }
                    store.Set(false, slot, item);
                    store.EnsureIcon(item);
                    names.Add(item.name);
                    added++;
                }
                if (cursor < queue.Count)
                {
                    Panel.Toast("正在导入" + label + "… 已加入 " + added + " 个", 60000);
                    return;
                }
                timer.Stop();
                timer.Dispose();
                store.Save();
                Panel.Invalidate();
                string message = "新装了 " + added + " 个";
                if (dup > 0) message += " · 跳过重复 " + dup + " 个";
                if (full > 0) message += " · 装不下 " + full + " 个";
                Panel.Toast(message, 4200);
            };
            timer.Start();
        }

        void EnsureCapacity(int needed)
        {
            int empty = 0;
            foreach (Item item in store.bag) if (item == null) empty++;
            if (needed <= empty) return;
            int used = store.bag.Count - empty;
            // 现在每页固定 3x9=27 格, 装不下就自动加"页"
            int per = store.BagPerPage;
            int target = (int)Math.Ceiling((used + needed) / (double)per);
            if (target > store.BagPages && target <= 9)
            {
                store.config.bagPages = target;
                store.ResizeSlots();
                store.Save();
                ApplySettings(true);
            }
        }

        /// <summary>把图标缓存全部删掉重新取一遍 (图标变了 / 之前取到的是通用图标时用)。</summary>
        public void RefreshIcons()
        {
            try
            {
                foreach (string file in Directory.GetFiles(store.IconsDir, "*.png")) File.Delete(file);
            }
            catch (Exception) { }
            foreach (Item item in store.AllItems())
            {
                item.icon = null;
                store.EnsureIcon(item);
            }
            store.Save();
            Panel.ClearIconCache();
            Panel.Invalidate();
        }

        // ---------------------------------------------------------- 设置
        public void OpenSettings()
        {
            ClosePopup();
            if (Settings != null) CloseSettings();
            Settings = new SettingsForm(this);
            Rectangle area = CurrentScreen().WorkingArea;
            int sx = store.config.setX, sy = store.config.setY;
            Rectangle vs = SystemInformation.VirtualScreen;
            bool valid = (sx != -1 || sy != -1) &&
                         sx >= vs.Left - Settings.Width + 180 && sx <= vs.Right - 180 &&
                         sy >= vs.Top && sy <= vs.Bottom - 40;
            Settings.Location = valid
                ? new Point(sx, sy)
                : new Point(area.Left + Math.Max(0, (area.Width - Settings.Width) / 2),
                            area.Top + Math.Max(0, (area.Height - Settings.Height) / 2));
            Settings.Show(Panel);
            Settings.TopMost = store.config.topMost;
            Settings.Activate();
            ForceForeground(Settings.Handle);
        }

        /// <summary>用户把设置窗拖到哪就记下来。</summary>
        public void SaveSettingsPosition(Point location)
        {
            store.config.setX = location.X;
            store.config.setY = location.Y;
            store.Save();
        }

        public void CloseSettings()
        {
            if (Settings == null) return;
            SettingsForm old = Settings;
            Settings = null;
            try { old.Close(); } catch (Exception) { }
            if (Panel.Visible) Panel.Focus();
        }

        public void ApplySettings(bool rebuild)
        {
            Panel.Rebuild();
            ApplyTopMost();
            ApplyDim();
            if (Panel.Visible)
            {
                Panel.Location = PanelLocation();
            }
            RegisterHotkeys();
            if (Settings != null) Settings.Rebuild();
        }

        public void Quit()
        {
            try { store.Save(); } catch (Exception) { }
            try { RegisterHotkeysCleanup(); } catch (Exception) { }
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
            }
            Panel.Hide();
            dim.Hide();
            ExitThread();
            // 兜底: 万一还有东西拖着不肯退出, 0.6 秒后强制结束
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(600);
                Environment.Exit(0);
            });
        }

        void RegisterHotkeysCleanup()
        {
            IntPtr hwnd = Panel.Handle;
            for (int i = 0; i <= 9; i++) HotKeys.Unregister(hwnd, HK_HOTBAR_BASE + i);
            for (int i = 1; i <= Store.HotbarSize; i++) HotKeys.Unregister(hwnd, HK_DIGIT_BASE + i);
            HotKeys.Unregister(hwnd, HK_TOGGLE);
        }

    }

    static class Program
    {
        /// <summary>自测模式: 不动用户设置、不弹别人窗口。</summary>
        public static bool SelfTest;

        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int value);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string message);

        const string MutexName = "MinecraftBag_SingleInstance";

        // 一定要用静态字段存住它: 局部变量的互斥体会被 GC 回收掉句柄,
        // 那样"只允许一个实例"的保护就悄悄失效了 (会开出好几个进程)。
        static System.Threading.Mutex instanceMutex;

        [STAThread]
        static void Main(string[] args)
        {
            bool hidden = Array.IndexOf(args, "--hidden") >= 0;
            bool selftest = Array.IndexOf(args, "--selftest") >= 0;
            SelfTest = selftest;
            string dataDir = ArgValue(args, "--data-dir");
            string shot = ArgValue(args, "--shot");
            string shotSettings = ArgValue(args, "--shot-settings");
            int demo = ArgInt(args, "--demo");
            bool refreshIcons = Array.IndexOf(args, "--refresh-icons") >= 0;

            bool created;
            instanceMutex = new System.Threading.Mutex(false, MutexName, out created);
            bool another = !created;
            if (!another && AnotherInstanceAlive()) another = true;   // 兜底: 看进程表
            if (another && !selftest)
            {
                Log.Write("已经有一个背包在跑了, 先试试把它唤出来");
                if (NotifyExistingInstance())
                {
                    Log.Write("已唤起已有窗口, 这次不重复启动");
                    return;
                }
                // 唤不动: 可能是那个实例卡住了, 或者它的窗口不在当前桌面上。
                // 这种情况宁可多开一个, 也不能让你点了没反应。
                Log.Write("唤不动已有实例, 这次改为自己启动一个");
            }

            try { SetProcessDpiAwareness(2); }
            catch (Exception) { try { SetProcessDPIAware(); } catch (Exception) { } }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                Log.Write("界面异常: " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Write("未处理异常: " + e.ExceptionObject);
            };

            BagApp app = new BagApp(dataDir);
            string scan = ArgValue(args, "--scan");
            if (demo > 0)
            {
                List<string> paths = ShellPaths.Scan(ShellPaths.StartMenuDirs(), true);
                if (paths.Count > demo) paths = paths.GetRange(0, demo);
                app.ImportPaths(paths.ToArray(), "示例程序");
            }
            else if (!string.IsNullOrEmpty(scan))
            {
                app.ImportFrom(scan);
            }
            if (refreshIcons) app.RefreshIcons();

            if (selftest || !hidden)
            {
                app.ShowPanel();
                if (app.store.AllItems().Count == 0)
                    app.Panel.Toast("点左上角齿轮 → 扫描导入", 6000);
            }

            if (selftest)
            {
                string target = string.IsNullOrEmpty(shot) ? Path.Combine(Store.AppDir(), "selftest.png") : shot;
                string stem = target.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? target.Substring(0, target.Length - 4)
                    : target;
                int step = 0;
                Timer timer = new Timer();
                timer.Interval = 1500;
                timer.Tick += delegate
                {
                    step++;
                    try
                    {
                        switch (step)
                        {
                            case 1:
                                SaveForm(app.Panel, target);
                                break;
                            case 2:
                                app.Panel.TestHover(false, 1);
                                Application.DoEvents();
                                SaveForm(app.Panel, stem + "_hover.png");
                                break;
                            case 3:
                                app.Panel.TestHold(false, 0,
                                    new Point(app.Panel.layout.Cell * 2 + 12, app.Panel.layout.Cell * 3 + 12));
                                Application.DoEvents();
                                SaveForm(app.Panel, stem + "_hold.png");
                                break;
                            case 4:
                                app.Panel.CancelHold();
                                app.ScanMenu();
                                Application.DoEvents();
                                SaveComposite(app.Panel, app.CurrentPopup, stem + "_popup.png");
                                break;
                            case 5:
                                app.ClosePopup();
                                app.OpenSettings();
                                Application.DoEvents();
                                SaveForm(app.Settings, string.IsNullOrEmpty(shotSettings) ? stem + "_settings.png" : shotSettings);
                                break;
                        }
                    }
                    catch (Exception exc) { Log.Write("截图失败: " + exc); }
                    if (step >= 5)
                    {
                        timer.Stop();
                        app.Quit();
                    }
                };
                timer.Start();
            }

            Application.Run(app);
        }

        static string ArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        /// <summary>进程表里有没有另一个自己在跑。</summary>
        static bool AnotherInstanceAlive()
        {
            try
            {
                Process current = Process.GetCurrentProcess();
                Process[] all = Process.GetProcessesByName(current.ProcessName);
                foreach (Process p in all)
                {
                    if (p.Id == current.Id) continue;
                    try
                    {
                        if (!p.HasExited) return true;
                    }
                    catch (Exception) { return true; }
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>让已经在跑的那个实例把背包弹出来; 成功返回 true。
        /// 窗口可能还没建好, 所以重试一会儿; 一直找不到就返回 false (调用方决定是否自己启动)。</summary>
        static bool NotifyExistingInstance()
        {
            int message = RegisterWindowMessage("MinecraftBag_ShowPanel_Message");
            if (message == 0) return false;
            for (int i = 0; i < 25; i++)
            {
                IntPtr hwnd = FindWindow(null, "我的世界背包");
                if (hwnd != IntPtr.Zero)
                {
                    Log.Write("唤起已有窗口 hwnd=" + hwnd);
                    PostMessage(hwnd, message, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
                System.Threading.Thread.Sleep(100);
            }
            Log.Write("找不到已有窗口, 广播一次试试");
            PostMessage(new IntPtr(0xFFFF), message, IntPtr.Zero, IntPtr.Zero);
            System.Threading.Thread.Sleep(400);
            return FindWindow(null, "我的世界背包") != IntPtr.Zero;
        }

        static int ArgInt(string[] args, string name)
        {
            string value = ArgValue(args, name);
            int number;
            return int.TryParse(value, out number) ? number : 0;
        }

        static void SaveForm(Form form, string path)
        {
            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            Log.Write("已保存截图 " + path);
        }

        static void SaveComposite(Form panel, Form extra, string path)
        {
            using (Bitmap bmp = new Bitmap(panel.Width, panel.Height))
            {
                panel.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                if (extra != null)
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    using (Bitmap pop = new Bitmap(extra.Width, extra.Height))
                    {
                        extra.DrawToBitmap(pop, new Rectangle(0, 0, pop.Width, pop.Height));
                        int x = extra.Left - panel.Left;
                        int y = extra.Top - panel.Top;
                        g.DrawImageUnscaled(pop, x, y);
                    }
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            Log.Write("已保存截图 " + path);
        }
    }
}
