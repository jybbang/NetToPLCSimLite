using ConsoleTableExt;
using IsoOnTcp;
using log4net;
using NamedPipeWrapper;
using NetToPLCSimLite.Helpers;
using NetToPLCSimLite.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetToPLCSimLite.Services
{
    public class S7PlcSimService : IDisposable
    {
        #region Fields
        private readonly ILog log = LogExt.log;
        private NamedPipeClient<List<byte[]>> pipeClient;

        private readonly List<S7Protocol> PlcSimList = new List<S7Protocol>();
        private readonly ConcurrentQueue<List<byte[]>> msgQueue = new ConcurrentQueue<List<byte[]>>();
        private readonly ConcurrentDictionary<string, IsoToS7online> s7ServerList = new ConcurrentDictionary<string, IsoToS7online>();
        #endregion

        #region Properties
        public int S7Port { get; set; } = CONST.S7_PORT;
        public int SvcTimeout { get; set; } = CONST.SVC_TIMEOUT;
        public string PipeName { get; set; }
        public string PipeServerName { get; set; }
        public string PipeServerPath { get; set; }
        #endregion

        #region Public Methods
        public void Run()
        {
            try
            {
                log.Info("START, Running NetToPLCSimLite.");
                if (!GetS7Port()) throw new InvalidOperationException();
                StartPipe();
                log.Info("FINISH, Running NetToPLCSimLite.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public override string ToString()
        {
            try
            {
                var sb = new StringBuilder($"{Environment.NewLine}CURRENT [PLCSIM] LIST ( {PlcSimList.Count} ):");
                if (PlcSimList.Count > 0)
                {
                    sb.Append(ConsoleTableBuilder.From(PlcSimList).WithFormat(ConsoleTableBuilderFormat.MarkDown).Export().ToString());
                }
                return sb.AppendLine().ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Private Methods
        private bool GetS7Port()
        {
            try
            {
                var service = new S7ServiceHelper();
                var s7svc = service.FindS7Service();
                if (s7svc == null) throw new NullReferenceException();

                var before = service.IsPortAvailable(S7Port);
                if (!before)
                {
                    service.StopService(s7svc, SvcTimeout);
                    Thread.Sleep(100);

                    service.StartTcpServer(S7Port);
                    Thread.Sleep(100);

                    if (!service.StartService(s7svc, SvcTimeout)) return false;
                    Thread.Sleep(100);

                    service.StopTcpServer();
                    Thread.Sleep(100);
                }

                return service.IsPortAvailable(S7Port);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void StartPipe()
        {
            try
            {
                if (string.IsNullOrEmpty(PipeName)) throw new ArgumentNullException(nameof(PipeName));
                pipeClient = new NamedPipeClient<List<byte[]>>(PipeName);
                pipeClient.ServerMessage += PipeClient_ServerMessage;
                pipeClient.Disconnected += PipeClient_Disconnected;
                pipeClient.Error += PipeClient_Error;
                pipeClient.Start();
                log.Info("OK, Start PipeClient Listenning.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void StopPipe()
        {
            try
            {
                pipeClient.ServerMessage -= PipeClient_ServerMessage;
                pipeClient.Disconnected -= PipeClient_Disconnected;
                pipeClient.Error -= PipeClient_Error;
                pipeClient.Stop();
                log.Info("OK, Stop PipeClient Listenning.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void PipeClient_Disconnected(NamedPipeConnection<List<byte[]>, List<byte[]>> connection)
        {

            try
            {
                log.Warn("PIPE, Disconnected PipeServer.");
                if (string.IsNullOrEmpty(PipeServerName)) return;
                var procs = Process.GetProcessesByName(PipeServerName);
                if (procs != null)
                    foreach (var proc in procs)
                    {
                        proc.ProcessKill();
                    }

                if (!string.IsNullOrEmpty(PipeServerPath))
                {
                    log.Warn("PIPE, Restart PipeServer.");
                    Process.Start(PipeServerPath);
                }
            }
            catch (Exception ex)
            {
                log.Error(nameof(PipeClient_Disconnected), ex);
            }
            finally
            {
                log.Warn("RESTART PROGRAM.");
                Environment.Exit(-1);
            }
        }

        private void PipeClient_Error(Exception exception)
        {
            log.Error(nameof(PipeClient_Error), exception);
        }

        private void PipeClient_ServerMessage(NamedPipeConnection<List<byte[]>, List<byte[]>> connection, List<byte[]> message)
        {
            try
            {
                log.Debug("RECEIVED, PipeClient ServerMessage");
                msgQueue.Enqueue(message);
                List<byte[]> msg = null;
                while (!msgQueue.IsEmpty)
                {
                    msgQueue.TryDequeue(out msg);
                }
                if (msg == null) return;

                log.Debug("=== Received S7 PLCSim List ===");
                var adding = new List<S7Protocol>();
                var original = new List<S7Protocol>();
                foreach (var item in msg)
                {
                    var plc = item.ProtobufDeserialize<S7Protocol>();
                    original.Add(plc);

                    var exist = PlcSimList.FirstOrDefault(x => x.Ip == plc.Ip);
                    if (exist == null) adding.Add(plc);
                    else plc.IsStarted = exist.IsStarted;
                    log.Debug($"Name:{plc.Name}, IP:{plc.Ip}");
                }

                log.Debug("=== Current S7 PLCSim List ===");
                var removing = new List<S7Protocol>();
                foreach (var plc in PlcSimList)
                {
                    var exist = original.FirstOrDefault(x => x.Ip == plc.Ip);
                    if (exist == null) removing.Add(plc);
                    log.Debug($"Name:{plc.Name}, IP:{plc.Ip}");
                }

                if (adding.Count > 0) AddStation(adding);
                if (removing.Count > 0) RemoveStation(removing);

                // Return
                var ret = original.Select(x => x.ToProtobuf()).ToList();
                if (ret == null) return;
                connection.PushMessage(ret);
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
                    log.Debug($"Name:{item.Name}, IP:{item.Ip}, Connected:{item.IsConnected}, Started:{item.IsStarted}");
                }
            }
        }

        private void AddStation(IEnumerable<S7Protocol> adding)
        {
            try
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
                        var ip = IPAddress.Parse(item.Ip);
                        var err = string.Empty;
                        var ret = srv.start(item.Name, ip, tsaps, ip, item.Rack, item.Slot, ref err);
                        if (ret)
                        {
                            var conn = item.Connect();
                            if (conn)
                            {
                                srv.DataReceived = item.DataReceived;
                                s7ServerList.TryAdd(item.Ip, srv);

                                PlcSimList.Add(item);
                                item.IsStarted = true;
                                log.Info($"OK, Name:{item.Name}, IP:{item.Ip}");
                            }
                            else
                            {
                                srv.stop();
                                item.IsStarted = false;
                                log.Warn($"NG, Name:{item.Name}, IP:{item.Ip}");
                            }
                        }
                        else
                            log.Warn($"NG({err}), Can not start NetToPLCSimLite Server, Name:{item.Name}, IP:{item.Ip}");
                    }
                    catch (Exception ex)
                    {
                        srv?.stop();
                        item.Disconnect();
                        item.IsStarted = false;
                        log.Error($"ERR, Name:{item.Name}, IP:{item.Ip}, {ex.Message}");
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void RemoveStation(IEnumerable<S7Protocol> removing)
        {
            try
            {
                log.Info("=== Removing S7 PLCSim List ===");
                foreach (var item in removing)
                {
                    try
                    {
                        s7ServerList.TryRemove(item.Ip, out IsoToS7online srv);
                        srv?.stop();
                        item.Disconnect();
                        item.IsStarted = false;

                        var exist = PlcSimList.FirstOrDefault(x => x.Ip == item.Ip);
                        if (exist != null)
                        {
                            PlcSimList.Remove(exist);
                            log.Info($"OK, Name:{item.Name}, IP:{item.Ip}");
                        }
                        else
                            log.Warn($"NG, Name:{item.Name}, IP:{item.Ip}");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"ERR, Name:{item.Name}, IP:{item.Ip}, {ex.Message}");
                    }
                }
            }
            catch (Exception)
            {
                throw;
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
                    RemoveStation(PlcSimList);
                    StopPipe();
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
