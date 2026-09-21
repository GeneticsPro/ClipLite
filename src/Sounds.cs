using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Reflection;

namespace ClipLite
{
    internal enum SoundEvent { Capture, Erase, Ignore }

    /// <summary>
    /// Event sounds. The built-in set is embedded in the executable as 16-bit PCM WAV
    /// (assets/sounds/*.wav, see build.cmd) — nothing extra has to be shipped next to the exe.
    /// Setting format per event: "off" | "default:&lt;preset&gt;" | "custom:&lt;path to .wav&gt;".
    /// </summary>
    internal static class Sounds
    {
        /// <summary>Built-in presets; each name is also the embedded resource base name.</summary>
        public static readonly string[] Presets = { "Capture", "Erase", "Ignore", "Append" };

        public static string DefaultPreset(SoundEvent ev)
        {
            switch (ev)
            {
                case SoundEvent.Erase: return "Erase";
                case SoundEvent.Ignore: return "Ignore";
                default: return "Capture";
            }
        }

        public static string Key(SoundEvent ev) { return "Sound." + ev; }

        public static string GetSetting(Settings s, SoundEvent ev)
        {
            var fallback = "default:" + DefaultPreset(ev);
            var setting = s.Get(Key(ev), fallback);
            if (string.IsNullOrEmpty(setting)) return fallback;
            if (setting == "off" || setting.StartsWith("custom:")) return setting;
            // a preset from an older build (synthesized set) no longer exists — fall back
            return IsPreset(PresetOf(setting)) ? setting : fallback;
        }

        /// <summary>Preset name of a "default:x" / bare setting value.</summary>
        public static string PresetOf(string setting)
        {
            return setting != null && setting.StartsWith("default:") ? setting.Substring(8) : setting;
        }

        public static bool IsPreset(string name)
        {
            return Array.IndexOf(Presets, name) >= 0;
        }

        static readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
        static SoundPlayer player;

        public static void Play(Settings s, SoundEvent ev)
        {
            PlaySetting(GetSetting(s, ev));
        }

        public static void PlaySetting(string setting)
        {
            try
            {
                if (string.IsNullOrEmpty(setting) || setting == "off") return;
                byte[] wav;
                if (setting.StartsWith("custom:"))
                {
                    var path = setting.Substring(7);
                    if (!File.Exists(path)) return;
                    wav = File.ReadAllBytes(path);
                }
                else
                {
                    wav = Builtin(PresetOf(setting));
                    if (wav == null) return;
                }
                if (player != null) player.Dispose();
                player = new SoundPlayer(new MemoryStream(wav));
                player.Play(); // async, returns immediately
            }
            catch { }
        }

        static byte[] Builtin(string name)
        {
            byte[] wav;
            if (cache.TryGetValue(name, out wav)) return wav;
            if (!IsPreset(name)) return null;
            using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("ClipLite.Sounds." + name + ".wav"))
            {
                if (src == null) return null;
                wav = new byte[src.Length];
                for (int read = 0, n; read < wav.Length; read += n)
                {
                    n = src.Read(wav, read, wav.Length - read);
                    if (n <= 0) return null;
                }
            }
            cache[name] = wav;
            return wav;
        }
    }
}
