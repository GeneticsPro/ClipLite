using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClipLite
{
    internal class Settings
    {
        readonly string path;
        readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Settings(string path)
        {
            this.path = path;
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                int i = line.IndexOf('=');
                if (i <= 0 || line.StartsWith(";")) continue;
                values[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
        }

        public string Get(string key, string def)
        {
            string v;
            return values.TryGetValue(key, out v) ? v : def;
        }

        public int GetInt(string key, int def)
        {
            int v;
            return int.TryParse(Get(key, ""), out v) ? v : def;
        }

        public long GetLong(string key, long def)
        {
            long v;
            return long.TryParse(Get(key, ""), out v) ? v : def;
        }

        public void Set(string key, object value)
        {
            values[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; ClipLite settings. Hotkey example: Ctrl+Alt+V, Ctrl+Shift+Space, Win+Alt+C");
            foreach (var kv in values) sb.AppendLine(kv.Key + "=" + kv.Value);
            try { File.WriteAllText(path, sb.ToString(), Encoding.UTF8); } catch { }
        }
    }
}
