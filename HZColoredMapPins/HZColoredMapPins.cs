using HarmonyLib;                     // Harmony injection
using Qud.API;                        // JournalAPI, JournalMapNote
using System;                         // DateTime, Type
using System.Collections;             // DictionaryEntry
using System.Collections.Generic;     // List
using System.Collections.Specialized; // OrderedDictionary
using System.Linq;                    // Any()
using XRL.UI;                         // JournalScreen
using XRL.World.Parts;                // TerrainNotes

namespace HunterZ.HZColoredMapPins
{
  // this is a static class to hold a dictionary of colors by location category
  //
  // each entry is an ordered 4 character string, with the following meanings
  //  for each character position:
  //   0 => map cell foreground, render frame 0
  //   1 => map cell foreground, render frame 1
  //   2 => map cell background & journal category
  //
  // an ordered dictionary is used so that items are stored in order of
  //  decreasing precedence, as a map cell may have multiple tracked notes
  //
  // defaultString is meant to be used as a default case when no matching value
  //  is present
  public class Colors
  {
    public static readonly string defaultString = "Gkg";

    public static readonly OrderedDictionary dict = new OrderedDictionary
    {
      {"Named Locations",           "kyY"},
      {"Artifacts",                 "YwW"},
      {"Historic Sites",            "YoO"},
      {"Settlements",               "YcC"},
      {"Ruins with Becoming Nooks", "YrR"},
      {"Legendary Creatures",       "YmM"},
      {"Lairs",                     "Mkm"},
      {"Ruins",                     "Rkr"},
      {"Merchants",                 "Wkw"},
      {"Oddities",                  "Ckc"},
      {"Baetyls",                   "YbB"},
      {"Natural Features",          "YgG"},
      {"Miscellaneous",             "Yky"}
    };
  }

  // use a postfix to tack logic onto the end of TerrainNotes::HandleEvent()
  [HarmonyPatch(typeof(TerrainNotes), "HandleEvent", new Type[] { typeof(XRL.World.ZoneActivatedEvent) })]
  public class Patch1
  {
    public static void Postfix(TerrainNotes __instance)
    {
      // remove any previous stashed value from parent object
      __instance.ParentObject.DeleteStringProperty("HZColorNotes");

      // base method already determined whether one or more tracked notes exist
      // abort if there aren't any
      if (!__instance.tracked)
      {
        return;
      }

      // default to "<DEFAULT>" color, because we know *something* is active
      string colorString = Colors.defaultString;
      // scan the list of notes in priority order to decide on a color
      foreach (DictionaryEntry entry in Colors.dict)
      {
        // skip this category if it's not applicable
        if (__instance.notes.Any(
            (JournalMapNote item) =>
            item.category == (string)entry.Key && item.tracked
           ))
        {
          // map cell has an active note of this category
          // grab it's color and stop looking
          colorString = (string)entry.Value;
          break;
        }
      }

      // stash the chosen colors in the parent GameObject
      __instance.ParentObject.SetStringProperty("HZColorNotes", colorString);
    }
  }

  // use a prefix to replace the default TerrainNotes::Render() method
  [HarmonyPatch(typeof(TerrainNotes), "Render")]
  public class Patch2
  {
    public static bool Prefix(TerrainNotes __instance, XRL.World.RenderEvent E, ref bool __result)
    {
      // set return value to true
      // this is a kludge because the original implementation does it by
      //  chaining to its base class, which we can't access from here?
      __result = true;

      // bail out unless one or more tracked notes exist
      if (!__instance.tracked)
      {
        // don't execute original implementation
        return false;
      }

      string colorString = __instance.ParentObject.GetStringProperty("HZColorNotes", Colors.defaultString);
      E.ColorString += "&" + colorString[DateTime.Now.Second & 1]
                    +  "^" + colorString[2];
      E.DetailColor = colorString[2].ToString();

      // don't execute original implementation
      return false;
    }
  }

  // use a postfix to tack logic onto the end of JournalScreen::UpdateEntries()
  [HarmonyPatch(typeof(JournalScreen), "UpdateEntries")]
  public class Patch3
  {
    public static void Postfix(JournalScreen __instance, ref List<string> ___displayLines, string selectedTab)
    {
      // bail out if the originating call didn't go down the desired path
      if (__instance.selectedCategory != null ||
          selectedTab != JournalScreen.STR_LOCATIONS)
      {
        return;
      }

      List<string> cats = JournalAPI.GetMapNoteCategories();
      // also bail out if there weren't any journal categories
      if (cats.Count == 0)
      {
        return;
      }

      // clear the previously-added lines so that we can replace them here
      ___displayLines.Clear();
      // now loop through and replace with new ones
      foreach (string cat in cats)
      {
        string item = "["
          + (JournalAPI.GetCategoryMapNoteToggle(cat) ? "{{G|X}}" : " ")
          + "] {{"
          + (Colors.dict.Contains(cat) ? ((string)Colors.dict[cat])[2] : Colors.defaultString[2])
          + "|" + cat + "}}"
        ;
        ___displayLines.Add(item);
      }
    }
  }
}
