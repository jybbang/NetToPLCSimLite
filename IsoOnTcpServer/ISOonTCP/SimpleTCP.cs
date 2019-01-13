using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetToPLCSimLite;
using SimpleTCP;

namespace TcpLib
{
    /// <SUMMARY>
    /// This class holds useful information for keeping track of each client connected
    /// to the server, and provides the means for sending/receiving data to the remote
    /// host.
    /// </SUMMARY>
    public class ConnectionState
    {
        internal TcpClient m_conn;
        internal TcpServer m_server;
        internal TcpServiceProvider m_provider;
        internal byte[] m_buffer;

        /// <SUMMARY>
        /// Returns the number of bytes waiting to be read.
        /// </SUMMARY>
        public int AvailableData
        {
            get { return m_conn.Client.Available; }
        }

        /// <SUMMARY>
        /// Tells you if the socket is connected.
        /// </SUMMARY>
        public bool Connected
        {
            get { return m_conn.Client.Connected; }
        }

        /// <SUMMARY>
        /// Sends Data to the remote host.
        /// </SUMMARY>
        public bool Write(byte[] buffer, int offset, int count)
        {
            try
            {
                m_conn.Client.Send(buffer, offset, count, SocketFlags.None);
                return true;
            }
            catch (Exception ex)
            {
                LogExt.log.Error(nameof(ConnectionState), ex);
                return false;
            }
        }

        /// <SUMMARY>
        /// Ends connection with the remote host.
        /// </SUMMARY>
        public void EndConnection()
        {
            if (m_conn != null && m_conn.Connected)
            {
                m_conn.Close();
            }
            m_conn.Dispose();
            m_conn = null;
        }
    }

    /// <SUMMARY>
    /// Allows to provide the server with the actual code that is goint to service
    /// incoming connections.
    /// </SUMMARY>
    public abstract class TcpServiceProvider : ICloneable
    {
        /// <SUMMARY>
        /// Provides a new instance of the object.
        /// </SUMMARY>
        public virtual object Clone()
        {
            throw new NotImplementedException();
        }

        /// <SUMMARY>
        /// Gets executed when the server accepts a new connection.
        /// </SUMMARY>
        public abstract void OnAcceptConnection(ConnectionState state);

        /// <SUMMARY>
        /// Gets executed when the server detects incoming data.
        /// This method is called only if OnAcceptConnection has already finished.
        /// </SUMMARY>
        public abstract void OnReceiveData(byte[] msg);

        /// <SUMMARY>
        /// Gets executed when the server needs to shutdown the connection.
        /// </SUMMARY>
        public abstract void OnDropConnection(ConnectionState state);
    }

    public class TcpServer : IDisposable
    {
        #region Fields
        private ConnectionState connectionState;
        private SimpleTcpServer server;
        private readonly int port;
        private readonly TcpServiceProvider serviceProvider;
        #endregion

        #region Constructors
        public TcpServer(int port, TcpServiceProvider serviceProvider)
        {
            this.port = port;
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        #endregion

        #region Public Methods
        public bool Start(IPAddress ipAddress)
        {
            try
            {
                if (server != null) Stop();
                server = new SimpleTcpServer().Start(ipAddress, port);
                server.ClientConnected += Server_ClientConnected;
                server.ClientDisconnected += Server_ClientDisconnected;
                server.DataReceived += Server_DataReceived;
                LogExt.log.Debug($"SimpleTCP, Start, IP:{ipAddress}, Port:{port}");
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Stop()
        {
            lock (this)
            {
                server.ClientConnected -= Server_ClientConnected;
                server.ClientDisconnected -= Server_ClientDisconnected;
                server.DataReceived -= Server_DataReceived;

                connectionState.EndConnection();
                connectionState = null;
                server.Stop();
                server = null;

                LogExt.log.Debug($"SimpleTCP, Stop.");
            }
        }
        #endregion

        #region Private Methods
        private void Server_DataReceived(object sender, Message e)
        {
            // ReceivedDataReady_Handler
            try
            {
                connectionState.m_provider.OnReceiveData(e.Data);
            }
            catch (Exception ex)
            {
                LogExt.log.Error(nameof(Server_DataReceived), ex);
                //report error in the provider
            }
        }

        private void Server_ClientDisconnected(object sender, System.Net.Sockets.TcpClient e)
        {
            try
            {
                connectionState.m_provider.OnDropConnection(connectionState);
                LogExt.log.Debug($"SimpleTCP, Client Disconnected, Client:{e.Client.AddressFamily}");
            }
            catch (Exception ex)
            {
                LogExt.log.Error(nameof(Server_ClientDisconnected), ex);
                //some error in the provider
            }
        }

        private void Server_ClientConnected(object sender, System.Net.Sockets.TcpClient e)
        {
            try
            {
                // ConnectionReady_Handler
                var st = new ConnectionState();
                st.m_conn = e;
                st.m_server = this;
                st.m_provider = (TcpServiceProvider)serviceProvider.Clone();
                st.m_buffer = new byte[4];

                //AcceptConnection_Handler
                st.m_provider.OnAcceptConnection(st);
                connectionState = st;

                LogExt.log.Debug($"SimpleTCP, Connected, Client:{e.Client.AddressFamily}");
            }
            catch (Exception ex)
            {
                LogExt.log.Error(nameof(Server_ClientConnected), ex);
                //report error in provider... Probably to the EventLog
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
                    Stop();
                }

                // TODO: 관리되지 않는 리소스(관리되지 않는 개체)를 해제하고 아래의 종료자를 재정의합니다.
                // TODO: 큰 필드를 null로 설정합니다.

                disposedValue = true;
            }
        }

        // TODO: 위의 Dispose(bool disposing)에 관리되지 않는 리소스를 해제하는 코드가 포함되어 있는 경우에만 종료자를 재정의합니다.
        // ~TcpServer() {
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
