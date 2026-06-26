using ScavLib.command;
using ScavLib.util;

namespace ScavButt;

public class ButtCommand : BaseCommand
{
    public override string Name => "butt";
    public override string Description => "ScavButt — buttplug.io integration. Usage: butt <status>";

    public override void Execute(string[] args)
    {
        // args[0] is "butt", real subcommand is args[1]
        string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "status";

        switch (sub)
        {
            case "status":
                ExecuteStatus();
                break;
            default:
                GameUtil.Log($"[ScavButt] Unknown subcommand '{args[1]}'. Usage: butt status");
                break;
        }
    }

    private static void ExecuteStatus()
    {
        GameUtil.Log("[ScavButt] === ScavButt Status ===");
        GameUtil.Log($"[ScavButt] Version : {Plugin.PluginVersion}");
        // TODO: replace placeholders once ButtplugManager is implemented
        GameUtil.Log("[ScavButt] Intiface : not yet implemented");
        GameUtil.Log("[ScavButt] Devices  : not yet implemented");
    }
}
