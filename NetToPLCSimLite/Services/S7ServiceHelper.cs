using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetToPLCSimLite.Services
{
    public class S7ServiceHelper
    {
        #region Fields
        private readonly ILog log;
        private TcpListener tcp;
        #endregion

        #region Constructors
        public S7ServiceHelper()
        {
            log = LogExt.log;
        }
        #endregion

        #region Public Methods
        public void GetS7OnlinePort()
        {
            try
            {
                log.Info("RUN, Get S7 online port.");
                var s7svc = FindS7Service();
                var before = IsTcpPortAvailable(CONST.S7_PORT);
                if (!before)
                {
                    if (StopS7Service(s7svc)) log.Info("OK, Stop S7 online service.");
                    else throw new InvalidOperationException("NG, Can not stop S7 online service.");
                    Thread.Sleep(50);

                    if (StartTcpServer()) log.Info("OK, Start temporary TCP server.");
                    else throw new InvalidOperationException("NG, Can not start TCP server.");
                    Thread.Sleep(50);

                    if (StartS7Service(s7svc)) log.Info("OK, Start S7 online service.");
                    else throw new InvalidOperationException("NG, Can not start S7 online service.");
                    Thread.Sleep(50);

                    if (StopTcpServer()) log.Info("OK, Stop temporary TCP server.");
                    else throw new InvalidOperationException("NG, Can not stop TCP server.");
                    Thread.Sleep(50);

                    var after = IsTcpPortAvailable(CONST.S7_PORT);
                    if (after) log.Info("COMPLETE, Get S7 online port.");
                    else throw new InvalidOperationException("FAIL, Can not get S7 online port.");
                }
                else log.Info("COMPLETE, Aleady get S7 online port.");
            }
            catch (Exception ex)
            {
                log.Error("S7Online", ex);
                throw;
            }
        }
        #endregion

        #region Private Methods
        private ServiceController FindS7Service()
        {
            var services = ServiceController.GetServices();
            var s7svc = services.FirstOrDefault(x => x.ServiceName == "s7oiehsx" || x.ServiceName == "s7oiehsx64");
            if (s7svc == null) throw new NullReferenceException("Can not find S7 online service.");
            return s7svc;
        }

        private bool StartS7Service(ServiceController s7svc)
        {
            if (s7svc.Status == ServiceControllerStatus.Running) return true;
            s7svc.Start();
            s7svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(CONST.SERVICE_TIMEOUT));
            s7svc.Refresh();
            return s7svc.Status == ServiceControllerStatus.Running;
        }

        private bool StopS7Service(ServiceController s7svc)
        {
            if (s7svc.Status == ServiceControllerStatus.Stopped) return true;
            s7svc.Stop();
            s7svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(CONST.SERVICE_TIMEOUT));
            s7svc.Refresh();
            return s7svc.Status == ServiceControllerStatus.Stopped;
        }

        private bool StartTcpServer()
        {
            try
            {
                tcp = new TcpListener(IPAddress.Any, CONST.S7_PORT);
                tcp.Start();
                return tcp.Server.IsBound;
            }
            catch
            {
                StopTcpServer();
                throw;
            }
        }

        private bool StopTcpServer()
        {
            try
            {
                if (tcp != null) tcp.Stop();
                return !tcp.Server.IsBound;
            }
            catch
            {
                return false;
            }
        }

        private bool IsTcpPortAvailable(int port)
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
                if (endpoint.Port == port)
                {
                    isAvailable = false;
                    break;
                }
            }
            return isAvailable;
        }
        #endregion
    }
}
