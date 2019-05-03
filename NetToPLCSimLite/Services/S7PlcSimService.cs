using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConsoleTableExt;
using DynamicData;
using IsoOnTcp;
using NetToPLCSimLite.Helpers;
using NetToPLCSimLite.Models;
using Serilog;

namespace NetToPLCSimLite.Services
{
    public class S7PlcSimService : IDisposable
    {
        #region Constants
        public const int S7_PORT = 102;
        public const int SVC_TIMEOUT = 20000;
        #endregion

        #region Fields
        public event EventHandler<string> PlcSimErr;
        private readonly ConcurrentDictionary<string, IsoToS7online> s7ServerList = new ConcurrentDictionary<string, IsoToS7online>();
        #endregion
        
        #region Fields
        private readonly ISourceCache<S7Protocol, string> plcsimSource = new SourceCache<S7Protocol, string>(x => x.Ip);
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        #endregion

        #region Constructors
        public S7PlcSimService()
        {
            // 로거등록
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console();

            disposables.Add(plcsimSource.Connect()
                .OnItemAdded(item =>
                {
                    IsoToS7online srv = null;
                    try
                    {
                        var tsaps = new List<byte[]>();
                        byte tsap2 = (byte)(item.Rack << 4 | item.Slot);
                        tsaps.Add(new byte[] { 0x01, tsap2 });
                        tsaps.Add(new byte[] { 0x02, tsap2 });
                        tsaps.Add(new byte[] { 0x03, tsap2 });

                        srv = new IsoToS7online(false);
                        var ip = IPAddress.Parse(item.Ip);
                        var err = string.Empty;
                        var srvStart = srv.start(item.Name, ip, tsaps, ip, item.Rack, item.Slot, ref err);
                        if (srvStart)
                        {
                            var conn = item.Connect();
                            if (conn)
                            {
                                srv.DataReceived = item.DataReceived;
                                s7ServerList.TryAdd(item.Ip, srv);
                                item.ErrorHandler = new Action<string>((ipp) => ErrorHandler(ipp));
                                Log.Information($"OK, {item.ToString()}");
                            }
                            else
                            {
                                srv?.Dispose();
                                srv = null;
                                plcsimSource.Remove(item);
                                Log.Warning($"NG, {item.ToString()}");
                            }
                        }
                        else
                            Log.Warning($"NG({err}), {item.ToString()}");
                    }
                    catch (Exception ex)
                    {
                        srv?.Dispose();
                        srv = null;
                        plcsimSource.Remove(item);
                        Log.Error(ex, $"ERR, {item.ToString()}");
                    }
                })
                .OnItemRemoved(item =>
                {
                    try
                    {
                        if (s7ServerList.TryRemove(item.Ip, out IsoToS7online srv))
                        {
                            srv?.Dispose();
                            srv = null;
                        }
                        item?.Dispose();
                        Log.Information($"OK, {item.ToString()}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"ERR, {item.ToString()}", ex);
                    }
                })
                .DisposeMany()
                .Subscribe());

            Log.Debug($"{nameof(S7PlcSimService)} - Created.");
        }
        #endregion

        #region Public Methods
        public bool GetS7Port()
        {
            var ret = false;
            try
            {
                Log.Information($"{nameof(S7PlcSimService)}.{nameof(GetS7Port)} - Get S7 Port.");
                var service = new S7ServiceHelper();
                var s7svc = service.FindS7Service();
                if (s7svc == null) throw new NullReferenceException();

                ret = service.IsPortAvailable(S7_PORT);
                if (!ret)
                {
                    service.StopService(s7svc, SVC_TIMEOUT);
                    Thread.Sleep(100);

                    service.StartTcpServer(S7_PORT);
                    Thread.Sleep(100);

                    if (!service.StartService(s7svc, SVC_TIMEOUT))
                    {
                        service.StopTcpServer();
                        return false;
                    }
                    Thread.Sleep(100);

                    service.StopTcpServer();
                    Thread.Sleep(100);

                    ret = service.IsPortAvailable(S7_PORT);
                }
            }
            catch (Exception)
            {
                ret = false;
                throw;
            }
            finally
            {
                if (ret)
                    Log.Information($"{nameof(S7PlcSimService)}.{nameof(GetS7Port)} - OK, Get S7 Port.");
                else
                    Log.Warning($"{nameof(S7PlcSimService)}.{nameof(GetS7Port)} - NG, Get S7 Port.");
            }
            return ret;
        }

        public IEnumerable<S7Protocol> SetStation(IEnumerable<S7Protocol> list)
        {
            try
            {
                var removing = plcsimSource.Items.Except(list).ToList();
                var adding = list.Except(plcsimSource.Items).ToList();

                plcsimSource.Edit(u =>
                {
                    u.AddOrUpdate(adding);
                    u.Remove(removing);
                });
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Log.Debug($"=== After S7 PLCSim List ===");
                foreach (var item in plcsimSource.Items)
                {
                    Log.Debug(item.ToString());
                }
                Log.Debug("==============================");
            }

            return plcsimSource.Items.Where(x => x.IsConnected);
        }

        public override string ToString()
        {
            try
            {
                return plcsimSource.Count > 0 ?
                ConsoleTableBuilder
                    .From(plcsimSource.Items.ToList())
                    .WithFormat(ConsoleTableBuilderFormat.Minimal)
                    .Export()
                    .ToString()
                : "No More Plc Sim.";
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Private Methods
        private void ErrorHandler(string ip)
        {
            // Return
            if (string.IsNullOrEmpty(ip)) return;
            plcsimSource.RemoveKey(ip);
            Log.Warning($"{nameof(S7PlcSimService)}.{nameof(ErrorHandler)} - {ip}");
            PlcSimErr?.Invoke(this, ip);
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
                    Log.Debug($"{nameof(S7PlcSimService)} - Disposing.");
                    plcsimSource.Clear();
                    disposables?.Dispose();
                    Log.Debug($"{nameof(S7PlcSimService)} - Disposed.");
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
