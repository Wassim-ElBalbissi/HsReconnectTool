using System;
using System.Linq;
using WindowsFirewallHelper;

namespace UtilLib
{
    public class Firewall
    {
        static readonly string RuleNameOut = "HsReconnectTool";
        static readonly string RuleNameIn = "HsReconnectTool (Inbound)";
        IFirewall inst;
        IFirewallRule ruleOut;
        IFirewallRule ruleIn;

        private Firewall(IFirewall _inst, IFirewallRule _ruleOut, IFirewallRule _ruleIn)
        {
            inst = _inst;
            ruleOut = _ruleOut;
            ruleIn = _ruleIn;
        }
        public void EnableRule()
        {
            Logger.Log("Turning firewall block ON");
            if (ruleOut != null) ruleOut.IsEnable = true;
            if (ruleIn != null) ruleIn.IsEnable = true;
        }
        public void DisableRule()
        {
            Logger.Log("Turning firewall block OFF");
            if (ruleOut != null) ruleOut.IsEnable = false;
            if (ruleIn != null) ruleIn.IsEnable = false;
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

                // Block both directions so the game both stops receiving server data (so it
                // notices the disconnect quickly) and cannot re-establish during the window.
                var ruleOut = RecreateRule(inst, RuleNameOut, FirewallDirection.Outbound, exePath);
                var ruleIn = RecreateRule(inst, RuleNameIn, FirewallDirection.Inbound, exePath);

                return new Firewall(inst, ruleOut, ruleIn);
            }
            catch (Exception ex)
            {
                Logger.LogException("Firewall.TryCreate", ex);
                return null;
            }
        }

        // Removes any existing rule with the given name and creates a fresh, disabled block
        // rule for all profiles. IFirewallRule.Profiles is read-only, so recreating is the
        // only way to guarantee the rule targets every profile and the correct program path.
        static IFirewallRule RecreateRule(IFirewall inst, string name, FirewallDirection direction, string path)
        {
            var existing = inst.Rules.FirstOrDefault(r => r.Name == name);
            if (existing != null)
            {
                try { inst.Rules.Remove(existing); }
                catch (Exception ex) { Logger.LogException("Firewall.RemoveExistingRule", ex); }
            }

            var rule = inst.CreateApplicationRule(
                FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                name, FirewallAction.Block, path);
            rule.Direction = direction;
            rule.IsEnable = false;
            inst.Rules.Add(rule);
            Logger.Log("Firewall rule '{0}' created ({1} block, all profiles) for {2}", name, direction, path);
            return rule;
        }
    }
}
