using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace UtilLib
{
    public class HsState
    {
        Process[] processes;
        List<iphlpapi.MIB_TCPROW_OWNER_PID> connections;
        List<iphlpapi.MIB_TCP6ROW_OWNER_PID> connections6;

        public HsState(Process[] _processes,
            List<iphlpapi.MIB_TCPROW_OWNER_PID> _connections,
            List<iphlpapi.MIB_TCP6ROW_OWNER_PID> _connections6)
        {
            processes = _processes;
            connections = _connections;
            connections6 = _connections6 ?? new List<iphlpapi.MIB_TCP6ROW_OWNER_PID>();
        }

        public bool IsRunning
        {
            get
            {
                return processes.Length > 0;
            }
        }
        public int ProcessCount
        {
            get
            {
                return processes.Length;
            }
        }
        public int ConnectionCount
        {
            get
            {
                return connections.Count + connections6.Count;
            }
        }
        public bool IsConnectedToServer
        {
            get
            {
                return RemoteConnectionCount > 0;
            }
        }
        int RemoteConnectionCount
        {
            get
            {
                return connections.Count(c => Util.IsRemoteConnection(c))
                     + connections6.Count(c => Util.IsRemoteConnection(c));
            }
        }
        public List<iphlpapi.MIB_TCPROW_OWNER_PID> Connections
        {
            get
            {
                return connections;
            }
        }
        public List<iphlpapi.MIB_TCP6ROW_OWNER_PID> Connections6
        {
            get
            {
                return connections6;
            }
        }
        public string BinaryPath
        {
            get
            {
                var first = processes.FirstOrDefault();
                if (first == null)
                    return null;
                try
                {
                    return first.MainModule.FileName;
                }
                catch (System.Exception ex)
                {
                    Logger.LogException("HsState.BinaryPath", ex);
                    return null;
                }
            }
        }
    }
}
