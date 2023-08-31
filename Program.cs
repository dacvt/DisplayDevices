using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DisplayDevices
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 0)
            {
                Console.WriteLine("Can not pass 0 arguments");
            }
            else
            {
                int numDeviceColumn = Int32.Parse("3");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DisplayForm(numDeviceColumn));
            }
        }
    }
}
