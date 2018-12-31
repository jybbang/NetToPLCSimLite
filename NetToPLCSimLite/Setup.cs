using BizArk.ConsoleApp;
using log4net;
using NetToPLCSimLite.Helpers;
using NetToPLCSimLite.Services;
using SharpConfig;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace NetToPLCSimLite
{
    public class Setup : BaseConsoleApp
    {
        #region Fields
        private readonly ILog log = LogExt.log;
        private readonly S7PlcSimService s7Plcsim;
        #endregion

        #region Constructors
        public Setup()
        {
            var config = Configuration.LoadFromFile($@"{AppDomain.CurrentDomain.BaseDirectory}\{CONST.CONFIG_FILE}");
            var netCfg = config["NET"];
            var pipeCfg = config["PIPE"];

            s7Plcsim = new S7PlcSimService
            {
                PipeName = pipeCfg["NAME"].StringValue,
                PipeServerName = pipeCfg["PROC_NAME"].StringValue,
                PipeServerPath = pipeCfg["PATH"].StringValue,
                SvcTimeout = netCfg["SVC_TIMEOUT"].IntValue,
            };
        }
        #endregion

        #region Public Methods
        public override int Start()
        {
            log.Info("START PROGRAM.");
            try
            {
                s7Plcsim.Run();

                #region USER
                Task.Run(() =>
                {
                    while (true)
                    {
                        var keys = System.Console.ReadKey(true);
                        if (keys.Modifiers == ConsoleModifiers.Control && keys.Key == ConsoleKey.F1)
                            LogExt.SwitchLevel();
                        else if (keys.Modifiers == ConsoleModifiers.Control && keys.Key == ConsoleKey.F2)
                            System.Console.WriteLine(s7Plcsim.ToString());
                    }
                });

                var exitEvent = new ManualResetEvent(false);
                System.Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    exitEvent.Set();
                };

                LogExt.log.Debug("Press 'Ctrl + F1' to Swtich log level.");
                LogExt.log.Debug("Press 'Ctrl + F2' to Show list.");
                LogExt.log.Debug("Press 'Ctrl + C' to Exit program.");

                exitEvent.WaitOne();
                #endregion
            }
            catch (Exception ex)
            {
                log.Error(nameof(NetToPLCSimLite), ex);
            }
            finally
            {
                s7Plcsim?.Dispose();
                log.Info("EXIT PROGRAM.");
            }

            return 0;
        }
        #endregion
    }
}