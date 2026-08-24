using System.Net;
using System.Security.Principal;

namespace UtilLib
{
    public class Util
    {
        static public bool IsRemoteConnection(iphlpapi.MIB_TCPROW_OWNER_PID connection)
        {
            if (IPAddress.IsLoopback(connection.LocalAddress) || IPAddress.IsLoopback(connection.RemoteAddress))
                return false;
            if (connection.dwLocalAddr == 0 || connection.dwRemoteAddr == 0)
                return false;

            return true;
        }
        static public bool IsRemoteConnection(iphlpapi.MIB_TCP6ROW_OWNER_PID connection)
        {
            if (IPAddress.IsLoopback(connection.LocalAddress) || IPAddress.IsLoopback(connection.RemoteAddress))
                return false;
            if (IsAllZero(connection.ucLocalAddr) || IsAllZero(connection.ucRemoteAddr))
                return false;

            return true;
        }
        static bool IsAllZero(byte[] bytes)
        {
            if (bytes == null)
                return true;
            foreach (var b in bytes)
                if (b != 0)
                    return false;
            return true;
        }
        static public bool IsElevated()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
