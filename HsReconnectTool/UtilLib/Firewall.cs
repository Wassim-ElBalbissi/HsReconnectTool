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

                // Remove any pre-existing rule and recreate it, so it always has the correct
                // program path, direction and (all) profiles. Older versions created the rule
                // for the Public profile only; IFirewallRule.Profiles is read-only, so the only
                // way to correct it is to recreate the rule.
                var existing = inst.Rules.FirstOrDefault(r => r.Name == RuleName);
                if (existing != null)
                {
                    try
                    {
                        inst.Rules.Remove(existing);
                        Logger.Log("Removed pre-existing firewall rule to recreate it with correct settings");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException("Firewall.RemoveExistingRule", ex);
                    }
                }

                var rule = CreateRule(inst, exePath);
                Logger.Log("Firewall rule created for {0} (all profiles, outbound block)", exePath);

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
            // Apply to every network profile so the block works regardless of whether the
            // active network is classified Public, Private or Domain.
            var rule = inst.CreateApplicationRule(
                FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                RuleName, FirewallAction.Block, path);
            rule.Direction = FirewallDirection.Outbound;
            rule.IsEnable = false;
            inst.Rules.Add(rule);
            return rule;
        }

    }
}
