using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using S7PROSIMLib;
using Serilog;

namespace NetToPLCSimLite.Models
{
    public class S7Protocol : IDisposable, INotifyPropertyChanged, IEquatable<S7Protocol>
    {
        #region Const
        private const byte S7COMM_TRANSPORT_SIZE_BIT = 1;
        private const byte S7COMM_TRANSPORT_SIZE_BYTE = 2;
        #endregion

        #region Fields
        public event PropertyChangedEventHandler PropertyChanged;
        public Action<string> ErrorHandler;

        private readonly S7ProSim plcsim = new S7ProSim();
        private CompositeDisposable runDisposables;
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

                    runDisposables?.Dispose();
                    runDisposables = new CompositeDisposable();
                    runDisposables.Add(Observable.Interval(TimeSpan.FromSeconds(1))
                        .Select(_ => plcsim?.GetState())
                        .Where(x => !x.Contains("RUN"))
                        .Subscribe(x => Plcsim_ConnectionError("Plc Stopped", -1)));

                    Log.Information($"{nameof(S7Protocol)}.{nameof(Connect)} - Connected, Name:{Name}, IP:{Ip}, Instance:{Instance}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"{nameof(S7Protocol)}.{nameof(Connect)} - Name:{Name}, IP:{Ip}, Instance:{Instance}");
                Disconnect();
            }

            return IsConnected;
        }

        public void Disconnect()
        {
            try
            {
                runDisposables?.Dispose();
                if (IsConnected)
                {
                    plcsim.ConnectionError -= Plcsim_ConnectionError;
                    plcsim.Disconnect();
                    Log.Information($"{nameof(S7Protocol)}.{nameof(Disconnect)} - Disconnected, Name:{Name}, IP:{Ip}, Instance:{Instance}");
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
                Log.Error(ex, $"{nameof(S7Protocol)}.{nameof(DataReceived)} - Name:{Name}, IP:{Ip}, Instance:{Instance}, Data:{{ @data }}", data);
                Disconnect();
            }
        }
        #endregion

        #region Private Methods
        private void Plcsim_ConnectionError(string ControlEngine, int Error)
        {
            try
            {
                Log.Warning($"{nameof(S7Protocol)}.{nameof(Plcsim_ConnectionError)}.{ControlEngine} - Name:{Name}, IP:{Ip}, Instance:{Instance}");
                Disconnect();
                ErrorHandler?.Invoke(Ip);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"{nameof(S7Protocol)}.{nameof(Plcsim_ConnectionError)}.{ControlEngine} - Name:{Name}, IP:{Ip}, Instance:{Instance}");
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
                    runDisposables?.Dispose();
                    Disconnect();
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
            // GC.SuppressFinalize(this);
        }
        #endregion

        #region IEquatable Support
        public override bool Equals(object obj)
        {
            return Equals(obj as S7Protocol);
        }

        public bool Equals(S7Protocol other)
        {
            return other != null &&
                   Ip == other.Ip;
        }

        public override int GetHashCode()
        {
            return 475518940 + EqualityComparer<string>.Default.GetHashCode(Ip);
        }
        #endregion
    }
}
