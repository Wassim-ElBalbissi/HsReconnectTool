using System;
using System.IO;

namespace UtilLib
{
    // Simple file logger. The app is a WinExe with no console, so Console.WriteLine
    // output is invisible; this writes timestamped lines to a log file next to the
    // user's local app data so failures can actually be diagnosed.
    public static class Logger
    {
        static readonly object sync = new object();
        static readonly string LogPath = BuildLogPath();

        public static string FilePath
        {
            get { return LogPath; }
        }

        static string BuildLogPath()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HsReconnectTool");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "log.txt");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "HsReconnectTool.log");
            }
        }

        public static void Log(string message)
        {
            try
            {
                lock (sync)
                {
                    File.AppendAllText(LogPath,
                        string.Format("{0:yyyy-MM-dd HH:mm:ss.fff}  {1}{2}",
                            DateTime.Now, message, Environment.NewLine));
                }
            }
            catch
            {
                // Never let logging crash the app.
            }
        }

        public static void Log(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }

        public static void LogException(string context, Exception ex)
        {
            Log("ERROR in {0}: {1}", context, ex);
        }
    }
}
