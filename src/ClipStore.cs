using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ClipLite
{
    internal class ClipFormat
    {
        public string Name;
        public long Offset;
        public int Length;
    }

    internal class Clip
    {
        public long Seq;
        public DateTime Time;
        public string Source = "";
        public string Title = "";
        public string Hash = "";
        public string Collection;
        public string Preview = "";
        public string FilePath;
        public List<ClipFormat> Formats = new List<ClipFormat>();

        public bool Has(string name) { return Formats.Any(f => f.Name == name); }
        public int TotalBytes { get { return Formats.Sum(f => f.Length); } }
        public string Kind
        {
            get
            {
                if (Has(Fmt.Png)) return "image";
                if (Has(Fmt.Files)) return "files";
                if (Has(Fmt.Html) || Has(Fmt.Rtf)) return "rich";
                return "text";
            }
        }
    }

    internal static class Fmt
    {
        public const string Text = "Text", Html = "HTML", Rtf = "RTF", Png = "PNG", Files = "Files";
    }

    internal class ClipStore
    {
        public const string InBox = "InBox", Safe = "Safe", Trash = "Trash";

        /// <summary>Collections in tree order. Safe holds clips the user wants kept; it is never trimmed.</summary>
        public static readonly string[] Collections = { InBox, Safe, Trash };
        const int Magic = 0x31504C43; // "CLP1"
        const int PreviewChars = 4000;

        readonly string root;
        readonly Settings settings;
        public readonly Dictionary<string, List<Clip>> Lists = new Dictionary<string, List<Clip>>();
        public int InBoxLimit = 1000, TrashLimit = 1000;

        public ClipStore(string root, Settings settings)
        {
            this.root = root;
            this.settings = settings;
            InBoxLimit = settings.GetInt("InBoxLimit", 1000);
            TrashLimit = settings.GetInt("TrashLimit", 1000);
            foreach (var c in Collections)
            {
                Directory.CreateDirectory(Path.Combine(root, c));
                Lists[c] = new List<Clip>();
            }
        }

        public long LastSeq { get { return settings.GetLong("LastSeq", 0); } }

        public void LoadAll()
        {
            foreach (var c in Lists.Keys.ToList())
            {
                var list = Lists[c];
                list.Clear();
                foreach (var f in Directory.GetFiles(Path.Combine(root, c), "*.clp"))
                {
                    var clip = ReadHeader(f, c);
                    if (clip != null) list.Add(clip);
                }
                list.Sort((a, b) => b.Time.CompareTo(a.Time));
            }
        }

        Clip ReadHeader(string file, string collection)
        {
            try
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var r = new BinaryReader(fs, Encoding.UTF8))
                {
                    if (r.ReadInt32() != Magic) return null;
                    var clip = new Clip { FilePath = file, Collection = collection };
                    clip.Seq = r.ReadInt64();
                    clip.Time = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
                    clip.Source = r.ReadString();
                    clip.Title = r.ReadString();
                    clip.Hash = r.ReadString();
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                        clip.Formats.Add(new ClipFormat { Name = r.ReadString(), Length = r.ReadInt32() });
                    long off = fs.Position;
                    foreach (var f in clip.Formats) { f.Offset = off; off += f.Length; }
                    var text = clip.Formats.FirstOrDefault(f => f.Name == Fmt.Text) ?? clip.Formats.FirstOrDefault(f => f.Name == Fmt.Files);
                    if (text != null)
                    {
                        fs.Position = text.Offset;
                        var bytes = r.ReadBytes(Math.Min(text.Length, PreviewChars * 3));
                        var s = Encoding.UTF8.GetString(bytes);
                        clip.Preview = s.Length > PreviewChars ? s.Substring(0, PreviewChars) : s;
                    }
                    return clip;
                }
            }
            catch { return null; }
        }

        public byte[] Read(Clip clip, string name)
        {
            var f = clip.Formats.FirstOrDefault(x => x.Name == name);
            if (f == null) return null;
            try
            {
                using (var fs = new FileStream(clip.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fs.Position = f.Offset;
                    var buf = new byte[f.Length];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int k = fs.Read(buf, read, buf.Length - read);
                        if (k <= 0) break;
                        read += k;
                    }
                    return buf;
                }
            }
            catch { return null; }
        }

        public string ReadString(Clip clip, string name)
        {
            var b = Read(clip, name);
            return b == null ? null : Encoding.UTF8.GetString(b);
        }

        public List<KeyValuePair<string, byte[]>> ReadAll(Clip clip)
        {
            return clip.Formats.Select(f => new KeyValuePair<string, byte[]>(f.Name, Read(clip, f.Name)))
                               .Where(kv => kv.Value != null).ToList();
        }

        public static string ComputeHash(List<KeyValuePair<string, byte[]>> data)
        {
            using (var md5 = MD5.Create())
            {
                foreach (var kv in data)
                {
                    var nb = Encoding.UTF8.GetBytes(kv.Key);
                    md5.TransformBlock(nb, 0, nb.Length, null, 0);
                    md5.TransformBlock(kv.Value, 0, kv.Value.Length, null, 0);
                }
                md5.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(md5.Hash).Replace("-", "");
            }
        }

        /// <summary>
        /// Adds a captured clip. When the same content is already in the InBox nothing new is
        /// stored: the existing clip moves back to the top and <paramref name="duplicate"/> is set.
        /// </summary>
        public Clip Add(List<KeyValuePair<string, byte[]>> data, string source, string title, out bool duplicate)
        {
            var hash = ComputeHash(data);
            var inbox = Lists[InBox];
            var dup = inbox.FirstOrDefault(c => c.Hash == hash);
            duplicate = dup != null;
            if (dup != null)
            {
                dup.Time = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(source)) dup.Source = source;
                Rewrite(dup, ReadAll(dup));
                inbox.Remove(dup);
                inbox.Insert(0, dup);
                return dup;
            }

            long seq = LastSeq + 1;
            settings.Set("LastSeq", seq);
            settings.Save();
            var clip = new Clip
            {
                Seq = seq, Time = DateTime.UtcNow, Source = source ?? "", Title = title ?? "",
                Hash = hash, Collection = InBox,
                FilePath = Path.Combine(root, InBox, seq.ToString("D10") + ".clp")
            };
            Rewrite(clip, data);
            inbox.Insert(0, clip);
            EnforceLimits();
            return clip;
        }

        void Rewrite(Clip clip, List<KeyValuePair<string, byte[]>> data)
        {
            // Text first so ReadHeader can grab the preview cheaply.
            data = data.OrderBy(kv => kv.Key == Fmt.Text ? 0 : 1).ToList();
            var tmp = clip.FilePath + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                w.Write(Magic);
                w.Write(clip.Seq);
                w.Write(clip.Time.Ticks);
                w.Write(clip.Source ?? "");
                w.Write(clip.Title ?? "");
                w.Write(clip.Hash ?? "");
                w.Write(data.Count);
                foreach (var kv in data) { w.Write(kv.Key); w.Write(kv.Value.Length); }
                clip.Formats.Clear();
                long off = fs.Position;
                foreach (var kv in data)
                {
                    clip.Formats.Add(new ClipFormat { Name = kv.Key, Offset = off, Length = kv.Value.Length });
                    off += kv.Value.Length;
                }
                foreach (var kv in data) w.Write(kv.Value);
            }
            if (File.Exists(clip.FilePath)) File.Delete(clip.FilePath);
            File.Move(tmp, clip.FilePath);
            var text = data.FirstOrDefault(kv => kv.Key == Fmt.Text || kv.Key == Fmt.Files);
            if (text.Value != null)
            {
                var s = Encoding.UTF8.GetString(text.Value);
                clip.Preview = s.Length > PreviewChars ? s.Substring(0, PreviewChars) : s;
            }
            else clip.Preview = "";
        }

        public void UpdateText(Clip clip, string text)
        {
            var data = ReadAll(clip).Where(kv => kv.Key != Fmt.Text && kv.Key != Fmt.Html && kv.Key != Fmt.Rtf).ToList();
            data.Insert(0, new KeyValuePair<string, byte[]>(Fmt.Text, Encoding.UTF8.GetBytes(text)));
            clip.Hash = ComputeHash(data);
            clip.Title = MakeTitle(text);
            Rewrite(clip, data);
        }

        public void Rename(Clip clip, string title)
        {
            clip.Title = title;
            Rewrite(clip, ReadAll(clip));
        }

        public void Move(Clip clip, string collection)
        {
            if (clip.Collection == collection) return;
            var dest = Path.Combine(root, collection, Path.GetFileName(clip.FilePath));
            try
            {
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(clip.FilePath, dest);
            }
            catch { return; }
            Lists[clip.Collection].Remove(clip);
            clip.FilePath = dest;
            clip.Collection = collection;
            var list = Lists[collection];
            int i = list.FindIndex(c => c.Time < clip.Time);
            list.Insert(i < 0 ? list.Count : i, clip);
            EnforceLimits();
        }

        public void Delete(Clip clip)
        {
            try { File.Delete(clip.FilePath); } catch { }
            Lists[clip.Collection].Remove(clip);
        }

        /// <summary>Applies new collection limits, trimming whatever no longer fits.</summary>
        public void ApplyLimits(int inbox, int trash)
        {
            InBoxLimit = Math.Max(10, inbox);
            TrashLimit = Math.Max(10, trash);
            settings.Set("InBoxLimit", InBoxLimit);
            settings.Set("TrashLimit", TrashLimit);
            EnforceLimits();
        }

        void EnforceLimits()
        {
            var inbox = Lists[InBox];
            while (inbox.Count > InBoxLimit) Move(inbox[inbox.Count - 1], Trash);
            var trash = Lists[Trash];
            while (trash.Count > TrashLimit) Delete(trash[trash.Count - 1]);
        }

        public static string MakeTitle(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                line = line.Replace('\t', ' ');
                return line.Length > 160 ? line.Substring(0, 160) : line;
            }
            return text.Trim().Length == 0 ? "(whitespace)" : text.Trim();
        }
    }
}
