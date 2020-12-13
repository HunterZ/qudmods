using HarmonyLib;
using UnityEngine; // Color, Mathf
using XRL.World;   // Calendar, Cell, etc.

namespace HunterZ.HZDynBack
{
  public class BackgroundColorUtil
  {
    // private class API

    // default in vanilla display.txt is 15,59,58
    private static readonly Color defaultColor = new Color(0.059f, 0.231f, 0.227f, 1f);

    // brightness of default color
    // this MUST be defined after defaultColor, or else it will end up as zero!
    private static readonly float defaultColorBrightness = GetBrightness(defaultColor);

    // last recorded player depth
    private static int lastDepth = -1;

    // last color set by SetBackgroundColor(c, true), or by transition end
    private static Color lastFinalColor = defaultColor;

    // last recorded Joppa-versus-thinworld state
    private static bool lastJoppa = true;

    // last recorded game turn
    private static int lastTurn = -1;

    // stopwatch for timing the smooth transition change between desired colors
    // if stopwatch is active, a transition is in progress
    // stopwatch elapsed time is used only indirectly to protect from jumps
    private static readonly System.Diagnostics.Stopwatch stopwatch =
      new System.Diagnostics.Stopwatch();

    // last processed stopwatch time
    private static long stopwatchLastMilliseconds = 0;

    // color for each sundial index
    // brightness replaced with some fraction of the default background color's
    // note that brightness should never be lower than dungeon level 1
    private static readonly Color[] sundialColor = new[]
    {
      // 0 => dawn (turns 325 to 421, duration 97) [Calendar.startOfDay / 10]
      OverrideBrightness(
        new Color(0.063f, 0.235f, 0.224f, 1f), //  16  60  57
        0.875f * defaultColorBrightness),
      // 1 => sunrise (turns 422 to 517, duration 96)
      OverrideBrightness(
        new Color(0.247f, 0.647f, 0.733f, 1f), //  63 165 187
        0.875f * defaultColorBrightness),
      // 2 => morning (turns 518 to 614, duration 97)
      OverrideBrightness(
        new Color(0.475f, 0.753f, 0.808f, 1f), // 121 192 206
        1.000f * defaultColorBrightness),
      // 3 => midday (turns 615 to 710, duration 96)
      OverrideBrightness(
        new Color(0.475f, 0.753f, 0.808f, 1f), // 121 192 206
        1.000f * defaultColorBrightness),
      // 4 => afternoon (turns 711 to 807, duration 97)
      OverrideBrightness(
        new Color(0.475f, 0.753f, 0.808f, 1f), // 121 192 206
        1.000f * defaultColorBrightness),
      // 5 => sunset (turns 808 to 903, duration 96)
      OverrideBrightness(
        new Color(0.647f, 0.290f, 0.180f, 1f), // 165  74  46
        0.875f * defaultColorBrightness),
      // 6 => twilight (turns 904 to 999, duration 96)
      OverrideBrightness(
        new Color(0.224f, 0.157f, 0.310f, 1f), //  57  40  79
        0.875f * defaultColorBrightness),
      // 7 => evening (turns 1000 to 1174, duration 175) [Calendar.startOfNight / 10]
      OverrideBrightness(
        new Color(0.000f, 0.173f, 0.161f, 1f), //   0  44  41
        0.750f * defaultColorBrightness),
      // 8 => midnight (turns 1175 to 149, duration 175) [encompasses turn 0]
      OverrideBrightness(
        new Color(0.000f, 0.173f, 0.161f, 1f), //   0  44  41
        0.750f * defaultColorBrightness),        // darker due to longer duration
      // 9 => late (turns 150 to 324, duration 175)
      OverrideBrightness(
        new Color(0.000f, 0.173f, 0.161f, 1f), //   0  44  41
        0.750f * defaultColorBrightness)
    };

    // turn number for each sundial index
    private static readonly int[] sundialTurn = new[]
    {
       325,
       422,
       518,
       615,
       711,
       808,
       904,
      1000,
      1175,
       150
    };

    // smooth color transition target
    private static Color targetColor = defaultColor;

    // last transitional color set
    private static Color transitionColor = defaultColor;

    // elapsed transition time
    private static long transitionElapsedMilliseconds = -1;

    // length of smooth color transitions
    private static readonly long transitionTotalMilliseconds = 1000;

    // return brightness (V component of HSV) of given RGB color
    private static float GetBrightness(Color c)
    {
      Color.RGBToHSV(c, out _, out _, out float v);
      return v;
    }

    // get player's current depth
    // 10 => surface
    // <10 => above surface?
    // >10 => underground
    // returns 10 if depth cannot be determined for some reason
    private static int GetPlayerDepth()
    {
      int? depthField = XRL.Core.XRLCore.Core?.Game?.Player?.Body?.CurrentZone?.Z;
      return depthField == null ? 10 : depthField.Value;
    }

    // get sundial index for given turn number
    // this is basically a replica of the game's status bar sundial sprite index logic
    // does not check world
    // returns -1 if input out of range
    private static int GetSundialIndex(int turnNumber)
    {
      if (turnNumber < 0 || turnNumber >= Calendar.turnsPerDay) { throw new System.Exception("HZDynBack::GetSundialIndex(): turnNumber=" + turnNumber.ToString() + " out of range"); }
      for (int i = 0; i < sundialTurn.Length; ++i)
      {
        int bound1 = sundialTurn[i];
        int bound2 = sundialTurn[(i + 1) % sundialTurn.Length];
        if (bound2 > bound1)
        {
          // normal case
          if (bound1 <= turnNumber && turnNumber < bound2)
          {
            return i;
          }
        }
        else
        {
          // this range encompasses a rollover
          if (bound1 <= turnNumber || turnNumber < bound2)
          {
            return i;
          }
        }
      }
      throw new System.Exception("HZDynBack::GetSundialIndex(): Failed to get sundial index for turnNumber=" + turnNumber.ToString());
      /* original logic derived from core game, for reference
      if (turnNumber < 0 || turnNumber > Calendar.turnsPerDay) { return -1; }
      int firstDayTurn = Calendar.startOfDay / 10;
      int firstNightTurn = Calendar.startOfNight / 10;
      // number of turns since dawn
      int turnsSinceDawn = (
        (turnNumber + Calendar.turnsPerDay - firstDayTurn)
        % Calendar.turnsPerDay
      );
      // number of daylight turns
      int dayLenTurns = firstNightTurn - firstDayTurn;
      // number of night turns
      int nightLenTurns = Calendar.turnsPerDay + firstDayTurn - firstNightTurn;
      // is it day or night?
      if (turnsSinceDawn < dayLenTurns)
      {
        // day
        // calculate index linearly in [0, 6] (can't reach 7 due to less-than)
        return turnsSinceDawn * 7 / dayLenTurns;
      }
      // night
      // number of turns since nightfall
      int turnsSinceNight = turnsSinceDawn - dayLenTurns;
      // calculate index linearly in [7, 10]
      return 7 + (turnsSinceNight * 3 / nightLenTurns);
      */
    }

    // return value in range [0, 1) representing progress between indices
    private static float GetSundialIndexDistance(int turnNumber)
    {
      if (turnNumber < 0 || turnNumber >= Calendar.turnsPerDay) { throw new System.Exception("HZDynBack::GetSundialIndexDistance(): turnNumber=" + turnNumber.ToString() + " out of range"); }
      // get current and next indexes
      int currentIndex = GetSundialIndex(turnNumber);
      int nextIndex = (currentIndex + 1) % sundialTurn.Length;
      // get last and next index turns
      int lastIndexTurn = sundialTurn[currentIndex];
      int nextIndexTurn = sundialTurn[nextIndex];
      // unwrap next index turn to be larger than other turn values if needed
      if (nextIndexTurn < lastIndexTurn) { nextIndexTurn += Calendar.turnsPerDay; }
      // calculate deltas relative to last index turn number
      int turnsSinceLastIndex = turnNumber - lastIndexTurn;
      int turnsBetweenIndices = nextIndexTurn - lastIndexTurn;
      // calculate ratio of deltas as float
      float distance = (float)turnsSinceLastIndex / turnsBetweenIndices;
      return distance;
    }

    // return true if player is in "Joppa" world
    private static bool IsInJoppaWorld()
    {
      bool? inJoppaWorld = XRL.Core.XRLCore.Core?.Game?.Player?.Body?.CurrentCell?.ParentZone?._ZoneID?.StartsWith("Joppa");
      return inJoppaWorld != null && inJoppaWorld.Value;
    }

    // multiply color's brightness level by given factor
    private static Color MultiplyBrightness(Color c, float b)
    {
      Color.RGBToHSV(c, out float h, out float s, out float v);
      return Color.HSVToRGB(h, s, v * b);
    }

    // override color's brightness with provided value between 0 and 1
    private static Color OverrideBrightness(Color c, float b)
    {
      Color.RGBToHSV(c, out float h, out float s, out _);
      return Color.HSVToRGB(h, s, b);
    }

    // set game background color to specified value
    private static void SetBackgroundColor(Color color, bool finalColor)
    {
      // if this is a final (i.e., non-transitional) update, update states
      if (finalColor)
      {
        lastFinalColor = color;
      }
      // remove old ColorToCharMap entry
      // we have to maintain this reverse mapping, or else the game crashes on thinworld entry
      _ = ConsoleLib.Console.ColorUtility.ColorToCharMap.Remove(
        ConsoleLib.Console.ColorUtility.ColorMap['k']
      );
      // add new color to all data structures
      ConsoleLib.Console.ColorUtility.ColorMap['k'] = color;
      ConsoleLib.Console.ColorUtility.ColorToCharMap.Add(color, 'k');
      ConsoleLib.Console.ColorUtility.usfColorMap[0] = color;
      // get reference to cell class' background color cache variable
      System.Reflection.FieldInfo ColorBlack =
        typeof(Cell).GetField(
          "ColorBlack",
          System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
        );
      if (ColorBlack != null) { ColorBlack.SetValue(null, color); }
    }

    // set nextColor to desired background color based on current game state
    // does not check game world
    private static void UpdateTargetColor()
    {
      // optimization: check whether something significant happened
      int  depth        = GetPlayerDepth();
      bool inJoppaWorld = IsInJoppaWorld();
      int  turn         = Calendar.CurrentDaySegment / 10;
      if (depth        == lastDepth &&
          inJoppaWorld == lastJoppa &&
          turn         == lastTurn  &&
          targetColor  != defaultColor)
      {
        // nope, abort
        return;
      }

      // handle various states that have different background color behaviors
      if (!inJoppaWorld) // thinworld
      {
        // use default
        targetColor = defaultColor;
      }
      else if (depth < 10) // on the world map
      {
        // use default
        // this is for readability but also because I can't seem to fix jumping
        targetColor = defaultColor;
      }
      else if (depth >= 11) // underground
      {
        // optimization: only recalculate if depth changed
        // ...or if at default color (e.g. due to death prompt)
        if (depth != lastDepth ||
            targetColor == defaultColor)
        {
          // multiply V by value between 0 and 1 that diminishes with depth
          targetColor = MultiplyBrightness(defaultColor, 1f / Mathf.Sqrt(depth - 9));
        }
      }
      else // ground level
      {
        // populate turnList if needed
        int curIndex  = GetSundialIndex(turn);
        int nextIndex = (curIndex + 1) % sundialTurn.Length;
        // interpolate color based on current turn's distance between indexes
        targetColor = Color.Lerp(
          sundialColor[curIndex],
          sundialColor[nextIndex],
          GetSundialIndexDistance(turn)
        );
      }

      // update significance trackers
      lastDepth = depth;
      lastJoppa = inJoppaWorld;
      lastTurn  = turn;
    }

    // public class-specific API

    // public interface to reset background color
    public static void ResetBackgroundColor()
    {
      stopwatch.Reset();
      targetColor = defaultColor;
      SetBackgroundColor(defaultColor, true);
    }

    // primary public interface to perform automatic background color updates
    // implements smooth real-time transitions between colors
    public static void Update()
    {
      // handle case that a transition is in progress
      if (stopwatch.IsRunning)
      {
        long deltaMs = stopwatch.ElapsedMilliseconds - stopwatchLastMilliseconds;
        // protect from large jumps by only updating times for small ones
        if (deltaMs < 100)
        {
          transitionElapsedMilliseconds += deltaMs;
        }
        stopwatchLastMilliseconds = stopwatch.ElapsedMilliseconds;
        // if total time has elapsed, stop and finalize the target color
        if (transitionElapsedMilliseconds >= transitionTotalMilliseconds)
        {
          stopwatch.Reset();
          SetBackgroundColor(targetColor, true);
        }
        else
        {
          // stopwatch not elapsed; interpolate non-final color based on time
          transitionColor = Color.Lerp(
            lastFinalColor, targetColor,
            (float)transitionElapsedMilliseconds / transitionTotalMilliseconds
          );
          SetBackgroundColor(transitionColor, false);
        }
      }

      // update target color based on game state
      Color targetColorOld = targetColor;
      UpdateTargetColor();

      // handle potential new target color states
      if (stopwatch.IsRunning)
      {
        // transition already in progress
        if (targetColor != targetColorOld)
        {
          // target changed
          // lock in current transition color as new starting point
          SetBackgroundColor(transitionColor, true);
          // restart timer
          stopwatch.Reset();
          stopwatch.Start();
          stopwatchLastMilliseconds = 0;
          transitionElapsedMilliseconds = 0;
        }
        // else allow transition to continue as-is
      }
      else if (targetColor != lastFinalColor)
      {
        // we're not at target color, but transition not started
        // start timer
        stopwatch.Reset();
        stopwatch.Start();
        stopwatchLastMilliseconds = 0;
        transitionElapsedMilliseconds = 0;
      }
    }
  }

  // custom player object "part"
  // this is now vestigial, existing only for compatibility with old saves
  [System.Serializable]
  public class HZDynBackPart : IPart
  {
  }

  // prefix the game summary/scores screen logic with a background color reset
  // this is really only needed for transit to thinworld, as Patch2 already
  //  handles regular game end conditions
  [HarmonyPatch(typeof(XRL.Core.XRLCore), "BuildScore")]
  public class PatchBuildScore
  {
    public static bool Prefix()
    {
      // set default background color
      BackgroundColorUtil.ResetBackgroundColor();

      // allow the original implementation to execute
      return true;
    }
  }

  // set default background color on game end
  [HarmonyPatch(typeof(XRL.Messages.MessageQueue), "QueueUnityMessage")]
  public class PatchGameEnd
  {
    public static bool Prefix(string Message)
    {
      // set default background color if message is "!clear"
      // this seems to get passed in on game end
      if (Message == "!clear")
      {
        BackgroundColorUtil.ResetBackgroundColor();
      }

      // allow the original implementation to execute
      return true;
    }
  }

  // stupid fix for loading old saves that used bad namespace
  [HarmonyPatch(typeof(XRL.ModManager), "ResolveType")]
  public class PatchResolveType
  {
    public static bool Prefix(ref string TypeID)
    {
      // if the OLD part class path turns up, replace it with the current one
      if (TypeID == "XRL.World.HZDynBackPart")
      {
        TypeID = "HunterZ.HZDynBack.HZDynBackPart";
      }
      // allow original implementation to run
      return true;
    }
  }

  // prefix zone render calls with background color update logic
  [HarmonyPatch(typeof(XRL.World.Zone), "Render", new System.Type[]
  {
    typeof(ConsoleLib.Console.ScreenBuffer),
    typeof(int),
    typeof(int),
    typeof(int),
    typeof(int),
    typeof(int),
    typeof(int)
  })]
  public class PatchZoneRender
  {
    public static bool Prefix()
    {
      BackgroundColorUtil.Update();
      return true;
    }
  }
}
