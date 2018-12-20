using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using IsoOnTcp;

namespace NetToPLCSim
{
    public class Service
    {
        public Service()
        {
        }

        private string m_Servicename = string.Empty;
        private const int m_cPort = 102;
        private TcpListener m_Listener;

        public Config m_Conf = new Config();
        public List<IsoToS7online> m_servers = new List<IsoToS7online>();

        public bool StopService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);

            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                // MessageBox.Show("Service '" + serviceName + "' 종료 시간 초과");
                return false;
            }
            catch (Exception)
            {
                // MessageBox.Show("Service '" + serviceName + "' 종료 오류: " + e);
                return false;
            }

            // Maybe the service was stopped after waiting time in dialog
            service.Refresh();

            return (service.Status == ServiceControllerStatus.Stopped);
        }

        public bool StartService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);

            if (service.Status == ServiceControllerStatus.Running)
                return true;

            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                // MessageBox.Show("Service '" + serviceName + "' 시작 시간 초과");
                return false;
            }
            catch
            {
                // MessageBox.Show("Service '" + serviceName + "' 시작 오류");
                return false;
            }

            // Maybe the service was started after waiting time in dialog
            service.Refresh();

            return (service.Status == ServiceControllerStatus.Running);
        }

        public bool GetS7DOSHelperServiceName()
        {
            string machineName = ".";   // local
            ServiceController[] services = null;

            try
            {
                services = ServiceController.GetServices(machineName);
            }
            catch
            {
                m_Servicename = String.Empty;
                return false;
            }

            for (int i = 0; i < services.Length; i++)
            {
                if ((services[i].ServiceName == "s7oiehsx") || (services[i].ServiceName == "s7oiehsx64"))
                {
                    m_Servicename = services[i].ServiceName;
                    return true;
                }
            }

            m_Servicename = String.Empty;
            return false;
        }

        public bool IsTcpPortAvailable()
        {
            bool isAvailable = true;

            // Evaluate current system tcp connections. This is the same information provided
            // by the netstat command line application, just in .Net strongly-typed object
            // form.  We will look through the list, and if our port we would like to use
            // in our TcpClient is occupied, we will set isAvailable to false.
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == m_cPort)
                {
                    isAvailable = false;
                    break;
                }
            }
            return isAvailable;
        }

        public bool Step1()
        {
            return StopService(m_Servicename, 20000);
        }

        public bool Step2()
        {
            try
            {
                m_Listener = new TcpListener(IPAddress.Any, m_cPort);
                m_Listener.Start();
            }
            catch (SocketException e)
            {
                int test = e.ErrorCode;
                return false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool Step3()
        {
            return StartService(m_Servicename, 20000);
        }

        public bool Step4()
        {
            try
            {
                m_Listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Step5()
        {
            return IsTcpPortAvailable();
        }

        public bool getPort102back()
        {
            if (!Step1())
            {
                // MessageBox.Show($"Step 1) Stopping service {m_Servicename} Failed");
                return false;
            }

            Thread.Sleep(5000);

            if (!Step2())
            {
                // MessageBox.Show($"Step 2) Starting our own Server on TCP Port {m_cPort} Failed");
                return false;
            }

            if (!Step3())
            {
                Step4(); // stops the previous started TCP server
                // MessageBox.Show($"Step 3) Starting service {m_Servicename} Failed");
                return false;
            }

            if (!Step4())
            {
                // MessageBox.Show($"Step 4) Stopping our own Server Failed");
                return false;
            }

            if (!Step5())
            {
                // MessageBox.Show($"Step 5) Checking TCP Port {m_cPort} Failed");
                return false;
            }

            return true;
        }

        public int AddStation(string stationName, IPAddress networkIpAddress, IPAddress plcsimIpAddress, int rack, int slot, bool tsapCheckEnabled)
        {
            int stationIdx = -1;

            if (m_Conf.IsStationNameUnique(stationName) == false)
                return stationIdx;

            StationData station = new StationData(stationName, networkIpAddress, plcsimIpAddress, rack, slot, tsapCheckEnabled);

            m_Conf.Stations.Add(station);
            stationIdx = m_Conf.Stations.IndexOf(station);

            IsoToS7online srv = new IsoToS7online(station.TsapCheckEnabled);
            m_servers.Add(srv);

            return stationIdx;
        }

        public void DeleteStation(int stationIdx)
        {
            if (stationIdx < 0)
                return;

            if (m_Conf.Stations[stationIdx].Status == StationStatus.RUNNING.ToString())
            {
                m_servers[stationIdx].stop();
            }

            m_servers.RemoveAt(stationIdx);
            m_Conf.Stations.RemoveAt(stationIdx);
        }

        public bool StartServer(int stationIdx)
        {
            String Temp = string.Empty;

            if (stationIdx < 0)
                return false;

            StationData station = m_Conf.Stations[stationIdx];

            List<byte[]> tsaps = new List<byte[]>();
            byte tsap2 = (byte)(((station.PlcsimRackNumber << 4) | station.PlcsimSlotNumber));
            tsaps.Add(new byte[] { 0x01, tsap2 });
            tsaps.Add(new byte[] { 0x02, tsap2 });
            tsaps.Add(new byte[] { 0x03, tsap2 });

            if (!m_servers[stationIdx].start(station.Name, station.NetworkIpAddress, tsaps, station.PlcsimIpAddress, station.PlcsimRackNumber, station.PlcsimSlotNumber, ref Temp))
            {
                station.Connected = false;
                station.Status = StationStatus.ERROR.ToString();

                return false;
            }

            station.Connected = true;
            station.Status = StationStatus.RUNNING.ToString();

            return true;
        }

        public void StopServer(int stationIdx)
        {
            if (stationIdx < 0)
                return;

            StationData station = m_Conf.Stations[stationIdx];

            if (station.Status == StationStatus.RUNNING.ToString())
            {
                m_servers[stationIdx].stop();
                station.Connected = false;
                //m_servers[stationIdx].Dispose();
                station.Status = StationStatus.STOPPED.ToString();
            }
        }
    }
}
