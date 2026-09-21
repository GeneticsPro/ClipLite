using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ClipLite
{
    internal enum CaptureStatus { Captured, Empty, Ignored, Failed }

    internal static class ClipboardIO
    {
        public const int MaxTextBytes = 4 * 1024 * 1024;
        public const int MaxImagePixels = 8000 * 8000;

        static IDataObject GetDataObject()
        {
            for (int i = 0; i < 10; i++)
            {
                try { return Clipboard.GetDataObject(); }
                catch (ExternalException) { Thread.Sleep(30); }
            }
            return null;
        }

        /// <summary>Reads the current clipboard. Returns null when there is nothing worth keeping.</summary>
        public static List<KeyValuePair<string, byte[]>> Capture(out string title, out CaptureStatus status)
        {
            title = null;
            status = CaptureStatus.Failed;
            var obj = GetDataObject();
            if (obj == null) return null;
            string[] present;
            try { present = obj.GetFormats(false); } catch { return null; }
            if (present == null || present.Length == 0) { status = CaptureStatus.Empty; return null; }
            status = CaptureStatus.Ignored;

            // Password managers and other apps ask clipboard tools to skip their data.
            if (present.Contains("ExcludeClipboardContentFromMonitorProcessing") || present.Contains("Clipboard Viewer Ignore"))
                return null;
            if (present.Contains("CanIncludeInClipboardHistory"))
            {
                try
                {
                    var ms = obj.GetData("CanIncludeInClipboardHistory") as MemoryStream;
                    if (ms != null && ms.Length >= 4 && BitConverter.ToInt32(ms.ToArray(), 0) == 0) return null;
                }
                catch { }
            }

            var data = new List<KeyValuePair<string, byte[]>>();
            string text = null;

            try
            {
                if (obj.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = obj.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0)
                    {
                        var joined = string.Join("\r\n", files);
                        data.Add(Kv(Fmt.Files, joined));
                        title = files.Length == 1 ? Path.GetFileName(files[0]) : "[" + files.Length + " files] " + Path.GetFileName(files[0]);
                        { status = CaptureStatus.Captured; return data; }
                    }
                }
            }
            catch { }

            try
            {
                if (obj.GetDataPresent(DataFormats.UnicodeText))
                    text = obj.GetData(DataFormats.UnicodeText) as string;
                else if (obj.GetDataPresent(DataFormats.Text))
                    text = obj.GetData(DataFormats.Text) as string;
            }
            catch { }

            if (!string.IsNullOrEmpty(text))
            {
                var tb = Encoding.UTF8.GetBytes(text);
                if (tb.Length > MaxTextBytes) return null;
                data.Add(new KeyValuePair<string, byte[]>(Fmt.Text, tb));
                title = ClipStore.MakeTitle(text);

                try
                {
                    if (obj.GetDataPresent(DataFormats.Html))
                    {
                        var h = ReadRaw(obj.GetData(DataFormats.Html));
                        if (h != null && h.Length < MaxTextBytes) data.Add(new KeyValuePair<string, byte[]>(Fmt.Html, h));
                    }
                }
                catch { }
                try
                {
                    if (obj.GetDataPresent(DataFormats.Rtf))
                    {
                        var r = ReadRaw(obj.GetData(DataFormats.Rtf));
                        if (r != null && r.Length < MaxTextBytes) data.Add(new KeyValuePair<string, byte[]>(Fmt.Rtf, r));
                    }
                }
                catch { }
                { status = CaptureStatus.Captured; return data; }
            }

            try
            {
                if (obj.GetDataPresent(DataFormats.Bitmap) || present.Contains("PNG"))
                {
                    Image img = null;
                    if (present.Contains("PNG"))
                    {
                        var ms = obj.GetData("PNG") as MemoryStream;
                        if (ms != null) img = Image.FromStream(new MemoryStream(ms.ToArray()));
                    }
                    if (img == null) img = Clipboard.GetImage();
                    if (img != null)
                    {
                        using (img)
                        {
                            if ((long)img.Width * img.Height > MaxImagePixels) return null;
                            using (var ms = new MemoryStream())
                            {
                                img.Save(ms, ImageFormat.Png);
                                data.Add(new KeyValuePair<string, byte[]>(Fmt.Png, ms.ToArray()));
                            }
                            title = "[Image " + img.Width + "×" + img.Height + "]";
                        }
                        { status = CaptureStatus.Captured; return data; }
                    }
                }
            }
            catch { }

            return null;
        }

        static byte[] ReadRaw(object o)
        {
            var s = o as string;
            if (s != null) return Encoding.UTF8.GetBytes(s);
            var ms = o as MemoryStream;
            if (ms != null)
            {
                var b = ms.ToArray();
                int n = Array.IndexOf(b, (byte)0);
                if (n >= 0) Array.Resize(ref b, n);
                return b;
            }
            return null;
        }

        static KeyValuePair<string, byte[]> Kv(string name, string s)
        {
            return new KeyValuePair<string, byte[]>(name, Encoding.UTF8.GetBytes(s));
        }

        /// <summary>Puts a stored clip on the clipboard. Returns the clipboard sequence number after the write.</summary>
        public static uint Put(List<KeyValuePair<string, byte[]>> data)
        {
            var obj = new DataObject();
            Image img = null;
            foreach (var kv in data)
            {
                switch (kv.Key)
                {
                    case Fmt.Text:
                        obj.SetData(DataFormats.UnicodeText, Encoding.UTF8.GetString(kv.Value));
                        break;
                    case Fmt.Html:
                        obj.SetData(DataFormats.Html, new MemoryStream(kv.Value));
                        break;
                    case Fmt.Rtf:
                        obj.SetData(DataFormats.Rtf, Encoding.UTF8.GetString(kv.Value));
                        break;
                    case Fmt.Png:
                        img = Image.FromStream(new MemoryStream(kv.Value));
                        obj.SetData(DataFormats.Bitmap, true, img);
                        obj.SetData("PNG", false, new MemoryStream(kv.Value));
                        break;
                    case Fmt.Files:
                        var sc = new StringCollection();
                        sc.AddRange(Encoding.UTF8.GetString(kv.Value).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries));
                        obj.SetFileDropList(sc);
                        break;
                }
            }
            try { Clipboard.SetDataObject(obj, true, 10, 50); }
            catch { return 0; }
            return Native.GetClipboardSequenceNumber();
        }

        /// <summary>Activates the target window and sends the paste keystroke.</summary>
        public static void PasteInto(IntPtr target, string mode)
        {
            if (target == IntPtr.Zero || !Native.IsWindow(target)) return;
            Native.SetForegroundWindow(target);
            Thread.Sleep(60);

            // Release modifiers the user may still be holding (e.g. from the hotkey).
            foreach (var vk in new byte[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if ((Native.GetAsyncKeyState(vk) & 0x8000) != 0)
                    Native.keybd_event(vk, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);

            if (mode == "ShiftIns")
            {
                Native.keybd_event(0x10, 0, 0, UIntPtr.Zero);
                Native.keybd_event(0x2D, 0, 0, UIntPtr.Zero);
                Native.keybd_event(0x2D, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
                Native.keybd_event(0x10, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            else
            {
                Native.keybd_event(0x11, 0, 0, UIntPtr.Zero);
                Native.keybd_event(0x56, 0, 0, UIntPtr.Zero);
                Native.keybd_event(0x56, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
                Native.keybd_event(0x11, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }
    }
}
