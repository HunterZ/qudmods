using System.Collections.Generic; // List

namespace HunterZ.HZMapPinToggle
{
  // custom player object "part" to handle events
  [System.Serializable]
  public class HZMapPinTogglePart : XRL.World.IPart
  {
    // custom event name for ability activation
    private static readonly string EVENT_NAME = "HZMapPinToggleEvent";

    // list of supported toggle actions for chosen location category
    private static readonly string[] TOGGLE_ACTIONS = new string[]
    {
      "Unvisited {{R|OFF}}, visited {{R|OFF}} (ALL off)",           // 0
      "Unvisited {{R|OFF}}, visited {{K|N/A}} (unvisited off)",     // 1
      "Unvisited {{R|OFF}}, visited {{G| ON}} (visited on ONLY)",   // 2
      "Unvisited {{K|N/A}}, visited {{R|OFF}} (visited off)",       // 3
      "Unvisited {{K|N/A}}, visited {{G| ON}} (visited on)",        // 4
      "Unvisited {{G| ON}}, visited {{R|OFF}} (unvisited on ONLY)", // 5
      "Unvisited {{G| ON}}, visited {{K|N/A}} (unvisited on)",      // 6
      "Unvisited {{G| ON}}, visited {{G| ON}} (ALL on)",            // 7
    };

    // toggle action enum
    private enum TrackAction { OF, NA, ON };

    // category toggle behavior enum
    private enum CategoryBehavior { ALL, NONE, SYNC };

    private static CategoryBehavior GetCategoryBehavior()
    {
      switch (XRL.UI.Options.GetOption("HZMapPinToggleOptionId", "AlwaysEnable"))
      {
        case "AlwaysEnable":   return CategoryBehavior.ALL;
        case "None":           return CategoryBehavior.NONE;
        case "SyncToContents": return CategoryBehavior.SYNC;
      }

      return CategoryBehavior.ALL;
    }

    private static bool VisitedNote(Qud.API.JournalMapNote mapNote)
    {
      System.Collections.Generic.Dictionary<string, long> visitedTime = XRL.Core.XRLCore.Core?.Game?.ZoneManager?.VisitedTime;
      return (
        mapNote != null &&
        visitedTime != null &&
        visitedTime.ContainsKey(mapNote.ZoneID)
      );
    }

    private static (TrackAction, TrackAction) GetActions(int actChoice)
    {
      return actChoice switch
      {
        0 => (TrackAction.OF, TrackAction.OF),
        1 => (TrackAction.OF, TrackAction.NA),
        2 => (TrackAction.OF, TrackAction.ON),
        3 => (TrackAction.NA, TrackAction.OF),
        4 => (TrackAction.NA, TrackAction.ON),
        5 => (TrackAction.ON, TrackAction.OF),
        6 => (TrackAction.ON, TrackAction.NA),
        7 => (TrackAction.ON, TrackAction.ON),
        _ => (TrackAction.NA, TrackAction.NA),
      };
    }

    private static void ApplyActions(int actChoice, int catChoice, string catName)
    {
      // derive action flags from player choice
      var (actUnv, actVis) = GetActions(actChoice);
      // iterate over all revealed map notes
      foreach (Qud.API.JournalMapNote mapNote in
               Qud.API.JournalAPI.GetMapNotes(
                (Qud.API.JournalMapNote item) =>
                  item.Revealed &&
                  (catChoice == 0 ||
                   item.Category == catName)
                )
              )
      {
        // apply chosen action(s)
        switch (VisitedNote(mapNote) ? actVis : actUnv)
        {
          case TrackAction.OF: mapNote.Tracked = false; break;
          case TrackAction.NA: /* do nothing */         break;
          case TrackAction.ON: mapNote.Tracked = true;  break;
        }
      }
    }

    public static void AddAbility(XRL.World.GameObject player)
    {
      // abort if ActivatedAbilities part is not found
      if (player.GetPart("ActivatedAbilities") is not XRL.World.Parts.ActivatedAbilities aaPart) { return; }
      // check whether ability already exists
      XRL.World.Parts.ActivatedAbilityEntry ability = aaPart.GetAbilityByCommand(EVENT_NAME);
      if (ability == null)
      {
        // ability does not yet exist; add it
        _ = aaPart.AddAbility(
          Name:             "Toggle Map Pins",
          Command:          EVENT_NAME,
          Class:            "Location Management",
          Description:      "Open menu of map pin bulk toggle functions",
          IsWorldMapUsable: true,
          Silent:           true
        );
      }
      else
      {
        // ability already exists; make sure it's usable on world map (mainly in
        //  case of old save - can probably be removed in some future version)
        ability.IsWorldMapUsable = true;
      }
    }

    // register for custom event callbacks
    public override void Register(XRL.World.GameObject Object, XRL.IEventRegistrar Registrar)
    {
      Registrar.Register(EVENT_NAME);
      base.Register(Object, Registrar);
    }

    public override bool FireEvent(XRL.World.Event E)
    {
      // bail out if not the event we want
      if (E.ID != EVENT_NAME) { return base.FireEvent(E); }

      // get list of known location categories
      System.Collections.Generic.List<string> categories = Qud.API.JournalAPI.GetMapNoteCategories();
      // if no locations known, pop up an informational message and abort
      if (categories.Count <= 0)
      {
        XRL.UI.Popup.ShowSpace("No location categories known yet. Go forth and explore!");
        return base.FireEvent(E);
      }
      // build list of category choices
      string[] catArray = new string[categories.Count + 1];
      catArray[0] = "All Known Categories";
      categories.CopyTo(catArray, 1);
      // query the user for category choice
      int catChoice = XRL.UI.Popup.ShowOptionList(
        Title:       "Map Pin Toggle - Location",
        Options:     catArray,
        Intro:       "Which location category's map pins would you like to toggle?\n",
        AllowEscape: true
      );
      // abort if player escaped out
      if (catChoice < 0) { return base.FireEvent(E); }
      string catName = catArray[catChoice];
      // query the user for toggle action choice
      int actChoice = XRL.UI.Popup.ShowOptionList(
        Title:       "Map Pin Toggle - Action",
        Options:     TOGGLE_ACTIONS,
        Intro:       "What action would you like to perform for " +
                      (catChoice == 0 ? " all known categories" : catName) +
                      "?\n",
        AllowEscape: true
      );
      // abort if player escaped out
      if (actChoice < 0) { return base.FireEvent(E); }

      // player has committed to a change - apply it
      ApplyActions(actChoice, catChoice, catName);

      XRL.World.Zone z = XRL.Core.XRLCore.Core?.Game?.Player?.Body?.CurrentCell?.ParentZone;
      if (z != null && z.IsWorldMap())
      {
        z.Activated();
      }

      // toggle categories per configured behavior
      switch (GetCategoryBehavior())
      {
        case CategoryBehavior.ALL:
        {
          // enable all categories since we're managing individual entries
          // otherwise disabled categories may override visibility
          foreach (string catString in categories)
          {
            Qud.API.JournalAPI.SetCategoryMapNoteToggle(catString, true);
          }
        }
        break;

        case CategoryBehavior.NONE:
        {
          // do nothing
        }
        break;

        case CategoryBehavior.SYNC:
        {
          // add categories with tracked notes to hash set
          System.Collections.Generic.HashSet<string> catSet = new();
          foreach (Qud.API.JournalMapNote mapNote in Qud.API.JournalAPI.GetMapNotes(
                   (Qud.API.JournalMapNote item) => item.Revealed && item.Tracked))
          {
            catSet.Add(mapNote.Category);
          }
          // now iterate over all categories and toggle based on set presence
          foreach (string catString in categories)
          {
            Qud.API.JournalAPI.SetCategoryMapNoteToggle(catString, catSet.Contains(catString));
          }
        }
        break;
      }

      return base.FireEvent(E);
    }
  }

  // mutator class to add "part" to player on new game
  [XRL.PlayerMutator]
  public class GameStartHander : XRL.IPlayerMutator
  {
    // called whenever starting a new game
    public void mutate(XRL.World.GameObject player)
    {
      // add part unconditionally
      player.AddPart<HZMapPinTogglePart>();
      // also set up ability
      HZMapPinTogglePart.AddAbility(player);
    }
  }

  // game load handler class to add "part" to player on save load
  [XRL.HasCallAfterGameLoaded]
  public static class GameLoadHander
  {
    // called whenever loading a save game
    [XRL.CallAfterGameLoaded]
    public static void GameLoadHandler()
    {
      XRL.World.GameObject player = XRL.Core.XRLCore.Core?.Game?.Player?.Body;
      if (player == null) { return; }
      // add part only if it's not already present
      player.RequirePart<HZMapPinTogglePart>();
      // also set up ability if needed
      HZMapPinTogglePart.AddAbility(player);
    }
  }
}
