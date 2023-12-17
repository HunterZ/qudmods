using HarmonyLib;

using System.Text;
using XRL.UI;
using XRL.World.Parts;

namespace HunterZ.HZShowOwner
{
  [HarmonyPatch(typeof(Description), nameof(Description.GetLongDescription), new System.Type[]
  {
    typeof(StringBuilder)
  })]
  static class Patch1
  {
    public static void Postfix(Description __instance, StringBuilder SB)
    {
      // note: have to use '\n' instead of AppendLine() because the latter
      //  results in music notes appearing in non-overlay mode for some reason
      SB.Append("\n");
      XRL.World.GameObject go = __instance.ParentObject;
      // faction (optional)
      string factionKey = go.GetPrimaryFaction();
      if (!string.IsNullOrEmpty(factionKey))
      {
        SB.Append("\nFaction: " + factionKey);
        XRL.World.Faction faction = XRL.World.Factions.GetIfExists(factionKey);
        if (faction != null)
        {
          SB.Append(" \"" + faction.DisplayName + "\"");
          XRL.World.Reputation reputation = XRL.Core.XRLCore.Core?.Game?.PlayerReputation;
          if (reputation != null)
          {
            int factionValue = reputation.Get(faction);
            SB.Append(" (" + factionValue.ToString() + ")");
          }
        }
      }
      // owner (optional)
      string owner = go.Owner;
      if (!string.IsNullOrEmpty(owner))
      {
        SB.Append("\nOwner: ").Append(owner);
      }
      // id (optional)
      if (Options.GetOption("HZShowOwnerOptionId").EqualsNoCase("Yes"))
      {
        string bp = go.Blueprint;
        if (string.IsNullOrEmpty(bp))
        {
          bp = "<null>";
        }
        string id = go.ID;
        if (string.IsNullOrEmpty(id))
        {
          id = "<null>";
        }
        SB.Append("\nBPID: ").Append(bp).Append("#").Append(id);
      }
    }
  }
}
