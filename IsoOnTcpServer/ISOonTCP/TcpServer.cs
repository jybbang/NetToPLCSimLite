using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections;
using log4net;
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
        public EndPoint RemoteEndPoint {
            get { return m_conn.RemoteEndPoint; }
        }

        /// <SUMMARY>
        /// Returns the number of bytes waiting to be read.
        /// </SUMMARY>
        public int AvailableData {
            get { return m_conn.Available; }
        }

        /// <SUMMARY>
        /// Tells you if the socket is connected.
        /// </SUMMARY>
        public bool Connected {
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
            catch
            {
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
            catch
            {
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
                m_conn.Shutdown(SocketShutdown.Both);
                m_conn.Close();
            }
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

        ~TcpServer()
        {
            Dispose(false);
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
                    catch
                    {

                    }
                }
            }
            this.Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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

                    // modified by Ryu
                    LogExt.log.Debug("m_connections.Count >= _maxConnections");
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
            try { st.m_provider.OnAcceptConnection(st); }
            catch
            {
                //report error in provider... Probably to the EventLog
            }
            //Starts the ReceiveData callback loop
            if (st.m_conn.Connected)
            {
                try
                {
                    st.m_conn.BeginReceive(st.m_buffer, 0, 0, SocketFlags.None, ReceivedDataReady, st);
                }
                catch (Exception ex)
                {
                    // modified by Ryu
                    DropConnection(st);
                    LogExt.log.Error($"AcceptConnection_Handler", ex);                                
                }
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
                if (st.m_conn.Available == 0)
                {
                    DropConnection(st);
                    return;
                }
            }
            catch
            {
                DropConnection(st);
                return;
            }
            st.m_conn.EndReceive(ar);
            //Im considering the following condition as a signal that the
            //remote host droped the connection.
            if (st.m_conn.Available == 0) DropConnection(st);
            else
            {
                try { st.m_provider.OnReceiveData(st); }
                catch
                {
                    //report error in the provider
                }
                //Resume ReceivedData callback loop
                if (st.m_conn.Connected)
                {
                    try
                    {
                        st.m_conn.BeginReceive(st.m_buffer, 0, 0, SocketFlags.None,
                          ReceivedDataReady, st);
                    }
                    catch
                    {
                        DropConnection(st);
                    }
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
                    try { st.m_provider.OnDropConnection(st); }
                    catch
                    {
                        //some error in the provider
                    }
                    st.m_conn.Shutdown(SocketShutdown.Both);
                    st.m_conn.Close();
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
                try { st.m_provider.OnDropConnection(st); }
                catch
                {
                    //some error in the provider
                }

                try { st.m_conn.Shutdown(SocketShutdown.Both); }
                catch
                {
                    // TODO: 10.7.2014
                    // handle System.ObjectDisposedException correctly
                    // implement IDisposable
                }
                st.m_conn.Close();
                if (m_connections.Contains(st))
                    m_connections.Remove(st);
            }
        }

        public int MaxConnections {
            get {
                return _maxConnections;
            }
            set {
                _maxConnections = value;
            }
        }

        public int CurrentConnections {
            get {
                lock (this) { return m_connections.Count; }
            }
        }
    }
}