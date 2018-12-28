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
            };

            S7ServiceHelper.Timeout = netCfg["SVC_TIMEOUT"].IntValue;
        }
        #endregion

        #region Public Methods
        public override int Start()
        {
            log.Info("Start Program.");
            try
            {
                log.Info("RUN, Get S7 online port.");
                var s7svc = S7ServiceHelper.FindS7Service();
                if (s7svc == null) throw new NullReferenceException();

                var before = S7ServiceHelper.IsS7PortAvailable();
                if (!before)
                {
                    S7ServiceHelper.StopS7Service(s7svc);
                    Thread.Sleep(50);

                    S7ServiceHelper.StartTcpServer();
                    Thread.Sleep(50);

                    if(!S7ServiceHelper.StartS7Service(s7svc)) throw new InvalidOperationException("FAIL, You must run S7 online service.");
                    Thread.Sleep(50);

                    S7ServiceHelper.StopTcpServer();
                    Thread.Sleep(50);

                    if (S7ServiceHelper.IsS7PortAvailable()) log.Info("FINISH, Get S7 online port.");
                    else throw new InvalidOperationException("FAIL, Can not get S7 online port.");
                }
                else log.Info("FINISH, Get S7 online port.");

                s7Plcsim.StartListenPipe();

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
                log.Info("Exit Program.");
            }

            return 0;
        }
        #endregion
    }
}