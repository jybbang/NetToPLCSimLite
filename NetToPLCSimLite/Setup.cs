using BizArk.ConsoleApp;
using log4net;
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
        private readonly ILog log;
        private S7PlcSimService s7Plcsim;
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
            log.Info("Start Program.");
            try
            {
                var config = Configuration.LoadFromFile($@"{AppDomain.CurrentDomain.BaseDirectory}\{CONST.CONFIG_FILE}");
                var netCfg = config["NET"];
                var pipeCfg = config["PIPE"];

                log.Info("RUN, Get S7 online port.");
                var timeout = netCfg["TIMEOUT"].IntValue;
                var s7svcHelper = new Helpers.S7ServiceHelper();
                var s7svc = s7svcHelper.FindS7Service();
                var before = s7svcHelper.IsTcpPortAvailable(CONST.S7_PORT);
                if (!before)
                {
                    if (s7svcHelper.StopS7Service(s7svc, timeout)) log.Info("OK, Stop S7 online service.");
                    else throw new InvalidOperationException("NG, Can not stop S7 online service.");
                    Thread.Sleep(50);

                    if (s7svcHelper.StartTcpServer()) log.Info("OK, Start temporary TCP server.");
                    else throw new InvalidOperationException("NG, Can not start TCP server.");
                    Thread.Sleep(50);

                    if (s7svcHelper.StartS7Service(s7svc, timeout)) log.Info("OK, Start S7 online service.");
                    else throw new InvalidOperationException("NG, Can not start S7 online service.");
                    Thread.Sleep(50);

                    if (s7svcHelper.StopTcpServer()) log.Info("OK, Stop temporary TCP server.");
                    else throw new InvalidOperationException("NG, Can not stop TCP server.");
                    Thread.Sleep(50);

                    var after = s7svcHelper.IsTcpPortAvailable(CONST.S7_PORT);
                    if (after) log.Info("COMPLETE, Get S7 online port.");
                    else throw new InvalidOperationException("FAIL, Can not get S7 online port.");
                }
                else log.Info("COMPLETE, Aleady get S7 online port.");

                s7Plcsim = new S7PlcSimService
                {
                    PipeName = pipeCfg["NAME"].StringValue,
                    PipeServerName = pipeCfg["PROC_NAME"].StringValue,
                    PipeServerPath = pipeCfg["PATH"].StringValue,
                };
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