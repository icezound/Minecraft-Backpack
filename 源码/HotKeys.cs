// 全局快捷键 (Win32 RegisterHotKey)。
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MCBag
{
    public static class HotKeys
    {
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;
        public const int MOD_NOREPEAT = 0x4000;
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        static readonly Dictionary<string, int> mods = new Dictionary<string, int>
        {
            { "ctrl", MOD_CONTROL }, { "control", MOD_CONTROL }, { "alt", MOD_ALT },
            { "shift", MOD_SHIFT }, { "win", MOD_WIN }, { "super", MOD_WIN }, { "cmd", MOD_WIN },
        };

        static readonly Dictionary<string, int> keys = new Dictionary<string, int>
        {
            { "space", 0x20 }, { "esc", 0x1B }, { "escape", 0x1B }, { "enter", 0x0D },
            { "tab", 0x09 }, { "backspace", 0x08 }, { "`", 0xC0 }, { "-", 0xBD }, { "=", 0xBB },
            { "[", 0xDB }, { "]", 0xDD }, { "\\", 0xDC }, { ";", 0xBA }, { "'", 0xDE },
            { ",", 0xBC }, { ".", 0xBE }, { "/", 0xBF }, { "up", 0x26 }, { "down", 0x28 },
            { "left", 0x25 }, { "right", 0x27 }, { "insert", 0x2D }, { "delete", 0x2E },
            { "home", 0x24 }, { "end", 0x23 },
        };

        static HotKeys()
        {
            for (int i = 1; i <= 24; i++) keys["f" + i] = 0x6F + i;
        }

        public static bool TryParse(string spec, out int modsOut, out int vk)
        {
            modsOut = 0;
            vk = 0;
            if (string.IsNullOrEmpty(spec)) return false;
            bool hasKey = false;
            foreach (string raw in spec.ToLowerInvariant().Replace(" ", "").Split('+'))
            {
                if (raw.Length == 0) continue;
                if (mods.ContainsKey(raw)) { modsOut |= mods[raw]; continue; }
                if (keys.ContainsKey(raw)) { vk = keys[raw]; hasKey = true; continue; }
                if (raw.Length == 1 && char.IsLetterOrDigit(raw[0])) { vk = char.ToUpperInvariant(raw[0]); hasKey = true; continue; }
                return false;
            }
            if (!hasKey) return false;
            modsOut |= MOD_NOREPEAT;
            return true;
        }

        public static string Pretty(string spec)
        {
            List<string> parts = new List<string>();
            foreach (string raw in (spec ?? "").ToLowerInvariant().Split('+'))
            {
                if (raw.Length == 0) continue;
                switch (raw)
                {
                    case "ctrl":
                    case "control": parts.Add("Ctrl"); break;
                    case "alt": parts.Add("Alt"); break;
                    case "shift": parts.Add("Shift"); break;
                    case "win":
                    case "super": parts.Add("Win"); break;
                    default: parts.Add(raw.Length == 1 ? raw.ToUpperInvariant() : char.ToUpperInvariant(raw[0]) + raw.Substring(1)); break;
                }
            }
            return string.Join(" + ", parts.ToArray());
        }

        public static bool Register(IntPtr hwnd, int id, string spec)
        {
            int m, vk;
            if (!TryParse(spec, out m, out vk)) return false;
            return RegisterHotKey(hwnd, id, m, vk);
        }

        /// <summary>直接按 (修饰键, 虚拟键码) 注册。</summary>
        public static bool RegisterRaw(IntPtr hwnd, int id, int modifiers, int vk)
        {
            return RegisterHotKey(hwnd, id, modifiers | MOD_NOREPEAT, vk);
        }

        public static void Unregister(IntPtr hwnd, int id)
        {
            try { UnregisterHotKey(hwnd, id); }
            catch (Exception) { }
        }
    }
}
