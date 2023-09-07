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
                int numDeviceColumn = 3;
                try
                {
                    numDeviceColumn = Int32.Parse(args[1]);
                } catch (Exception)
                {
                    Console.WriteLine("Not found num device argument.");
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DisplayForm(numDeviceColumn));
            }
        }
    }
}
