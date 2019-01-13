using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConsoleTableExt;
using IsoOnTcp;
using log4net;
using NamedPipeWrapper;
using NetToPLCSimLite.Helpers;
using NetToPLCSimLite.Models;

namespace NetToPLCSimLite.Services
{
    public class S7PlcSimService : IDisposable
    {
        #region Fields
        private readonly ILog log = LogExt.log;
        private NamedPipeClient<Tuple<string, List<byte[]>>> pipeClient;
        private bool isBusy = false;

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
                var sb = new StringBuilder($"{Environment.NewLine}CURRENT [PLCSIM] LIST ( {PlcSimList.Count} ):{Environment.NewLine}");
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
            var ret = false;
            try
            {
                log.Info($"START, Get S7 Port.");
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

                ret = service.IsPortAvailable(S7Port);
            }
            catch (Exception)
            {
                ret = false;
                throw;
            }
            finally
            {
                if (ret) log.Info($"OK, Get S7 Port.");
                else log.Warn($"NG, Get S7 Port.");
            }
            return ret;
        }

        private void StartPipe()
        {
            var ret = false;
            try
            {
                log.Info("START, Start PipeClient.");
                if (string.IsNullOrEmpty(PipeName)) throw new ArgumentNullException(nameof(PipeName));
                pipeClient = new NamedPipeClient<Tuple<string, List<byte[]>>>(PipeName);
                pipeClient.ServerMessage += PipeClient_ServerMessage;
                pipeClient.Disconnected += PipeClient_Disconnected;
                pipeClient.Error += PipeClient_Error;
                pipeClient.Start();
                ret = true;
            }
            catch (Exception)
            {
                ret = false;
                throw;
            }
            finally
            {
                if (ret) log.Info("OK, Start PipeClient.");
                else log.Warn("NG, Start PipeClient.");
            }
        }

        private void StopPipe()
        {
            var ret = false;
            try
            {
                log.Info("START, Stop PipeClient.");
                if (pipeClient == null) return;
                pipeClient.ServerMessage -= PipeClient_ServerMessage;
                pipeClient.Disconnected -= PipeClient_Disconnected;
                pipeClient.Error -= PipeClient_Error;
                pipeClient.Stop();
            }
            catch (Exception)
            {
                ret = false;
                throw;
            }
            finally
            {
                if (ret) log.Info("OK, Stop PipeClient.");
                else log.Warn("NG, Stop PipeClient.");
            }
        }

        private void PipeClient_Disconnected(NamedPipeConnection<Tuple<string, List<byte[]>>, Tuple<string, List<byte[]>>> connection)
        {
            try
            {
                log.Warn("PIPE, PipeServer Disconnected.");
                if (string.IsNullOrEmpty(PipeServerName)) return;
                var procs = Process.GetProcessesByName(PipeServerName);
                if (procs != null)
                    foreach (var proc in procs)
                    {
                        proc.Kill();
                    }

                if (!string.IsNullOrEmpty(PipeServerPath) || File.Exists(PipeServerPath))
                {
                    log.Warn("PIPE, PipeServer Program restart.");
                    Process.Start(PipeServerPath);
                }
            }
            catch (Exception ex)
            {
                log.Error(nameof(PipeClient_Disconnected), ex);
            }
            finally
            {
                log.Warn("Exit Program due to PipeServer.");
                Environment.Exit(-1);
            }
        }

        private void PipeClient_Error(Exception exception)
        {
            log.Error(nameof(PipeClient_Error), exception);
        }

        private void PipeClient_ServerMessage(NamedPipeConnection<Tuple<string, List<byte[]>>, Tuple<string, List<byte[]>>> connection, Tuple<string, List<byte[]>> message)
        {
            log.Debug("PIPE, Received ServerMessage");
            msgQueue.Enqueue(message.Item2);
            if (isBusy) return;
            try
            {
                isBusy = true;
                List<byte[]> msg = null;
                while (!msgQueue.IsEmpty)
                {
                    // 가장 최근것만 처리
                    msgQueue.TryDequeue(out msg);
                }
                if (msg == null) return;

                log.Debug($"=== Received S7 PLCSim List ===");
                var adding = new List<S7Protocol>();
                var original = new List<S7Protocol>();
                foreach (var item in msg)
                {
                    var plc = item.ProtobufDeserialize<S7Protocol>();
                    original.Add(plc);

                    var exist = PlcSimList.FirstOrDefault(x => x.Ip == plc.Ip);
                    if (exist == null) adding.Add(plc);
                    else plc.IsStarted = exist.IsStarted;
                    log.Debug(plc.ToString());
                }
                log.Debug("==============================");

                log.Debug($"=== Current S7 PLCSim List ===");
                var removing = new List<S7Protocol>();
                foreach (var plc in PlcSimList)
                {
                    var exist = original.FirstOrDefault(x => x.Ip == plc.Ip);
                    if (exist == null) removing.Add(plc);
                    log.Debug(plc.ToString());
                }
                log.Debug("==============================");

                if (adding.Count > 0) AddStation(adding);
                if (removing.Count > 0) RemoveStation(removing);

                // Return
                var ret = original.Select(x => x.ToProtobuf()).ToList();
                if (ret == null) return;
                connection.PushMessage(new Tuple<string, List<byte[]>>(message.Item1 ?? string.Empty, ret));
            }
            catch (Exception ex)
            {
                log.Error(nameof(PipeClient_ServerMessage), ex);
            }
            finally
            {
                isBusy = false;
                log.Debug($"=== Running S7 PLCSim List ===");
                foreach (var item in PlcSimList)
                {
                    log.Debug(item.ToString());
                }
                log.Debug("==============================");
            }
        }

        private void AddStation(IEnumerable<S7Protocol> adding)
        {
            try
            {
                log.Info($"=== Adding S7 PLCSim List ===");
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
                                item.ErrorHandler = new Action<string>((ipp) => ErrorHandler(ipp));

                                log.Info($"OK, {item.ToString()}");
                            }
                            else
                            {
                                srv.stop();
                                item.IsStarted = false;
                                log.Warn($"NG, {item.ToString()}");
                            }
                        }
                        else
                            log.Warn($"NG({err}), {item.ToString()}");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"ERR, {item.ToString()}", ex);
                        srv?.stop();
                        item.Disconnect();
                        item.IsStarted = false;
                    }
                }
                log.Debug("==============================");
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
                log.Info($"=== Removing S7 PLCSim List ===");
                foreach (var item in removing)
                {
                    try
                    {
                        item.Disconnect();
                        item.IsStarted = false;

                        if (s7ServerList.TryRemove(item.Ip, out IsoToS7online srv))
                        {
                            srv?.Dispose();
                        }

                        var exist = PlcSimList.FirstOrDefault(x => x.Ip == item.Ip);
                        if (exist != null)
                        {
                            PlcSimList.Remove(exist);
                            log.Info($"OK, {item.ToString()}");
                        }
                        else
                            log.Warn($"NG, {item.ToString()}");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"ERR, {item.ToString()}", ex);
                    }
                }
                log.Debug("==============================");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void ErrorHandler(string ip)
        {
            // Return
            var error = PlcSimList.FirstOrDefault(x => !x.IsConnected && x.Ip == ip);
            if (error != null && s7ServerList.TryRemove(error.Ip, out IsoToS7online srv))
            {
                srv?.Dispose();

                error.IsStarted = false;
                log.Info($"STOPPED, {error.ToString()}");
            }

            var ret = PlcSimList.Select(x => x.ToProtobuf()).ToList();
            if (ret == null) return;
            pipeClient.PushMessage(new Tuple<string, List<byte[]>>(string.Empty, ret));
            PlcSimList.Remove(error);
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
