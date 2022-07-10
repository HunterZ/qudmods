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
    private enum ActE { OF, NA, ON };

    // category toggle behavior enum
    private enum CatE { ALL, NONE, SYNC };

    private static CatE GetCategoryBehavior()
    {
      switch (XRL.UI.Options.GetOption("HZMapPinToggleOptionId", "AlwaysEnable"))
      {
        case "AlwaysEnable":   return CatE.ALL;
        case "None":           return CatE.NONE;
        case "SyncToContents": return CatE.SYNC;
      }

      return CatE.ALL;
    }

    private static bool VisitedNote(Qud.API.JournalMapNote mapNote)
    {
      System.Collections.Generic.Dictionary<string, long> visitedTime = XRL.Core.XRLCore.Core?.Game?.ZoneManager?.VisitedTime;
      return (
        mapNote != null &&
        visitedTime != null &&
        visitedTime.ContainsKey(mapNote.zoneid)
      );
    }

    public static void AddAbility(XRL.World.GameObject player)
    {
      // abort if ActivatedAbilities part is not found
      if (!(player.GetPart("ActivatedAbilities") is XRL.World.Parts.ActivatedAbilities aaPart)) { return; }
      // abort if custom ability already exists
      XRL.World.Parts.ActivatedAbilityEntry ability = aaPart.GetAbilityByCommand(EVENT_NAME);
      if (ability != null) { return; }

      // add custom ability
      /* System.Guid abilityId = */
      _ = aaPart.AddAbility(
        Name:        "Toggle Map Pins",
        Command:     EVENT_NAME,
        Class:       "Location Management",
        Description: "Open menu of map pin bulk toggle functions",
        Silent:      true
      );
    }

    // register for custom event callbacks
    public override void Register(XRL.World.GameObject obj)
    {
      obj.RegisterPartEvent(this, EVENT_NAME);
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
      // query the user for toggle action choice
      int actChoice = XRL.UI.Popup.ShowOptionList(
        Title:       "Map Pin Toggle - Action",
        Options:     TOGGLE_ACTIONS,
        Intro:       "What action would you like to perform for " +
                      (catChoice == 0 ?
                        " all known categories" :
                        catArray[catChoice]) +
                      "?\n",
        AllowEscape: true
      );
      // abort if player escaped out
      if (actChoice < 0) { return base.FireEvent(E); }
      // player has committed to a change
      // derive action flags from player choice
      //  -1 => disable
      //   0 => no change
      //   1 => enable
      ActE actUnv = ActE.NA;
      ActE actVis = ActE.NA;
      switch (actChoice)
      {
        case 0: { actUnv = ActE.OF; actVis = ActE.OF; } break;
        case 1: { actUnv = ActE.OF; actVis = ActE.NA; } break;
        case 2: { actUnv = ActE.OF; actVis = ActE.ON; } break;
        case 3: { actUnv = ActE.NA; actVis = ActE.OF; } break;
        case 4: { actUnv = ActE.NA; actVis = ActE.ON; } break;
        case 5: { actUnv = ActE.ON; actVis = ActE.OF; } break;
        case 6: { actUnv = ActE.ON; actVis = ActE.NA; } break;
        case 7: { actUnv = ActE.ON; actVis = ActE.ON; } break;
      }
      // iterate over all revealed map notes
      foreach (Qud.API.JournalMapNote mapNote in
               Qud.API.JournalAPI.GetMapNotes(
                (Qud.API.JournalMapNote item) =>
                  item.revealed &&
                  (catChoice == 0 ||
                   item.category == catArray[catChoice])
                )
              )
      {
        // apply chosen action(s)
        switch (VisitedNote(mapNote) ? actVis : actUnv)
        {
          case ActE.OF: mapNote.tracked = false; break;
          case ActE.NA: /* do nothing */         break;
          case ActE.ON: mapNote.tracked = true;  break;
        }
      }

      XRL.World.Zone z = XRL.Core.XRLCore.Core?.Game?.Player?.Body?.CurrentCell?.ParentZone;
      if (z != null && z.IsWorldMap())
      {
        z.Activated();
      }

      // toggle categories per configured behavior
      switch (GetCategoryBehavior())
      {
        case CatE.ALL:
        {
          // enable all categories since we're managing individual entries
          // otherwise disabled categories may override visibility
          foreach (string catString in categories)
          {
            Qud.API.JournalAPI.SetCategoryMapNoteToggle(catString, true);
          }
        }
        break;

        case CatE.NONE:
        {
          // do nothing
        }
        break;

        case CatE.SYNC:
        {
          // add categories with tracked notes to hash set
          System.Collections.Generic.HashSet<string> catSet = new System.Collections.Generic.HashSet<string>();
          foreach (Qud.API.JournalMapNote mapNote in Qud.API.JournalAPI.GetMapNotes(
                   (Qud.API.JournalMapNote item) => item.revealed && item.tracked))
          {
            catSet.Add(mapNote.category);
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
  public class GameLoadHander
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
