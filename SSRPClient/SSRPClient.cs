using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SSRPClient
{
    public class SSRPClient : IDisposable
    {
        private const ushort SQL_SERVER_BROWSER_UDP_PORT = 1434;

        private readonly UdpClient udpClient;
        private readonly DataTable dataSourcesTable;
        private readonly int waitTimeout;

        /// <summary>
        /// A SQL Server Resolution Protocol client for detecting SQL Server instances within a network.
        /// </summary>
        /// <param name="waitTimeout">Total time in milliseconds to listen for responses. Default: 5000ms</param>
        /// <param name="receiveTimeout">Time to wait before timing out a receive request. Default: 1000ms</param>
        public SSRPClient(int waitTimeout = 5000, int receiveTimeout = 1000)
        {
            this.waitTimeout = waitTimeout;

            udpClient = new UdpClient()
            {
                Client =
                {
                    EnableBroadcast = true,
                    ReceiveTimeout = receiveTimeout
                },
                EnableBroadcast = true
            };

            dataSourcesTable = new DataTable("DataSources")
            {
                Columns =
                {
                    new DataColumn("ServerName", typeof(string)),
                    new DataColumn("InstanceName", typeof(string)),
                    new DataColumn("IsClustered", typeof(string)),
                    new DataColumn("Version", typeof(string))
                },
                CaseSensitive = false
            };
        }

        /// <summary>
        /// Retrieves raw string segment responses from the responding servers.
        /// </summary>
        /// <returns>A semi-color delimited key-value pair. Each server entry within the same server browser is delimited with two semi-colons.</returns>
        private async IAsyncEnumerable<string> GetServersStringsAsyncInternal()
        {
            await udpClient.SendAsync(new byte[1] { 0x02 }, 1, new IPEndPoint(IPAddress.Broadcast, SQL_SERVER_BROWSER_UDP_PORT));

            using (CancellationTokenSource cts = new CancellationTokenSource(waitTimeout))
            {
                Task<UdpReceiveResult> udpReceiveResult = null;
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        udpReceiveResult = udpClient.ReceiveAsync();
                        udpReceiveResult.Wait(udpClient.Client.ReceiveTimeout);
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.TimedOut)
                            throw;
                    }

                    if (udpReceiveResult != null && udpReceiveResult.IsCompleted && udpReceiveResult.Result.Buffer[0] == 0x05)
                    {
                        ushort byteCount = BitConverter.ToUInt16(udpReceiveResult.Result.Buffer, 1);
                        yield return Encoding.UTF8.GetString(udpReceiveResult.Result.Buffer, 3, byteCount);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves an Asynchronous Enumerable of SqlInstance records.
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<SqlInstance> GetServersAsync()
        {
            await foreach (string servers in GetServersStringsAsyncInternal())
            {
                string[] kvp;
                foreach (string server in servers.Split(new string[] { ";;" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    kvp = server.Split(';').Where((string s, int i) => i % 2 == 1).ToArray();
                    yield return new SqlInstance()
                    {
                        ServerName = kvp[0],
                        InstanceName = kvp[1],
                        IsClustered = string.Equals(kvp[2], "Yes", StringComparison.InvariantCultureIgnoreCase),
                        Version = kvp[3],
                    };
                }
            }
        }

        /// <summary>
        /// Retrieves a DataTable containing a series of responding servers.
        /// </summary>
        /// <returns></returns>
        public async Task<DataTable> GetDataSourcesAsync()
        {
            DataTable dataTable = dataSourcesTable.Copy();

            await foreach (string servers in GetServersStringsAsyncInternal())
                foreach (string server in servers.Split(new string[] { ";;" }, StringSplitOptions.RemoveEmptyEntries))
                    dataTable.LoadDataRow(server.Split(';').Where((string s, int i) => i % 2 == 1).ToArray(), true);

            return dataTable;
        }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    udpClient?.Close();
                    udpClient?.Dispose();

                    dataSourcesTable?.Clear();
                    dataSourcesTable?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~SSRPClient()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
