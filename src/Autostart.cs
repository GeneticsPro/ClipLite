using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClipLite
{
    /// <summary>Per-user "run at logon" entry. Starts ClipLite straight into the tray.</summary>
    internal static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "ClipLite";

        public static bool Enabled
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue(ValueName) != null;
                }
                catch { return false; }
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return;
                    if (value) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
