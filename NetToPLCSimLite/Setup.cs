using BizArk.ConsoleApp;
using log4net;
using NetToPLCSimLite.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace NetToPLCSimLite
{
    public class Setup : BaseConsoleApp
    {
        #region Fields
        private readonly ILog log;
        private readonly S7ServiceHelper s7Service = new S7ServiceHelper();
        private readonly S7PlcSimHelper s7Plcsim = new S7PlcSimHelper();
        #endregion

        #region Constructors
        public Setup()
        {
            log = LogExt.log;
        }
        #endregion

        #region Public Methods
        public override int Start()
        {
            try
            {
                log.Info("Started.");

                s7Service.GetS7OnlinePort();
                s7Plcsim.StartListenPipe();

                Task.Run(() =>
                {
                    while (true)
                    {
                        var keys = Console.ReadKey(true);
                        if (keys.Modifiers == ConsoleModifiers.Control && keys.Key == ConsoleKey.D)
                            LogExt.SwitchLevel();
                    }
                });
            }
            catch
            {
                Environment.Exit(-1);
            }

            return 0;
        }
        #endregion
    }
}