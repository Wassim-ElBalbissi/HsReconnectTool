using System;
using System.Windows.Forms;
using UtilLib;

namespace HsReconnectTool
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Logger.Log("==== HsReconnectTool starting (elevated: {0}) ====", Util.IsElevated());

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Logger.LogException("UnhandledException", e.ExceptionObject as Exception);
            Application.ThreadException += (s, e) =>
                Logger.LogException("ThreadException", e.Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }
}
