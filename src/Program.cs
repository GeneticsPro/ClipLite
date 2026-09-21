using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ClipLite
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, @"Local\ClipLite.Instance", out created))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ClipLite.Show"))
            {
                if (!created)
                {
                    showEvent.Set();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Portable mode: a file named "portable" next to the exe keeps data beside it.
                var exeDir = Path.GetDirectoryName(Application.ExecutablePath);
                var dataDir = File.Exists(Path.Combine(exeDir, "portable"))
                    ? Path.Combine(exeDir, "data")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipLite");

                Directory.CreateDirectory(dataDir);
                var crashLog = Path.Combine(dataDir, "crash.log");
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    try { File.AppendAllText(crashLog, DateTime.Now + "  " + e.Exception + "\r\n\r\n"); } catch { }
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try { File.AppendAllText(crashLog, DateTime.Now + "  " + e.ExceptionObject + "\r\n\r\n"); } catch { }
                };

                bool startVisible =!args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
                var form = new MainForm(dataDir, startVisible);

                var listener = new Thread(() =>
                {
                    while (true)
                    {
                        showEvent.WaitOne();
                        try { form.BeginInvoke((Action)form.ShowFromOtherInstance); }
                        catch { return; }
                    }
                }) { IsBackground = true };
                listener.Start();

                Application.Run(form);
                GC.KeepAlive(mutex);
            }
        }
    }
}
