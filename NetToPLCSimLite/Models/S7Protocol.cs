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
using ProtoBuf;
using S7PROSIMLib;

namespace NetToPLCSimLite.Models
{
    [ProtoContract]
    public class S7Protocol
    {
        #region Const
        private const byte S7COMM_TRANSPORT_SIZE_BIT = 1;
        private const byte S7COMM_TRANSPORT_SIZE_BYTE = 2;
        #endregion

        #region Fields
        private readonly ILog log = LogExt.log;
        private readonly S7ProSim plcsim = new S7ProSim();
        private readonly Timer timer = new Timer();
        private readonly ConcurrentQueue<byte[]> queue = new ConcurrentQueue<byte[]>();
        #endregion

        #region Properties
        [ProtoMember(1)]
        public string Name { get; set; } = string.Empty;
        [ProtoMember(2)]
        public string Ip { get; set; } = string.Empty;
        [ProtoMember(3)]
        public bool IsStarted { get; set; } = false;
        [ProtoMember(4)]
        public bool IsConnected { get; set; } = false;
        [ProtoMember(5)]
        public StationCpu Cpu { get; set; } = StationCpu.S400;
        [ProtoMember(6)]
        public int Rack { get; set; } = 0;
        [ProtoMember(7)]
        public int Slot { get; set; } = 3;
        [ProtoMember(8)]
        public string PlcPath { get; set; } = string.Empty;
        [ProtoMember(9)]
        public int Instance { get; set; } = -1;
        public Action<string> ErrorHandler;
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
            return $"Name:{Name}, IP:{Ip}, Connected:{IsConnected}, Started:{IsStarted}, Instance:{Instance}";
        }

        public bool Connect()
        {
            try
            {
                if (Instance < 1 || Instance > 8) return false;
                Disconnect();

                log.Info($"CONNECTING, Name:{Name}, IP:{Ip}, INS:{PlcPath}");
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
                    log.Info($"OK, Name:{Name}, IP:{Ip}, INS:{PlcPath}");
                }
                else log.Warn($"NG, Name:{Name}, IP:{Ip}, INS:{PlcPath}");
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
                IsStarted = false;
            }
        }

        public void DataReceived(byte[] received)
        {
            try
            {
                if (!IsConnected) return;
                queue.Enqueue(received);
                while (!queue.IsEmpty)
                {
                    if (queue.TryDequeue(out byte[] data))
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
                var st = plcsim.GetState() == "RUN_P" ? true : false;
                if (!st)
                {
                    plcsim.SetState("RUN_P");
                    plcsim.SetScanMode(ScanModeConstants.ContinuousScan);
                }
            }
            catch (Exception ex)
            {
                log.Error(nameof(Timer_Elapsed), ex);
            }
        }
        #endregion
    }
}
