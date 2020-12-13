using HarmonyLib;

namespace HunterZ.HZShowOwner
{
  [HarmonyPatch(typeof(XRL.UI.Look), "GenerateTooltipContent")]
  public class GenerateTooltipContentPatcher
  {
    public static bool Prefix(ref string __result, XRL.World.GameObject O)
    {
      XRL.World.Parts.Description part = O.GetPart<XRL.World.Parts.Description>();
      System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();
      // name line
      //  name
      _ = stringBuilder.Append(O.DisplayName);
      //  owner (optional)
      string owner = O.Owner;
      if (!string.IsNullOrEmpty(owner))
      {
        _ = stringBuilder.Append(" (").Append(owner).Append(")");
      }
      _ = stringBuilder.AppendLine();
      // reaction and/or faction line (optional)
      string feelingDescription = part.GetFeelingDescription();
      string primaryFaction = O.GetPrimaryFaction();
      string faction = (
          string.IsNullOrEmpty(primaryFaction) ?
            null :
            XRL.World.Factions.get(primaryFaction).DisplayName
      );
      if (!string.IsNullOrEmpty(feelingDescription) ||
          !string.IsNullOrEmpty(faction))
      {
        // reaction (optional)
        if (!string.IsNullOrEmpty(feelingDescription))
        {
          _ = stringBuilder.Append(feelingDescription);
          // add a space separator before faction if both are present
          if (!string.IsNullOrEmpty(faction))
          {
            _ = stringBuilder.Append(" ");
          }
        }
        // faction (optional)
        if (!string.IsNullOrEmpty(faction))
        {
          int factionValue = XRL.Core.XRLCore.Core.Game.PlayerReputation.get(primaryFaction);
          string factionValueString = "{{" + XRL.World.Reputation.getColor(factionValue) + "|" + factionValue.ToString() + "}}";
          _ = stringBuilder.Append("(").Append(factionValueString).Append(" ").Append(faction).Append(")");
        }
        _ = stringBuilder.AppendLine();
      }
      // assessment line
      //  health
      _ = stringBuilder.Append(XRL.Rules.Strings.WoundLevel(O));
      //  difficulty (optional)
      string difficultyDescription = part.GetDifficultyDescription();
      if (!string.IsNullOrEmpty(difficultyDescription))
      {
        _ = stringBuilder.Append(", ").Append(difficultyDescription);
      }
      _ = stringBuilder.AppendLine();
      // long description line(s)
      _ = stringBuilder.AppendLine();
      part.GetLongDescription(stringBuilder);
      // stringBuilder.AppendLine();
      __result = ConsoleLib.Console.Markup.Transform(stringBuilder.ToString());
      return false;
    }
  }
}
