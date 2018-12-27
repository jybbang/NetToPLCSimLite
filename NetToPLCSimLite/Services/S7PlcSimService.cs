﻿using ConsoleTableExt;
using IsoOnTcp;
using log4net;
using NamedPipeWrapper;
using NetToPLCSimLite.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetToPLCSimLite.Services
{
    public class S7PlcSimService : IDisposable
    {
        #region Fields
        private readonly ILog log;
        private readonly NamedPipeClient<List<S7PlcSim>> pipeClient = new NamedPipeClient<List<S7PlcSim>>(CONST.LOGGER_NAME);
        private readonly ConcurrentQueue<List<S7PlcSim>> msgQueue = new ConcurrentQueue<List<S7PlcSim>>();
        private readonly ConcurrentDictionary<string, IsoToS7online> s7ServerList = new ConcurrentDictionary<string, IsoToS7online>();
        #endregion

        #region Properties
        public List<S7PlcSim> PlcSimList { get; } = new List<S7PlcSim>();
        #endregion

        #region Constructors
        public S7PlcSimService()
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

        public override string ToString()
        {
            var sb = new StringBuilder($"{Environment.NewLine}CURRENT [PLCSIM] LIST ( {PlcSimList.Count} ):");
            if (PlcSimList.Count > 0)
            {
                sb.Append(ConsoleTableBuilder.From(PlcSimList).WithFormat(ConsoleTableBuilderFormat.MarkDown).Export().ToString());
            }
            return sb.AppendLine().ToString();
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
            try
            {
                log.Info("RECEIVED, PipeClient ServerMessage");
                msgQueue.Enqueue(message);
                List<S7PlcSim> msg = null;
                while (!msgQueue.IsEmpty)
                {
                    msgQueue.TryDequeue(out msg);
                }

                var adding = new List<S7PlcSim>();
                var removing = new List<S7PlcSim>();

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
            }
            catch (Exception ex)
            {
                log.Error(nameof(PipeClient_ServerMessage), ex);
            }
            finally
            {
                log.Debug("=== Running S7 PLCSim List ===");
                foreach (var item in PlcSimList)
                {
                    log.Debug($"Name:{item.Name}, IP:{item.PlcIp}, Connected:{item.IsConnected}, Started:{item.IsStarted}");
                }
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

                IsoToS7online srv = null;
                try
                {
                    srv = new IsoToS7online(false);
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
                            s7ServerList.TryAdd(item.PlcIp, srv);

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
                    srv?.stop();
                    item.IsStarted = false;
                    item.Disconnect();
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
                    s7ServerList.TryRemove(item.PlcIp, out IsoToS7online srv);
                    srv?.stop();
                    item.IsStarted = false;
                    item.Disconnect();

                    var exist = PlcSimList.FirstOrDefault(x => x.PlcIp == item.PlcIp);
                    if (exist != null)
                    {
                        PlcSimList.Remove(exist);
                        log.Info($"OK, Name:{item.Name}, IP:{item.PlcIp}");
                    }
                    else
                        log.Warn($"NG, Name:{item.Name}, IP:{item.PlcIp}");
                }
                catch (Exception ex)
                {
                    log.Error($"ERR, Name:{item.Name}, IP:{item.PlcIp}, {ex.Message}");
                }
            }
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // 중복 호출을 검색하려면

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 관리되는 상태(관리되는 개체)를 삭제합니다.
                    StopListenPipe();
                    RemoveStation(PlcSimList);
                }

                // TODO: 관리되지 않는 리소스(관리되지 않는 개체)를 해제하고 아래의 종료자를 재정의합니다.
                // TODO: 큰 필드를 null로 설정합니다.

                disposedValue = true;
            }
        }

        // TODO: 위의 Dispose(bool disposing)에 관리되지 않는 리소스를 해제하는 코드가 포함되어 있는 경우에만 종료자를 재정의합니다.
        // ~S7PlcSimService() {
        //   // 이 코드를 변경하지 마세요. 위의 Dispose(bool disposing)에 정리 코드를 입력하세요.
        //   Dispose(false);
        // }

        // 삭제 가능한 패턴을 올바르게 구현하기 위해 추가된 코드입니다.
        public void Dispose()
        {
            // 이 코드를 변경하지 마세요. 위의 Dispose(bool disposing)에 정리 코드를 입력하세요.
            Dispose(true);
            // TODO: 위의 종료자가 재정의된 경우 다음 코드 줄의 주석 처리를 제거합니다.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
