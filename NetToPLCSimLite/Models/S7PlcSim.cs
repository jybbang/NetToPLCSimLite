using log4net;
using ProtoBuf;
using S7PROSIMLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace NetToPLCSimLite.Models
{
    [ProtoContract]
    public class S7PlcSim
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
        public string PlcIp { get; set; } = string.Empty;
        [ProtoMember(3)]
        public StationCpu Cpu { get; set; } = StationCpu.S400;
        [ProtoMember(4)]
        public int Rack { get; set; } = 0;
        [ProtoMember(5)]
        public int Slot { get; set; } = 3;
        [ProtoMember(6)]
        public bool IsStarted { get; set; } = false;
        [ProtoMember(7)]
        public bool IsConnected { get; set; } = false;
        [ProtoMember(8)]
        public string PlcPath { get; set; } = string.Empty;
        [ProtoMember(9)]
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
        public bool Connect()
        {
            try
            {
                if (Instance < 1 || Instance > 8) return false;
                Disconnect();

                log.Info($"CONNECTING, Name:{Name}, IP:{PlcIp}, INS:{PlcPath}");
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
                    log.Info($"OK, Name:{Name}, IP:{PlcIp}, INS:{PlcPath}");
                }
                log.Warn($"NG, Name:{Name}, IP:{PlcIp}, INS:{PlcPath}");
            }
            catch (Exception ex)
            {
                Disconnect();
                log.Error($"ERR, Name:{Name}, IP:{PlcIp}, INS:{PlcPath}", ex);
            }

            return IsConnected;
        }

        public void Disconnect()
        {
            timer.Elapsed -= Timer_Elapsed;
            timer.Stop();

            if (IsConnected)
            {
                plcsim.ConnectionError -= Plcsim_ConnectionError;
                plcsim.Disconnect();
                IsConnected = false;
                IsStarted = false;
                log.Info($"DISCONNECTED, Name:{Name}, IP:{PlcIp}, INS:{Instance}");
            }
        }

        public void DataReceived(byte[] received)
        {
            if (!IsConnected) return;
            try
            {
                queue.Enqueue(received);
                while (!queue.IsEmpty)
                {
                    if (queue.TryDequeue(out byte[] data))
                    {
                        // INPUT, WRITE
                        var len = data.Length - 28;
                        if (len > 0 && data[0] == 0x32 && data[1] == 0x01 && data[10] == 0x05 && data[14] == 0x10 && data[20] == 0x81)
                        {
                            // address
                            byte[] aa = new byte[4] { 0, data[21], data[22], data[23] };
                            var addr = BitConverter.ToInt32(aa, 0);
                            addr = BitConverter.IsLittleEndian ? IPAddress.HostToNetworkOrder(addr) : addr;
                            var bytepos = addr / 8;
                            var bitpos = addr % 8;

                            var t_size = data[15];
                            if (t_size == S7COMM_TRANSPORT_SIZE_BIT)
                            {
                                var set = data[28] == 0 ? false : true;
                                plcsim.WriteInputPoint(bytepos, bitpos, set);
                                log.Debug($" >> [IP:{PlcIp},{Instance}] I{bytepos}.{bitpos} ({set})");
                            }
                            else if (t_size == S7COMM_TRANSPORT_SIZE_BYTE)
                            {
                                if (len == 1)
                                {
                                    var set = data[28];
                                    plcsim.WriteInputPoint(bytepos, 0, set);
                                    log.Debug($" >> [IP:{PlcIp}:{Instance}] IB{bytepos} ({set})");
                                }
                                else if (len == 2)
                                {
                                    var set = (UInt16)(data[28] << 8 | data[29]);
                                    plcsim.WriteInputPoint(bytepos, 0, set);
                                    log.Debug($" >> [IP:{PlcIp}:{Instance}] IW{bytepos} ({set})");
                                }
                                else if (len == 4)
                                {
                                    var set = (UInt32)(data[28] << 24 | data[29] << 16 | data[30] << 8 | data[31]);
                                    plcsim.WriteInputPoint(bytepos, 0, set);
                                    log.Debug($" >> [IP:{PlcIp}:{Instance}] ID{bytepos} ({set})");
                                }
                                else
                                {
                                    object set = new byte[len];
                                    Array.Copy(data, 28, (byte[])set, 0, len);
                                    plcsim.WriteInputImage(bytepos, ref set);
                                    log.Debug($" >> [IP:{PlcIp}:{Instance}] I{bytepos} -> {len} BYTE");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Disconnect();
                log.Error(nameof(DataReceived), ex);
            }
        }
        #endregion

        #region Private Methods
        private void Plcsim_ConnectionError(string ControlEngine, int Error)
        {
            log.Error($"PROSIM ERROR({Error}), Name:{Name}, IP:{PlcIp}, INS:{PlcPath}");
            Disconnect();
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
                log.Error(nameof(S7PlcSim), ex);
            }
        }
        #endregion
    }
}
