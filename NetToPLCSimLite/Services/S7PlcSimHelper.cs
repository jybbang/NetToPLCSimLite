using IsoOnTcp;
using log4net;
using NamedPipeWrapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetToPLCSimLite.Services
{
    public class S7PlcSimHelper
    {
        #region Fields
        private readonly ILog log;
        private readonly NamedPipeClient<List<S7PlcSim>> pipeClient = new NamedPipeClient<List<S7PlcSim>>(CONST.LOGGER_NAME);
        private readonly ConcurrentQueue<List<S7PlcSim>> msgQueue = new ConcurrentQueue<List<S7PlcSim>>();
        #endregion

        #region Properties
        public List<S7PlcSim> PlcSimList { get; } = new List<S7PlcSim>();
        public ConcurrentDictionary<string, IsoToS7online> S7ServerList { get; } = new ConcurrentDictionary<string, IsoToS7online>();
        #endregion

        #region Constructors
        public S7PlcSimHelper()
        {
            log = LogExt.log;
        }
        #endregion

        #region Public Methods
        public void StartListenPipe()
        {
            pipeClient.ServerMessage += PipeClient_ServerMessage;
            pipeClient.Disconnected += PipeClient_Disconnected;
            pipeClient.Error += PipeClient_Error;
            pipeClient.Start();
            log.Info("START, PipeClient Listenning.");
        }

        public void StopListenPipe()
        {
            pipeClient.ServerMessage -= PipeClient_ServerMessage;
            pipeClient.Disconnected -= PipeClient_Disconnected;
            pipeClient.Error -= PipeClient_Error;
            pipeClient.Stop();
            log.Info("STOP, PipeClient Listenning.");
        }
        #endregion

        #region Private Methods
        private void PipeClient_Disconnected(NamedPipeConnection<List<S7PlcSim>, List<S7PlcSim>> connection)
        {
            log.Warn("PipeClient Disconnected.");
        }

        private void PipeClient_Error(Exception exception)
        {
            log.Error("PipeClient", exception);
        }

        private void PipeClient_ServerMessage(NamedPipeConnection<List<S7PlcSim>, List<S7PlcSim>> connection, List<S7PlcSim> message)
        {
            msgQueue.Enqueue(message);
            List<S7PlcSim> msg = null;
            while (!msgQueue.IsEmpty)
            {
                msgQueue.TryDequeue(out msg);
            }

            var adding = new List<S7PlcSim>();
            var removing = new List<S7PlcSim>();

            log.Info("RECEIVED, PipeClient ServerMessage");
            log.Debug("=== Received S7 PLCSim List ===");
            foreach (var item in msg)
            {
                var exist = PlcSimList.Exists(x => x.PlcIp == item.PlcIp);
                if (!exist) adding.Add(item);
                log.Debug($"Name:{item.Name}, IP:{item.PlcIp}");
            }

            log.Debug("=== Current S7 PLCSim List ===");
            foreach (var item in PlcSimList)
            {
                var exist = msg.Exists(x => x.PlcIp == item.PlcIp);
                if (!exist) removing.Add(item);
                log.Debug($"Name:{item.Name}, IP:{item.PlcIp}");
            }

            if (adding.Count > 0) AddStation(adding);
            if (removing.Count > 0) RemoveStation(removing);

            // Return
            connection.PushMessage(PlcSimList);

            log.Debug("=== Running S7 PLCSim List ===");
            foreach (var item in PlcSimList)
            {
                log.Debug($"Name:{item.Name}, IP:{item.PlcIp}, Connected:{item.IsConnected}, Started:{item.IsStarted}");
            }
        }

        private void AddStation(List<S7PlcSim> adding)
        {
            log.Info("=== Adding S7 PLCSim List ===");
            foreach (var item in adding)
            {
                var tsaps = new List<byte[]>();
                byte tsap2 = (byte)(item.Rack << 4 | item.Slot);
                tsaps.Add(new byte[] { 0x01, tsap2 });
                tsaps.Add(new byte[] { 0x02, tsap2 });
                tsaps.Add(new byte[] { 0x03, tsap2 });

                try
                {
                    var srv = new IsoToS7online(false);
                    var ip = IPAddress.Parse(item.PlcIp);
                    var err = string.Empty;
                    var ret = srv.start(item.Name, ip, tsaps, ip, item.Rack, item.Slot, ref err);
                    if (ret)
                    {
                        item.IsStarted = true;
                        var conn = item.Connect();
                        if (conn)
                        {
                            srv.DataReceived = item.DataReceived;
                            S7ServerList.TryAdd(item.PlcIp, srv);

                            PlcSimList.Add(item);
                            log.Info($"OK, Name:{item.Name}, IP:{item.PlcIp}");
                        }
                        else
                        {
                            srv.stop();
                            item.IsStarted = false;
                        }
                    }
                    else
                        log.Warn($"NG({err}), Name:{item.Name}, IP:{item.PlcIp}");
                }
                catch (Exception ex)
                {
                    log.Error($"ERR, Name:{item.Name}, IP:{item.PlcIp}, {ex.Message}");
                }
            }
        }

        private void RemoveStation(List<S7PlcSim> removing)
        {
            log.Info("=== Removing S7 PLCSim List ===");
            foreach (var item in removing)
            {
                try
                {
                    S7ServerList.TryRemove(item.PlcIp, out IsoToS7online srv);
                    srv?.stop();
                    item.IsStarted = false;
                    item.Disconnect();
                }
                catch
                {
                }
                finally
                {
                    var exist = PlcSimList.FirstOrDefault(x => x.PlcIp == item.PlcIp);
                    if (exist != null)
                    {
                        PlcSimList.Remove(exist);
                        log.Info($"OK, Name:{item.Name}, IP:{item.PlcIp}");
                    }
                    else
                        log.Warn($"NG, Name:{item.Name}, IP:{item.PlcIp}");
                }
            }
        }
        #endregion
    }
}
