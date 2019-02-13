using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using log4net;
using S7PROSIMLib;

namespace NetToPLCSimLite.Models
{
    public class S7Protocol : IDisposable
    {
        #region Const
        private const byte S7COMM_TRANSPORT_SIZE_BIT = 1;
        private const byte S7COMM_TRANSPORT_SIZE_BYTE = 2;
        #endregion

        #region Fields
        public Action<string> ErrorHandler;

        private readonly ILog log = LogExt.log;
        private S7ProSim plcsim = new S7ProSim();
        private readonly Timer timer = new Timer();
        #endregion

        #region Properties
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public bool IsConnected { get; set; } = false;
        public StationCpu Cpu { get; set; } = StationCpu.S400;
        public int Rack { get; set; } = 0;
        public int Slot { get; set; } = 3;
        public string PlcPath { get; set; } = string.Empty;
        public int Instance { get; set; } = -1;
        #endregion

        #region Enums
        public enum StationCpu
        {
            S200 = 0,
            S300 = 10,
            S400 = 20,
            S1200 = 30,
            S1300 = 40,
        }
        #endregion

        #region Public Methods
        public override string ToString()
        {
            return $"Name:{Name}, IP:{Ip}, Connected:{IsConnected}, Instance:{Instance}";
        }

        public bool Connect()
        {
            try
            {
                if (Instance < 1 || Instance > 8) return false;
                Disconnect();

                plcsim.ConnectionError -= Plcsim_ConnectionError;
                plcsim.ConnectionError += Plcsim_ConnectionError;
                plcsim.ConnectExt(Instance);
                plcsim.SetState("RUN_P");
                var st = plcsim.GetState();
                IsConnected = st == "RUN_P" ? true : false;

                if (IsConnected)
                {
                    plcsim.SetScanMode(ScanModeConstants.ContinuousScan);
                    timer.Elapsed -= Timer_Elapsed;
                    timer.Elapsed += Timer_Elapsed;
                    timer.Interval = 1000;
                    timer.Start();
                }
            }
            catch (Exception ex)
            {
                log.Error($"ERR, Name:{Name}, IP:{Ip}, INS:{PlcPath}", ex);
                Disconnect();
            }

            return IsConnected;
        }

        public void Disconnect()
        {
            try
            {
                timer.Elapsed -= Timer_Elapsed;
                timer.Stop();

                if (IsConnected)
                {
                    plcsim.ConnectionError -= Plcsim_ConnectionError;
                    plcsim.Disconnect();
                    log.Info($"DISCONNECTED, Name:{Name}, IP:{Ip}, INS:{Instance}");
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                IsConnected = false;
            }
        }

        public void DataReceived(byte[] data)
        {
            if (!IsConnected || data == null) return;
            try
            {
                // INPUT, WRITE
                var lenth = data.Length - 28;
                if (lenth > 0 && data[0] == 0x32 && data[1] == 0x01 && data[10] == 0x05 && data[14] == 0x10 && data[20] == 0x81)
                {
                    // address
                    byte[] aa = new byte[4] { 0, data[21], data[22], data[23] };
                    var addr = BitConverter.ToInt32(aa, 0);
                    addr = BitConverter.IsLittleEndian ? IPAddress.HostToNetworkOrder(addr) : addr;
                    var bytepos = addr / 8;
                    var bitpos = addr % 8;

                    var t_size = data[15];
                    var len = BitConverter.ToInt16(data, 16);
                    len = BitConverter.IsLittleEndian ? IPAddress.HostToNetworkOrder(len) : len;

                    if (t_size == S7COMM_TRANSPORT_SIZE_BIT)
                    {
                        var set = data[28] == 0 ? false : true;
                        plcsim.WriteInputPoint(bytepos, bitpos, set);
                    }
                    else if (t_size == S7COMM_TRANSPORT_SIZE_BYTE)
                    {
                        object set = new byte[len];
                        Array.Copy(data, 28, (byte[])set, 0, len);
                        plcsim.WriteInputImage(bytepos, ref set);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(nameof(DataReceived), ex);
                Disconnect();
            }
        }
        #endregion

        #region Private Methods
        private void Plcsim_ConnectionError(string ControlEngine, int Error)
        {
            try
            {
                log.Error($"PROSIM ERROR({Error}), Name:{Name}, IP:{Ip}, INS:{PlcPath}");
                Disconnect();
                ErrorHandler?.Invoke(Ip);
            }
            catch (Exception ex)
            {
                log.Error(nameof(Plcsim_ConnectionError), ex);
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var st = plcsim?.GetState() == "RUN_P" ? true : false;
                if (!st)
                {
                    plcsim?.SetState("RUN_P");
                    plcsim?.SetScanMode(ScanModeConstants.ContinuousScan);
                }
            }
            catch (Exception ex)
            {
                timer.Elapsed -= Timer_Elapsed;
                timer.Stop();
                log.Error(nameof(Timer_Elapsed), ex);
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
                    Disconnect();
                    plcsim = null;
                }

                // TODO: 관리되지 않는 리소스(관리되지 않는 개체)를 해제하고 아래의 종료자를 재정의합니다.
                // TODO: 큰 필드를 null로 설정합니다.

                disposedValue = true;
            }
        }

        // TODO: 위의 Dispose(bool disposing)에 관리되지 않는 리소스를 해제하는 코드가 포함되어 있는 경우에만 종료자를 재정의합니다.
        // ~S7Protocol() {
        //   // 이 코드를 변경하지 마세요. 위의 Dispose(bool disposing)에 정리 코드를 입력하세요.
        //   Dispose(false);
        // }

        // 삭제 가능한 패턴을 올바르게 구현하기 위해 추가된 코드입니다.
        public void Dispose()
        {
            // 이 코드를 변경하지 마세요. 위의 Dispose(bool disposing)에 정리 코드를 입력하세요.
            Dispose(true);
            // TODO: 위의 종료자가 재정의된 경우 다음 코드 줄의 주석 처리를 제거합니다.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
