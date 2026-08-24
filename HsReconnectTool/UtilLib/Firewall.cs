using System;
using System.Linq;
using System.Windows.Forms;
using WindowsFirewallHelper;

namespace UtilLib
{
    public class Firewall
    {
        static readonly string RuleName = "HsReconnectTool";
        IFirewall inst;
        IFirewallRule rule;

        private Firewall(IFirewall _inst, IFirewallRule _rule)
        {
            inst = _inst;
            rule = _rule;
            Console.WriteLine("Firewall instance has been created");
        }
        public void EnableRule()
        {
            Console.WriteLine("Turning firewall rule On");
            rule.IsEnable = true;
        }
        public void DisableRule()
        {
            Console.WriteLine("Turning firewall rule Off");
            rule.IsEnable = false;
        }

        public static Firewall TryCreate(string exePath)
        {
            try
            {
                if (!FirewallManager.IsServiceRunning)
                {
                    Logger.Log("Windows Firewall service is not running - firewall disconnect unavailable");
                    return null;
                }

                IFirewall inst;
                if (!FirewallManager.TryGetInstance(out inst))
                {
                    Logger.Log("Could not get a Windows Firewall instance - firewall disconnect unavailable");
                    return null;
                }

                var rule = inst.Rules.FirstOrDefault(r => r.Name == RuleName);
                if (rule == null)
                {
                    rule = CreateRule(inst, exePath);
                    Logger.Log("Firewall rule has been created for {0}", exePath);
                }
                else
                {
                    Logger.Log("Firewall rule already exists");
                }

                return new Firewall(inst, rule);
            }
            catch (Exception ex)
            {
                Logger.LogException("Firewall.TryCreate", ex);
                return null;
            }
        }
        static IFirewallRule CreateRule(IFirewall inst, string path)
        {
            var rule = inst.CreateApplicationRule(RuleName, FirewallAction.Block, path);
            rule.Direction = FirewallDirection.Outbound;
            rule.IsEnable = false;
            inst.Rules.Add(rule);
            return rule;
        }

    }
}
