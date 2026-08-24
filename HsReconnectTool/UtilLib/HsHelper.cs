using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UtilLib
{
    public class HsHelper
    {
        static readonly HsHelper singletonInst = new HsHelper();

        Firewall firewall;
        bool isForceDisconnected = false;
        Random rnd = new Random();

        public static HsHelper Instance
        {
            get
            {
                return singletonInst;
            }
        }
        static Process[] ListHsProcesses()
        {
            return Process.GetProcessesByName(Constants.HsProcessName);
        }
        static List<iphlpapi.MIB_TCPROW_OWNER_PID> ListHsConnections(HashSet<uint> pids)
        {
            return iphlpapi.GetAllTCPConnections().Where(c => pids.Contains(c.ProcessId)).ToList();
        }
        static List<iphlpapi.MIB_TCP6ROW_OWNER_PID> ListHsConnections6(HashSet<uint> pids)
        {
            return iphlpapi.GetAllTCP6Connections().Where(c => pids.Contains(c.ProcessId)).ToList();
        }

        public HsState UpdateHsState()
        {
            Process[] processes = ListHsProcesses();
            var pids = new HashSet<uint>(processes.Select(p => (uint)p.Id));

            var connections = ListHsConnections(pids);
            var connections6 = ListHsConnections6(pids);
            var state = new HsState(processes, connections, connections6);

            if (state.IsRunning && firewall == null)
            {
                string binaryPath = state.BinaryPath;
                if (binaryPath != null)
                {
                    firewall = Firewall.TryCreate(binaryPath);
                    Logger.Log("Firewall instance created: {0} (binary: {1})", firewall != null, binaryPath);
                }
                else
                {
                    Logger.Log("Could not resolve Hearthstone binary path - firewall rule not created "
                        + "(run the tool as administrator so it can read the game process)");
                }
            }

            return state;
        }
        public bool IsConnectedToServer
        {
            get
            {
                if (isForceDisconnected)
                    return false;
                return UpdateHsState().IsConnectedToServer;
            }
        }

        void DisconnectViaFirewall()
        {
            isForceDisconnected = true;
            try
            {
                int min = SettingsFile.Default.DisconnectIntervalMin;
                int max = SettingsFile.Default.DisconnectIntervalMax;
                if (max <= min)
                    max = min + 1;
                int DisconnectTimeoutMs = rnd.Next(min * 1000, max * 1000);

                Logger.Log("Firewall block ON for {0} ms", DisconnectTimeoutMs);
                firewall.EnableRule();

                // Hold the block, and once per second also try to tear down the game's TCP
                // connections and log how many remote connections remain. The per-second count
                // shows whether the block is actually cutting traffic (count drops to 0) or the
                // connection is surviving (count stays > 0).
                int elapsed = 0;
                const int stepMs = 1000;
                while (elapsed < DisconnectTimeoutMs)
                {
                    CloseExistingTcpConnections();
                    HsState during = UpdateHsState();
                    int remote4 = during.Connections.Count(Util.IsRemoteConnection);
                    int remote6 = during.Connections6.Count(Util.IsRemoteConnection);
                    Logger.Log("During block t={0}ms: remote connections IPv4={1}, IPv6={2}", elapsed, remote4, remote6);

                    System.Threading.Thread.Sleep(stepMs);
                    elapsed += stepMs;
                }

                firewall.DisableRule();
                Logger.Log("Firewall block OFF");
            }
            finally
            {
                isForceDisconnected = false;
            }
        }
        int CloseExistingTcpConnections()
        {
            HsState state = UpdateHsState();
            int closed = 0, failed = 0;
            foreach (var c in state.Connections)
            {
                if (!Util.IsRemoteConnection(c))
                    continue;

                String error = iphlpapi.CloseRemoteIP(c.ToTcpRow());
                if (error == null)
                {
                    closed++;
                    Logger.Log("Closed TCP connection {0}", c);
                }
                else
                {
                    failed++;
                    Logger.Log("SetTcpEntry failed for {0}: {1}", c, error);
                }
            }
            int remote6 = state.Connections6.Count(Util.IsRemoteConnection);
            Logger.Log("TCP close summary: closed={0}, failed={1}, IPv6-remote(not closable)={2}", closed, failed, remote6);
            return closed;
        }
        void DisconnectViaTcpMessage()
        {
            int DisableButtonIntervalMs = 4000;

            HsState state = UpdateHsState();
            isForceDisconnected = true;
            try
            {
                int closed = 0;
                foreach (var c in state.Connections)
                {
                    if (!Util.IsRemoteConnection(c))
                        continue;

                    Logger.Log("Closing IPv4 connection {0}", c);
                    String error = iphlpapi.CloseRemoteIP(c.ToTcpRow());
                    if (null != error)
                    {
                        Logger.Log("Failed to close {0}: {1}", c, error);
                        MessageBox.Show(String.Format("Cannot close connection {0}\r\nError: {1}", c, error));
                    }
                    else
                    {
                        closed++;
                    }
                }

                int remote6 = state.Connections6.Count(Util.IsRemoteConnection);
                if (closed == 0)
                {
                    if (remote6 > 0)
                    {
                        Logger.Log("No IPv4 server connections closed, but {0} IPv6 connection(s) exist. "
                            + "SetTcpEntry cannot close IPv6 connections - the Windows Firewall method is required.", remote6);
                        MessageBox.Show(
                            "Hearthstone is connected over IPv6, which this fallback method cannot close.\r\n\r\n"
                            + "Make sure the Windows Firewall (Windows Defender Firewall) service is running so "
                            + "the tool can use its reliable firewall-based disconnect, then try again.",
                            "HsReconnectTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        Logger.Log("No server connections found to close (IPv4: {0}, IPv6: {1})",
                            state.Connections.Count, state.Connections6.Count);
                    }
                }

                System.Threading.Thread.Sleep(DisableButtonIntervalMs);
            }
            finally
            {
                isForceDisconnected = false;
            }
        }
        public void CloseConnectionsToServer()
        {
            Logger.Log("CloseConnectionsToServer requested");

            // Refresh state first so the firewall rule gets created if the game is running.
            HsState state = UpdateHsState();
            Logger.Log("HS running: {0}, processes: {1}, connections IPv4: {2}, IPv6: {3}, firewall: {4}",
                state.IsRunning, state.ProcessCount, state.Connections.Count, state.Connections6.Count,
                firewall != null ? "available" : "unavailable");

            if (!state.IsRunning)
            {
                Logger.Log("Hearthstone is not running - nothing to close");
                MessageBox.Show("Hearthstone is not running, so there is no connection to close.",
                    "HsReconnectTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (firewall != null)
            {
                Logger.Log("Using firewall disconnect method");
                Task.Factory.StartNew(() => RunSafely("DisconnectViaFirewall", DisconnectViaFirewall));
            }
            else
            {
                Logger.Log("Firewall unavailable - using SetTcpEntry fallback method");
                Task.Factory.StartNew(() => RunSafely("DisconnectViaTcpMessage", DisconnectViaTcpMessage));
            }
        }

        void RunSafely(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                isForceDisconnected = false;
                Logger.LogException(name, ex);
                MessageBox.Show(
                    String.Format("Failed to close the connection.\r\n\r\n{0}\r\n\r\nDetails were written to:\r\n{1}",
                        ex.Message, Logger.FilePath),
                    "HsReconnectTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
