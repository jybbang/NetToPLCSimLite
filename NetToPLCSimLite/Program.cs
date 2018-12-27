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

            // Start Program
            var cmdOpt = new CmdLineOptions();
            cmdOpt.ArgumentPrefix = "-";
            BaCon.Start<Setup>(new BaConStartOptions() { CmdLineOptions = cmdOpt });
        }
    }
}
