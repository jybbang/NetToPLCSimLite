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

namespace NetToPLCSimLite.Helpers
{
    public static class S7ServiceHelper
    {
        #region Fields
        private static readonly ILog log = LogExt.log;
        private static TcpListener tcp;
        #endregion

        #region Properteis
        public static int S7Port { get; set; } = CONST.S7_PORT;
        public static int Timeout { get; set; } = CONST.SVC_TIMEOUT;
        #endregion

        #region Public Methods
        public static ServiceController FindS7Service()
        {
            var services = ServiceController.GetServices();
            var s7svc = services.FirstOrDefault(x => x.ServiceName == "s7oiehsx" || x.ServiceName == "s7oiehsx64");

            if (s7svc == null) log.Warn("NG, Can not find S7 online service.");
            else log.Info("OK, Find S7 online service.");
            return s7svc;
        }

        public static bool StartS7Service(ServiceController s7svc)
        {
            if (s7svc == null) return false;
            if (s7svc.Status == ServiceControllerStatus.Running) return true;
            s7svc.Start();
            s7svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(Timeout));
            s7svc.Refresh();

            if (s7svc.Status == ServiceControllerStatus.Running)
            {
                log.Info("OK, Start S7 online service.");
                return true;
            }
            else
            {
                log.Warn("NG, Can not start S7 online service.");
                return false;
            }
        }

        public static bool StopS7Service(ServiceController s7svc)
        {
            if (s7svc == null) return false;
            if (s7svc.Status == ServiceControllerStatus.Stopped) return true;
            s7svc.Stop();
            s7svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(Timeout));
            s7svc.Refresh();

            if (s7svc.Status == ServiceControllerStatus.Stopped)
            {
                log.Info("OK, Stop S7 online service.");
                return true;
            }
            else
            {
                log.Warn("NG, Can not stop S7 online service.");
                return false;
            }
        }

        public static bool StartTcpServer()
        {
            try
            {
                tcp = new TcpListener(IPAddress.Any, S7Port);
                tcp.Start();
                if (tcp.Server.IsBound)
                {
                    log.Info("OK, Start TCP Server.");
                    return true;
                }
                else
                {
                    log.Warn("NG, Start TCP Server.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StopTcpServer();
                log.Error(nameof(StartTcpServer), ex);
                return false;
            }
        }

        public static bool StopTcpServer()
        {
            try
            {
                if (tcp != null) tcp.Stop();
                if (!tcp.Server.IsBound)
                {
                    log.Info("OK, Stop TCP Server.");
                    return true;
                }
                else
                {
                    log.Warn("NG, Stop TCP Server.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error(nameof(StartTcpServer), ex);
                return false;
            }
        }

        public static bool IsS7PortAvailable()
        {
            bool isAvailable = true;

            // Evaluate current system tcp connections. This is the same information provided
            // by the netstat command line application, just in .Net strongly-typed object
            // form.  We will look through the list, and if our port we would like to use
            // in our TcpClient is occupied, we will set isAvailable to false.
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (var endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == S7Port)
                {
                    isAvailable = false;
                    break;
                }
            }

            if (isAvailable)
            {
                log.Info($"OK, Port {S7Port} avilable.");
                return true;
            }
            else
            {
                log.Warn($"NG, Port {S7Port} avilable.");
                return false;
            }
        }
        #endregion
    }
}
