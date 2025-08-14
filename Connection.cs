using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SonicConnect
{
    public class Connection: IDisposable
    {
        private string _host;
        private int _port;
        private string _password;

        private TcpClient _tcpClient;

        private NetworkStream _networkStream;
        private StreamWriter _writer;
        private StreamReader _reader;

        private bool _disposed = false;

        public bool IsOpen => _tcpClient != null && _tcpClient.Connected;

        public string Host{ get { return _host; } }
        public int Port { get { return _port; } }
        public string Password { get { return _password; } }

        public StreamWriter Writer {  get { return _writer; } }
        public StreamReader Reader {  get { return _reader; } }

        public int ResponseTimeOutInMSec { get; set; } // Default timeout of 10 seconds


        public Connection(string host, int port, string password): this(host, port, password, 10000) { }

        public Connection(string host, int port, string password,int ResponseTimeOutInMSec= 10000)
        {
            _host = host;
            _port = port;
            _password = password;
            _tcpClient = new TcpClient();
            this.ResponseTimeOutInMSec = ResponseTimeOutInMSec;
        }

        public void Open()
        {
            if (!IsConnected())
            {
                _tcpClient.Connect(_host, _port);
                _networkStream = _tcpClient.GetStream();
                _writer = new StreamWriter(_networkStream, Encoding.ASCII) { AutoFlush = true };
                _reader = new StreamReader(_networkStream, Encoding.ASCII);
            }
        }

        public bool IsConnected()
        {
            try
            {
                if (_tcpClient?.Client == null)
                    return false;

                if (!_tcpClient.Client.Connected)
                    return false;

                // Check if the socket is readable and there is no data (meaning remote closed connection)
                bool readReady = _tcpClient.Client.Poll(0, SelectMode.SelectRead); //True= socket is readable
                bool noData = (_tcpClient.Client.Available == 0); // True= no data available

                return !(readReady && noData);// If the socket is readable and no data is available, it means the remote end has closed the connection.
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Tell the GC not to call the finalizer
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _writer?.Dispose();
                _writer = null;

                _reader?.Dispose();
                _reader = null;

                _networkStream?.Dispose();
                _networkStream = null;

                if (_tcpClient != null)
                {
                    try
                    {
                        if (IsConnected())
                            _tcpClient.Client?.Shutdown(SocketShutdown.Both);
                    }
                    catch (SocketException) 
                    {
                        //ignore exceptions during shutdown
                    }

                    _tcpClient.Dispose();
                    _tcpClient = null;
                }
            }

            _disposed = true;
        }
    }
}
