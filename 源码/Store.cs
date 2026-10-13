// 数据: 背包 / 快捷栏的内容、设置, 以及桌面和开始菜单的扫描。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace MCBag
{
    public class Item
    {
        public string name;
        public string path;
        public string kind;
        public string icon;
    }

    public class Config
    {
        public int scale = 3;
        public int bagRows = 5;
        public int bagPages = 3;                 // 背包页数 (每页固定 3x9)
        public bool dim = true;
        public bool showNames = true;
        public bool hotbarGlobal = true;
        public string toggleHotkey = "ctrl+alt+e";
        public bool autostart = false;
        public bool topMost = true;      // 是否始终显示在最顶端
        public bool hideOnLaunch = true; // 启动程序后是否收起背包
        public bool hotkeyToggles = false; // 唤出键再按一次是否收起 (默认只唤出)
        public int winX = -1;            // 窗口位置 (-1 = 自动居中)
        public int winY = -1;
        public int setX = -1;            // 设置窗位置
        public int setY = -1;
    }

    public static class ShellPaths
    {
        static readonly HashSet<string> Want = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".lnk", ".url", ".exe", ".bat", ".cmd" };

        static readonly Regex Skip = new Regex(
            "(卸载|uninstall|readme|read me|说明|帮助|help|license|许可|官网|网站|更新|update|修复|repair|反安装|debug)",
            RegexOptions.IgnoreCase);

        public static List<string> DesktopDirs()
        {
            List<string> list = new List<string>();
            Add(list, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            Add(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            Add(list, RegistryDesktop());
            Add(list, Path.Combine(Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public", "Desktop"));

            // Windows 会把桌面重定向到 OneDrive, 而且沙箱 / 多用户环境下 USERPROFILE
            // 不一定对, 所以从几个已知桌面目录反推出用户目录再找一遍。
            List<string> homes = new List<string>();
            AddHome(homes, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            AddHome(homes, ParentOf(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
            AddHome(homes, ParentOf(RegistryDesktop()));
            AddHome(homes, ParentOf(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)));
            foreach (string home in homes)
            {
                Add(list, Path.Combine(home, "Desktop"));
                Add(list, Path.Combine(home, "桌面"));
                Add(list, Path.Combine(home, "OneDrive", "Desktop"));
                Add(list, Path.Combine(home, "OneDrive", "桌面"));
                try
                {
                    foreach (string sub in Directory.EnumerateDirectories(home))
                    {
                        string name = Path.GetFileName(sub);
                        if (name.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("Dropbox", StringComparison.OrdinalIgnoreCase))
                        {
                            Add(list, Path.Combine(sub, "Desktop"));
                            Add(list, Path.Combine(sub, "桌面"));
                        }
                    }
                }
                catch (Exception) { }
            }
            return list;
        }

        static string ParentOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try { return Path.GetDirectoryName(path.TrimEnd('\\', '/')); }
            catch (Exception) { return null; }
        }

        static void AddHome(List<string> homes, string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            foreach (string existing in homes)
                if (string.Equals(existing, dir, StringComparison.OrdinalIgnoreCase)) return;
            homes.Add(dir);
        }

        public static List<string> StartMenuDirs()
        {
            List<string> list = new List<string>();
            Add(list, Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            Add(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
            return list;
        }

        static string RegistryDesktop()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                {
                    if (key != null)
                    {
                        string value = key.GetValue("Desktop") as string;
                        if (!string.IsNullOrEmpty(value)) return Environment.ExpandEnvironmentVariables(value);
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        static void Add(List<string> list, string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            string full;
            try { full = Path.GetFullPath(dir); }
            catch (Exception) { return; }
            if (!Directory.Exists(full)) return;
            foreach (string existing in list)
                if (string.Equals(existing, full, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(full);
        }

        public static List<string> Scan(List<string> folders, bool recursive)
        {
            List<string> found = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(folder, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                }
                catch (Exception) { continue; }
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("~$") || name.StartsWith(".")) continue;
                    if (!Want.Contains(Path.GetExtension(file))) continue;
                    if (Skip.IsMatch(name)) continue;
                    if (!seen.Add(file)) continue;
                    try
                    {
                        if (new FileInfo(file).Length == 0) continue;
                    }
                    catch (Exception) { continue; }
                    found.Add(file);
                }
            }
            return found;
        }

        public static string DisplayName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            name = Regex.Replace(name, @"\s*[-–—]\s*快捷方式$", "");
            name = name.Trim();
            return name.Length == 0 ? Path.GetFileName(path) : name;
        }
    }

    public class Store
    {
        public const int Cols = 9;
        public const int HotbarSize = 9;

        public Config config = new Config();
        public List<Item> bag = new List<Item>();
        public List<Item> hotbar = new List<Item>();

        public string Dir { get; private set; }
        public string IconsDir { get { return Path.Combine(Dir, "icons"); } }
        public string FilePath { get { return Path.Combine(Dir, "inventory.json"); } }

        public Store(string dirOverride)
        {
            Dir = string.IsNullOrEmpty(dirOverride) ? DefaultDir() : dirOverride;
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(IconsDir);
            Load();
        }

        public static string AppDir()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        static string DefaultDir()
        {
            string local = Path.Combine(AppDir(), "data");
            try
            {
                Directory.CreateDirectory(local);
                string probe = Path.Combine(local, ".writable");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return local;
            }
            catch (Exception)
            {
                string roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MinecraftBag");
                Directory.CreateDirectory(roaming);
                return roaming;
            }
        }

        public const int PageRows = 3;                            // 每页固定 3 行 x 9 列
        public int BagPerPage { get { return Cols * PageRows; } }
        public int BagRows { get { return PageRows; } }
        public int BagPageCount { get { return Math.Max(1, bag.Count / BagPerPage); } }
        public int BagPages { get { return Math.Max(1, Math.Min(9, config.bagPages)); } }

        /// <summary>翻页: 把前面几个"整页"的格子挪到队尾 (不丢东西, 只是显示顺序变了)。</summary>
        public void RotateBag(int blocks)
        {
            int per = BagPerPage;
            int total = bag.Count;
            if (per <= 0 || total <= per) return;
            int pages = total / per;
            blocks = ((blocks % pages) + pages) % pages;
            if (blocks == 0) return;
            List<Item> moved = bag.GetRange(0, blocks * per);
            bag.RemoveRange(0, blocks * per);
            bag.AddRange(moved);
        }

        /// <summary>批量删除: 只删当前页(滚动后就是最前面 per 格) 或全删。返回删掉的个数。</summary>
        public int ClearBag(bool currentPageOnly)
        {
            int per = BagPerPage;
            int limit = currentPageOnly ? Math.Min(per, bag.Count) : bag.Count;
            int count = 0;
            for (int i = 0; i < limit; i++)
            {
                if (bag[i] != null) { bag[i] = null; count++; }
            }
            return count;
        }

        public List<Item> Slots(bool isHotbar)
        {
            return isHotbar ? hotbar : bag;
        }

        public Item Get(bool isHotbar, int index)
        {
            List<Item> slots = Slots(isHotbar);
            if (index < 0 || index >= slots.Count) return null;
            return slots[index];
        }

        public void Set(bool isHotbar, int index, Item item)
        {
            List<Item> slots = Slots(isHotbar);
            if (index >= 0 && index < slots.Count) slots[index] = item;
        }

        public void Swap(bool hotA, int a, bool hotB, int b)
        {
            Item x = Get(hotA, a);
            Item y = Get(hotB, b);
            Set(hotA, a, y);
            Set(hotB, b, x);
        }

        public void ResizeSlots()
        {
            // 页数至少要能装下现有物品, 绝不因为调界面而丢东西
            int per = BagPerPage;
            int highest = -1;
            for (int i = 0; i < bag.Count; i++) if (bag[i] != null) highest = i;
            int needed = (highest + per) / per;
            int pages = Math.Max(BagPages, Math.Max(1, needed));
            int want = per * pages;
            while (bag.Count < want) bag.Add(null);
            while (bag.Count > want) bag.RemoveAt(bag.Count - 1);
            while (hotbar.Count < HotbarSize) hotbar.Add(null);
            while (hotbar.Count > HotbarSize) hotbar.RemoveAt(hotbar.Count - 1);
        }

        public int FirstEmpty(bool isHotbar)
        {
            List<Item> slots = Slots(isHotbar);
            for (int i = 0; i < slots.Count; i++) if (slots[i] == null) return i;
            return -1;
        }

        public List<Item> AllItems()
        {
            List<Item> list = new List<Item>();
            foreach (Item i in bag) if (i != null) list.Add(i);
            foreach (Item i in hotbar) if (i != null) list.Add(i);
            return list;
        }

        public bool FindPath(string path, out bool isHotbar, out int index)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool hot = pass == 1;
                List<Item> slots = Slots(hot);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && string.Equals(slots[i].path, path, StringComparison.OrdinalIgnoreCase))
                    {
                        isHotbar = hot;
                        index = i;
                        return true;
                    }
                }
            }
            isHotbar = false;
            index = -1;
            return false;
        }

        public static Item MakeItem(string path)
        {
            Item item = new Item();
            item.path = Path.GetFullPath(path);
            item.name = ShellPaths.DisplayName(item.path);
            string ext = Path.GetExtension(item.path).TrimStart('.').ToLowerInvariant();
            item.kind = Directory.Exists(item.path) ? "folder" : (ext.Length == 0 ? "file" : ext);
            return item;
        }

        public string EnsureIcon(Item item)
        {
            if (item == null) return null;
            if (!string.IsNullOrEmpty(item.icon))
            {
                string cached = Path.Combine(IconsDir, item.icon);
                if (File.Exists(cached) && new FileInfo(cached).Length > 0) return cached;
            }
            try
            {
                string file = ShellIcon.EnsureCached(item.path, IconsDir, 128);
                item.icon = Path.GetFileName(file);
                return file;
            }
            catch (Exception) { return null; }
        }

        // ---------------------------------------------------------- 皮肤
        public string SkinFile { get { return Path.Combine(Dir, "skin.png"); } }

        /// <summary>数据目录里的皮肤副本 -> 自动搜索游戏皮肤缓存 -> 内置默认形象。</summary>
        public Skin ResolveSkin()
        {
            string error;
            if (File.Exists(SkinFile))
            {
                Skin local = Skin.Load(SkinFile, out error);
                if (local != null)
                {
                    local.Source = "data\\skin.png";
                    return local;
                }
            }
            string found = Skin.AutoFind();
            if (!string.IsNullOrEmpty(found))
            {
                try
                {
                    File.Copy(found, SkinFile, true);
                    Skin auto = Skin.Load(SkinFile, out error);
                    if (auto != null)
                    {
                        auto.Source = "游戏里的皮肤";
                        return auto;
                    }
                }
                catch (Exception) { }
            }
            return Skin.Default();
        }

        /// <summary>用户指定的皮肤: 校验后复制到数据目录, 返回可用的 Skin, 失败返回 null。</summary>
        public Skin SetSkin(string path)
        {
            string error;
            Skin loaded = Skin.Load(path, out error);
            if (loaded == null) return null;
            try
            {
                if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(SkinFile), StringComparison.OrdinalIgnoreCase))
                    File.Copy(path, SkinFile, true);
            }
            catch (Exception) { }
            loaded.Source = Path.GetFileName(path);
            return loaded;
        }

        // ---------------------------------------------------------- 读写
        public void Load()
        {
            bag = new List<Item>();
            hotbar = new List<Item>();
            config = new Config();
            Dictionary<string, object> data = ReadJson(FilePath);
            if (data == null) data = ReadJson(FilePath + ".bak");   // 主文件坏了就用备份
            if (data != null)
            {
                object settingsObj;
                if (data.TryGetValue("settings", out settingsObj))
                    ReadConfig(settingsObj as Dictionary<string, object>);
                ReadSlots(data, "bag", bag);
                ReadSlots(data, "hotbar", hotbar);
            }
            ResizeSlots();
        }

        static Dictionary<string, object> ReadJson(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length == 0) return null;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                return ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, System.Text.Encoding.UTF8));
            }
            catch (Exception)
            {
                return null;
            }
        }

        void ReadConfig(Dictionary<string, object> settings)
        {
            if (settings == null) return;
            foreach (KeyValuePair<string, object> pair in settings)
            {
                object v = pair.Value;
                if (v == null) continue;
                bool flag;
                int number;
                switch (pair.Key)
                {
                    case "scale": if (int.TryParse(v.ToString(), out number)) config.scale = number; break;
                    case "bagRows": if (int.TryParse(v.ToString(), out number)) config.bagRows = number; break;
                    case "bagPages": if (int.TryParse(v.ToString(), out number)) config.bagPages = number; break;
                    case "dim": if (bool.TryParse(v.ToString(), out flag)) config.dim = flag; break;
                    case "showNames": if (bool.TryParse(v.ToString(), out flag)) config.showNames = flag; break;
                    case "hotbarGlobal": if (bool.TryParse(v.ToString(), out flag)) config.hotbarGlobal = flag; break;
                    case "autostart": if (bool.TryParse(v.ToString(), out flag)) config.autostart = flag; break;
                    case "topMost": if (bool.TryParse(v.ToString(), out flag)) config.topMost = flag; break;
                    case "hideOnLaunch": if (bool.TryParse(v.ToString(), out flag)) config.hideOnLaunch = flag; break;
                    case "hotkeyToggles": if (bool.TryParse(v.ToString(), out flag)) config.hotkeyToggles = flag; break;
                    case "winX": if (int.TryParse(v.ToString(), out number)) config.winX = number; break;
                    case "winY": if (int.TryParse(v.ToString(), out number)) config.winY = number; break;
                    case "setX": if (int.TryParse(v.ToString(), out number)) config.setX = number; break;
                    case "setY": if (int.TryParse(v.ToString(), out number)) config.setY = number; break;
                    case "toggleHotkey": if (!string.IsNullOrEmpty(v.ToString())) config.toggleHotkey = v.ToString(); break;
                }
            }
            config.scale = Math.Max(2, Math.Min(4, config.scale));
        }

        static void ReadSlots(Dictionary<string, object> data, string key, List<Item> target)
        {
            object raw;
            if (!data.TryGetValue(key, out raw)) return;
            // 注意: JavaScriptSerializer 把 JSON 数组读成 ArrayList (不是 object[]),
            // 所以这里必须按 IList 处理, 否则背包每次重启都会变空。
            System.Collections.IList list = raw as System.Collections.IList;
            if (list == null) return;
            foreach (object entry in list)
            {
                Dictionary<string, object> map = entry as Dictionary<string, object>;
                if (map == null) { target.Add(null); continue; }
                Item item = new Item();
                object v;
                item.name = map.TryGetValue("name", out v) && v != null ? v.ToString() : "";
                item.path = map.TryGetValue("path", out v) && v != null ? v.ToString() : "";
                item.kind = map.TryGetValue("kind", out v) && v != null ? v.ToString() : "";
                item.icon = map.TryGetValue("icon", out v) && v != null ? v.ToString() : null;
                target.Add(string.IsNullOrEmpty(item.path) ? null : item);
            }
        }

        public void Save()
        {
            string tmp = FilePath + ".tmp";
            try
            {
                Dictionary<string, object> data = new Dictionary<string, object>();
                data["version"] = 1;
                data["settings"] = new Dictionary<string, object>
                {
                    { "scale", config.scale },
                    { "bagRows", config.bagRows },
                    { "bagPages", config.bagPages },
                    { "dim", config.dim },
                    { "showNames", config.showNames },
                    { "hotbarGlobal", config.hotbarGlobal },
                    { "toggleHotkey", config.toggleHotkey },
                    { "autostart", config.autostart },
                    { "topMost", config.topMost },
                    { "hideOnLaunch", config.hideOnLaunch },
                    { "hotkeyToggles", config.hotkeyToggles },
                    { "winX", config.winX },
                    { "winY", config.winY },
                    { "setX", config.setX },
                    { "setY", config.setY },
                };
                data["bag"] = PackList(bag);
                data["hotbar"] = PackList(hotbar);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = Pretty(ser.Serialize(data));
                File.WriteAllText(tmp, json, new System.Text.UTF8Encoding(false));
            }
            catch (Exception) { return; }
            // 先留一份备份再替换, 免得写一半断电把背包弄丢
            try
            {
                if (File.Exists(FilePath))
                {
                    try { File.Copy(FilePath, FilePath + ".bak", true); }
                    catch (Exception) { }
                    File.Replace(tmp, FilePath, null);
                }
                else
                {
                    File.Move(tmp, FilePath);
                }
            }
            catch (Exception)
            {
                try
                {
                    File.Copy(tmp, FilePath, true);
                    File.Delete(tmp);
                }
                catch (Exception) { }
            }
        }

        static List<object> PackList(List<Item> slots)
        {
            List<object> list = new List<object>();
            foreach (Item item in slots)
            {
                if (item == null) { list.Add(null); continue; }
                list.Add(new Dictionary<string, object>
                {
                    { "name", item.name },
                    { "path", item.path },
                    { "kind", item.kind },
                    { "icon", item.icon },
                });
            }
            return list;
        }

        // 把 JSON 排一下版, 方便用户自己看
        static string Pretty(string json)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; sb.Append(c); break;
                    case '{':
                    case '[':
                        sb.Append(c);
                        if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']')) break;
                        indent++;
                        sb.Append('\n').Append(' ', indent);
                        break;
                    case '}':
                    case ']':
                        if (i > 0 && (json[i - 1] == '}' || json[i - 1] == ']' || json[i - 1] == 'x')) { }
                        indent = Math.Max(0, indent - 1);
                        sb.Append('\n').Append(' ', indent).Append(c);
                        break;
                    case ',':
                        sb.Append(c).Append('\n').Append(' ', indent);
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------- 系统动作
        public static void Launch(string path)
        {
            ProcessStartInfo info = new ProcessStartInfo(path);
            info.UseShellExecute = true;
            info.WorkingDirectory = Path.GetDirectoryName(path);
            Process.Start(info);
        }

        public static void Reveal(string path)
        {
            ProcessStartInfo info = new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"");
            info.UseShellExecute = true;
            Process.Start(info);
        }

        public static void SetAutostart(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    string name = "MinecraftBag";
                    if (enabled)
                    {
                        string exe = Path.Combine(AppDir(), Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location));
                        key.SetValue(name, "\"" + exe + "\" --hidden");
                    }
                    else
                    {
                        if (key.GetValue(name) != null) key.DeleteValue(name, false);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
