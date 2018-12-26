using BizArk.ConsoleApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetToPLCSimLite
{
    class Program
    {
        static void Main(string[] args)
        {
            // Set ThreadPool
            ThreadPool.SetMinThreads(200, 200);

            // For Disposing
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                exitEvent.Set();
            };

            // Start Program
            var cmdOpt = new CmdLineOptions();
            cmdOpt.ArgumentPrefix = "-";
            BaCon.Start<Setup>(new BaConStartOptions() { CmdLineOptions = cmdOpt });

            LogExt.log.Debug("Press 'Ctrl + D' to swtich log level.");
            LogExt.log.Debug("Press 'Ctrl + C' to exit program.");
            exitEvent.WaitOne();
        }
    }
}
