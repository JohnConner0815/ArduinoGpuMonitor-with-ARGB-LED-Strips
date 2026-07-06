using System;
using System.IO;
using System.Windows.Forms;

namespace ArduinoGpuMonitor
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                // Create a path for the log file in the same folder as the .exe
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");

                // Write the full error details to the text file
                File.WriteAllText(logPath, "Fatal Error on Startup:\n\n" + ex.ToString());

                // Let the user know it crashed and where to find the log
                MessageBox.Show("A fatal error occurred. The details have been saved to:\n" + logPath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}