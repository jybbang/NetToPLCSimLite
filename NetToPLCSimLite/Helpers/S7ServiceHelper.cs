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
    public class S7ServiceHelper
    {
        #region Fields
        private readonly ILog log;
        private TcpListener tcp;
        #endregion

        #region Properteis
        public int S7Port { get; set; } = CONST.S7_PORT;
        #endregion

        #region Constructors
        public S7ServiceHelper()
        {
            log = LogExt.log;
        }
        #endregion

        #region Public Methods
        public ServiceController FindS7Service()
        {
            var services = ServiceController.GetServices();
            var s7svc = services.FirstOrDefault(x => x.ServiceName == "s7oiehsx" || x.ServiceName == "s7oiehsx64");
            if (s7svc == null) throw new NullReferenceException("Can not find S7 online service.");
            return s7svc;
        }

        public bool StartS7Service(ServiceController s7svc, int timeout)
        {
            if (s7svc.Status == ServiceControllerStatus.Running) return true;
            s7svc.Start();
            s7svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(timeout));
            s7svc.Refresh();
            return s7svc.Status == ServiceControllerStatus.Running;
        }

        public bool StopS7Service(ServiceController s7svc, int timeout)
        {
            if (s7svc.Status == ServiceControllerStatus.Stopped) return true;
            s7svc.Stop();
            s7svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(timeout));
            s7svc.Refresh();
            return s7svc.Status == ServiceControllerStatus.Stopped;
        }

        public bool StartTcpServer()
        {
            try
            {
                tcp = new TcpListener(IPAddress.Any, S7Port);
                tcp.Start();
                return tcp.Server.IsBound;
            }
            catch
            {
                StopTcpServer();
                throw;
            }
        }

        public bool StopTcpServer()
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

        public bool IsS7PortAvailable()
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
            return isAvailable;
        }
        #endregion
    }
}
