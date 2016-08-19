using System;
using System.Windows.Forms;

namespace Busi
{
    static class Program
    {
        public static string Version = "0.0.1";
        
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MainFrm());
        }
    }
}
