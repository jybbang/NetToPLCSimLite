using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetToPLCSimLite;

// Basic Code of this server is taken from:
// http://www.codeproject.com/KB/IP/BasicTcpServer.aspx
//
namespace TcpLib
{
    /// <SUMMARY>
    /// This class holds useful information for keeping track of each client connected
    /// to the server, and provides the means for sending/receiving data to the remote
    /// host.
    /// </SUMMARY>
    public class ConnectionState
    {
        internal Socket m_conn;
        internal TcpServer m_server;
        internal TcpServiceProvider m_provider;
        internal byte[] m_buffer;

        /// <SUMMARY>
        /// Tells you the IP Address of the remote host.
        /// </SUMMARY>
        public EndPoint RemoteEndPoint
        {
            get { return m_conn.RemoteEndPoint; }
        }

        /// <SUMMARY>
        /// Returns the number of bytes waiting to be read.
        /// </SUMMARY>
        public int AvailableData
        {
            get { return m_conn.Available; }
        }

        /// <SUMMARY>
        /// Tells you if the socket is connected.
        /// </SUMMARY>
        public bool Connected
        {
            get { return m_conn.Connected; }
        }

        /// <SUMMARY>
        /// Reads data on the socket, returns the number of bytes read.
        /// </SUMMARY>
        public int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (m_conn.Available > 0)
                    return m_conn.Receive(buffer, offset, count, SocketFlags.None);
                else return 0;
            }
            catch (Exception ex)
            {
                LogExt.log.Error("TcpServer", ex);
                return 0;
            }
        }

        /// <SUMMARY>
        /// Sends Data to the remote host.
        /// </SUMMARY>
        public bool Write(byte[] buffer, int offset, int count)
        {
            try
            {
                m_conn.Send(buffer, offset, count, SocketFlags.None);
                return true;
            }
            catch (Exception ex)
            {
                LogExt.log.Error("TcpServer", ex);
                return false;
            }
        }


        /// <SUMMARY>
        /// Ends connection with the remote host.
        /// </SUMMARY>
        public void EndConnection()
        {
            //if (m_conn != null && m_conn.Connected)
            //{
            //    m_conn.Shutdown(SocketShutdown.Both);
            //    m_conn.Close();
            //}
            m_server.DropConnection(this);
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
            throw new Exception("Derived clases must override Clone method.");
        }

        /// <SUMMARY>
        /// Gets executed when the server accepts a new connection.
        /// </SUMMARY>
        public abstract void OnAcceptConnection(ConnectionState state);

        /// <SUMMARY>
        /// Gets executed when the server detects incoming data.
        /// This method is called only if OnAcceptConnection has already finished.
        /// </SUMMARY>
        public abstract void OnReceiveData(ConnectionState state);

        /// <SUMMARY>
        /// Gets executed when the server needs to shutdown the connection.
        /// </SUMMARY>
        public abstract void OnDropConnection(ConnectionState state);
    }

    public class TcpServer : IDisposable
    {
        private int m_port;
        private Socket m_listener;
        private TcpServiceProvider m_provider;
        private ArrayList m_connections;
        private int _maxConnections = 100;

        private AsyncCallback ConnectionReady;
        private WaitCallback AcceptConnection;
        private AsyncCallback ReceivedDataReady;

        private bool Disposed = false;

        /// <SUMMARY>
        /// Initializes server. To start accepting connections call Start method.
        /// </SUMMARY>
        public TcpServer(TcpServiceProvider provider, int port)
        {
            m_provider = provider;
            m_port = port;
            m_listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_connections = new ArrayList();
            ConnectionReady = new AsyncCallback(ConnectionReady_Handler);
            AcceptConnection = new WaitCallback(AcceptConnection_Handler);
            ReceivedDataReady = new AsyncCallback(ReceivedDataReady_Handler);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!Disposed)
                {
                    try
                    {
                        Stop();
                    }
                    catch (Exception ex)
                    {
                        LogExt.log.Error("Dispose", ex);
                    }
                }
            }
            this.Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /// <SUMMARY>
        /// Start accepting connections.
        /// A false return value tell you that the port is not available.
        /// </SUMMARY>
        public bool Start(IPAddress ip, ref string error)
        {
            try
            {
                m_listener.Bind(new IPEndPoint(ip, m_port));
                m_listener.Listen(100);
                m_listener.BeginAccept(ConnectionReady, null);
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }

        /// <SUMMARY>
        /// Callback function: A new connection is waiting.
        /// </SUMMARY>
        private void ConnectionReady_Handler(IAsyncResult ar)
        {
            lock (this)
            {
                if (m_listener == null) return;
                Socket conn = m_listener.EndAccept(ar);
                if (m_connections.Count >= _maxConnections)
                {
                    //Max number of connections reached.
                    conn.Shutdown(SocketShutdown.Both);
                    conn.Close();
                }
                else
                {
                    //Start servicing a new connection
                    ConnectionState st = new ConnectionState();
                    st.m_conn = conn;
                    st.m_server = this;
                    st.m_provider = (TcpServiceProvider)m_provider.Clone();
                    st.m_buffer = new byte[4];
                    m_connections.Add(st);
                    //Queue the rest of the job to be executed latter
                    ThreadPool.QueueUserWorkItem(AcceptConnection, st);
                }
                //Resume the listening callback loop
                m_listener.BeginAccept(ConnectionReady, null);
            }
        }

        /// <SUMMARY>
        /// Executes OnAcceptConnection method from the service provider.
        /// </SUMMARY>
        private void AcceptConnection_Handler(object state)
        {
            ConnectionState st = state as ConnectionState;
            try {
                st.m_provider.OnAcceptConnection(st);

                //Starts the ReceiveData callback loop
                if (st.m_conn.Connected)
                {
                    st.m_conn.BeginReceive(st.m_buffer, 0, 0, SocketFlags.None, ReceivedDataReady, st);
                }
            }
            catch (Exception ex)
            {
                LogExt.log.Error("AcceptConnection_Handler", ex);
                //report error in provider... Probably to the EventLog
            }
        }

        /// <SUMMARY>
        /// Executes OnReceiveData method from the service provider.
        /// </SUMMARY>
        private void ReceivedDataReady_Handler(IAsyncResult ar)
        {
            ConnectionState st = ar.AsyncState as ConnectionState;
            try
            {
                st.m_conn.EndReceive(ar);
            }
            catch (Exception ex)
            {
                LogExt.log.Error("ReceivedDataReady_Handler", ex);
                return;
            }
            //Im considering the following condition as a signal that the
            //remote host droped the connection.
            if (st.m_conn.Available == 0) DropConnection(st);
            else
            {
                try {
                    st.m_provider.OnReceiveData(st);

                    //Resume ReceivedData callback loop
                    if (st.m_conn.Connected)
                    {
                        st.m_conn.BeginReceive(st.m_buffer, 0, 0, SocketFlags.None,
                          ReceivedDataReady, st);
                    }
                }
                catch (Exception ex)
                {
                    LogExt.log.Error("ReceivedDataReady_Handler", ex);
                    //report error in the provider
                }
            }
        }

        /// <SUMMARY>
        /// Shutsdown the server
        /// </SUMMARY>
        public void Stop()
        {
            lock (this)
            {
                m_listener.Close();
                m_listener = null;
                //Close all active connections
                foreach (object obj in m_connections)
                {
                    ConnectionState st = obj as ConnectionState;
                    try { st.m_provider.OnDropConnection(st);

                        st.m_conn.Shutdown(SocketShutdown.Both);
                        st.m_conn.Close();
                    }
                    catch (Exception ex)
                    {
                        LogExt.log.Error("Stop", ex);
                        //some error in the provider
                    }
                }
                m_connections.Clear();
            }
        }

        /// <SUMMARY>
        /// Removes a connection from the list
        /// </SUMMARY>
        internal void DropConnection(ConnectionState st)
        {
            lock (this)
            {
                try {
                    st.m_provider.OnDropConnection(st);

                    st.m_conn.Shutdown(SocketShutdown.Both);
                    st.m_conn.Close();
                    if (m_connections.Contains(st))
                        m_connections.Remove(st);
                }
                catch (Exception ex)
                {
                    LogExt.log.Error("TcpServer", ex);
                    //some error in the provider
                }
            }
        }

        public int MaxConnections
        {
            get
            {
                return _maxConnections;
            }
            set
            {
                _maxConnections = value;
            }
        }

        public int CurrentConnections
        {
            get
            {
                lock (this) { return m_connections.Count; }
            }
        }
    }
}