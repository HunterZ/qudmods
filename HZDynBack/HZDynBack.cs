using HarmonyLib;
using UnityEngine; // Color, Mathf
using XRL.World;   // Calendar, Cell, etc.

namespace HunterZ.HZDynBack
{
  // general public color constants and stateless utilities
  public static class HZColorUtil
  {
    // default in vanilla display.txt is 15,59,58
    public static readonly Color defaultColor = new Color(0.059f, 0.231f, 0.227f, 1f);

    // brightness of default color
    // this MUST be defined after defaultColor, or else it will end up as zero!
    public static readonly float defaultColorBrightness = GetBrightness(defaultColor);

    // return brightness (V component of HSV) of given RGB color
    public static float GetBrightness(Color c)
    {
      Color.RGBToHSV(c, out _, out _, out float v);
      return v;
    }

    // multiply color's brightness level by given factor
    public static Color MultiplyBrightness(Color c, float b)
    {
      Color.RGBToHSV(c, out float h, out float s, out float v);
      return Color.HSVToRGB(h, s, v * b);
    }

    // override color's brightness with provided value between 0 and 1
    public static Color OverrideBrightness(Color c, float b)
    {
      Color.RGBToHSV(c, out float h, out float s, out _);
      return Color.HSVToRGB(h, s, b);
    }
  }

  // static class for managing sundial (surface) colors
  public static class SundialColorManager
  {
    // private class API

    // color for each sundial index
    // brightness replaced with some fraction of the default background color's
    // note that brightness should never be lower than dungeon level 1
    private static readonly Color[] colorByIndex = new[]
    {
      // 0 => dawn (turns 325 to 421, duration 97) [Calendar.startOfDay / 10]
      HZColorUtil.OverrideBrightness(
        new Color(0.063f, 0.235f, 0.224f, 1f), //  16  60  57
        0.875f * HZColorUtil.defaultColorBrightness),
      // 1 => sunrise (turns 422 to 517, duration 96)
      HZColorUtil.OverrideBrightness(
        new Color(0.247f, 0.647f, 0.733f, 1f), //  63 165 187
        0.875f * HZColorUtil.defaultColorBrightness),
      // 2 => morning (turns 518 to 614, duration 97)
      HZColorUtil.OverrideBrightness(
        new Color(0.475f, 0.753f, 0.808f, 1f), // 121 192 206
        1.000f * HZColorUtil.defaultColorBrightness),
      // 3 => midday (turns 615 to 710, duration 96)
      HZColorUtil.OverrideBrightness(
        new Color(0.475f, 0.753f, 0.808f, 1f), // 121 192 206
        1.000f * HZColorUtil.defaultColorBrightness),
      // 4 => afternoon (turns 711 to 807, duration 97)
      HZColorUtil.OverrideBrightness(
        new Color(0.475f, 0.753f, 0.808f, 1f), // 121 192 206
        1.000f * HZColorUtil.defaultColorBrightness),
      // 5 => sunset (turns 808 to 903, duration 96)
      HZColorUtil.OverrideBrightness(
        new Color(0.647f, 0.290f, 0.180f, 1f), // 165  74  46
        0.875f * HZColorUtil.defaultColorBrightness),
      // 6 => twilight (turns 904 to 999, duration 96)
      HZColorUtil.OverrideBrightness(
        new Color(0.224f, 0.157f, 0.310f, 1f), //  57  40  79
        0.875f * HZColorUtil.defaultColorBrightness),
      // 7 => evening (turns 1000 to 1174, duration 175) [Calendar.startOfNight / 10]
      HZColorUtil.OverrideBrightness(
        new Color(0.000f, 0.173f, 0.161f, 1f), //   0  44  41
        0.750f * HZColorUtil.defaultColorBrightness),
      // 8 => midnight (turns 1175 to 149, duration 175) [encompasses turn 0]
      HZColorUtil.OverrideBrightness(
        new Color(0.000f, 0.173f, 0.161f, 1f), //   0  44  41
        0.750f * HZColorUtil.defaultColorBrightness),
      // 9 => late (turns 150 to 324, duration 175)
      HZColorUtil.OverrideBrightness(
        new Color(0.000f, 0.173f, 0.161f, 1f), //   0  44  41
        0.750f * HZColorUtil.defaultColorBrightness)
    };

    // interpolated sundial color by turn number
    private static Color[] colorByTurn = null;

    // turn number for each sundial index
    private static readonly int[] turnByIndex = new[]
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

    // get sundial index for given turn number
    // this is basically a replica of the game's status bar sundial sprite index logic
    // does not check world
    // returns -1 if input out of range
    private static int GetIndex(int turnNumber)
    {
      if (turnNumber < 0 || turnNumber >= Calendar.turnsPerDay) { throw new System.Exception("HZDynBack::GetSundialIndex(): turnNumber=" + turnNumber.ToString() + " out of range"); }
      for (int i = 0; i < turnByIndex.Length; ++i)
      {
        int bound1 = turnByIndex[i];
        int bound2 = turnByIndex[(i + 1) % turnByIndex.Length];
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
    private static float GetIndexDistance(int turnNumber)
    {
      if (turnNumber < 0 || turnNumber >= Calendar.turnsPerDay) { throw new System.Exception("HZDynBack::GetSundialIndexDistance(): turnNumber=" + turnNumber.ToString() + " out of range"); }
      // get current and next indexes
      int currentIndex = GetIndex(turnNumber);
      int nextIndex = (currentIndex + 1) % turnByIndex.Length;
      // get last and next index turns
      int lastIndexTurn = turnByIndex[currentIndex];
      int nextIndexTurn = turnByIndex[nextIndex];
      // unwrap next index turn to be larger than other turn values if needed
      if (nextIndexTurn < lastIndexTurn) { nextIndexTurn += Calendar.turnsPerDay; }
      // calculate deltas relative to last index turn number
      int turnsSinceLastIndex = turnNumber - lastIndexTurn;
      int turnsBetweenIndices = nextIndexTurn - lastIndexTurn;
      // calculate ratio of deltas as float
      float distance = (float)turnsSinceLastIndex / turnsBetweenIndices;
      return distance;
    }

    private static void PopulateColorByTurn()
    {
      // abort if already populated
      if (colorByTurn != null) { return; }
      // populate colorByTurn array
      colorByTurn = new Color[Calendar.turnsPerDay];
      for (int turn = 0; turn < Calendar.turnsPerDay; ++turn)
      {
        XRL.UI.Loading.SetLoadingStatus(
          XRL.World.Event.NewStringBuilder().Clear()
          .Append("Calculating surface background color for turn ")
          .Append(turn)
          .Append(" of ")
          .Append(Calendar.turnsPerDay)
          .Append("...")
          .ToString()
        );
        int curIndex  = GetIndex(turn);
        int nextIndex = (curIndex + 1) % turnByIndex.Length;
        // interpolate color based on current turn's distance between indexes
        colorByTurn[turn] = Color.Lerp(
          colorByIndex[curIndex],
          colorByIndex[nextIndex],
          GetIndexDistance(turn)
        );
      }
    }

    // public class API

    // return interpolated sundial color for given turn
    public static Color GetColor(int turnNumber)
    {
      if (turnNumber < 0 || turnNumber >= Calendar.turnsPerDay) { throw new System.Exception("HZDynBack::GetColorByTurn(): turnNumber=" + turnNumber.ToString() + " out of range"); }
      // populate sundialColorByTurn array if needed
      PopulateColorByTurn();
      return colorByTurn[turnNumber];
    }
  }

  // static class for managing background colors
  public static class BackgroundColorManager
  {
    // private class API

    // reference to property used by the game to determine the default
    //  background color to be painted
    private static Traverse defaultBackground = null;

    // last recorded player depth
    private static int lastDepth = -1;

    // last color set by SetBackgroundColor(c, true), or by transition end
    private static Color lastFinalColor = HZColorUtil.defaultColor;

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

    // smooth color transition target
    private static Color targetColor = HZColorUtil.defaultColor;

    // last transitional color set
    private static Color transitionColor = HZColorUtil.defaultColor;

    // elapsed transition time
    private static long transitionElapsedMilliseconds = -1;

    // length of smooth color transitions
    private static readonly long transitionTotalMilliseconds = 1000;

    // bool wrapper for UI "do smooth transitions" option
    private static bool DoSmoothTransition =>
      XRL.UI.Options.GetOption("HZDynBackOptionSmooth").EqualsNoCase("Yes");

    private static int GameTurn => Calendar.CurrentDaySegment / 10;

    // get player's current depth
    // 10 => surface
    // <10 => above surface?
    // >10 => underground
    // returns 10 if depth cannot be determined for some reason
    private static int PlayerDepth
    {
      get
      {
        int? depthField = XRL.Core.XRLCore.Core?.Game?.Player?.Body?.CurrentZone?.Z;
        return depthField == null ? 10 : depthField.Value;
      }
    }

    // return true if player is in "Joppa" world
    private static bool PlayerInJoppaWorld
    {
      get
      {
        bool? inJoppaWorld = XRL.Core.XRLCore.Core?.Game?.Player?.Body?.CurrentCell?.ParentZone?._ZoneID?.StartsWith("Joppa");
        return inJoppaWorld != null && inJoppaWorld.Value;
      }
    }

    // set game background color to specified value
    private static void SetBackgroundColor(Color color, bool finalColor)
    {
      // if this is a final (i.e., non-transitional) update, update states
      if (finalColor)
      {
        lastFinalColor = color;
      }

      if (defaultBackground == null && ConsoleLib.Console.ColorUtility.Colors != null)
      {
        defaultBackground = Traverse.Create(ConsoleLib.Console.ColorUtility.Colors)?.Property("DefaultBackground");
      }
      defaultBackground?.SetValue(color);
    }

    // set targetColor to desired background color based on current game state
    // does not check game world
    // returns whether targetColor actually changed
    private static bool UpdateTargetColor()
    {
      // optimization: check whether something significant happened
      int  depth        = PlayerDepth;
      bool inJoppaWorld = PlayerInJoppaWorld;
      int  turn         = GameTurn;
      if (depth        == lastDepth &&
          inJoppaWorld == lastJoppa &&
          turn         == lastTurn  &&
          targetColor  != HZColorUtil.defaultColor)
      {
        // nope, abort
        return false;
      }

      // save previous target color for reporting whether it got changed
      Color targetColorOld = targetColor;

      // handle various states that have different background color behaviors
      if (!inJoppaWorld) // thinworld
      {
        // use default
        targetColor = HZColorUtil.defaultColor;
      }
      else if (depth < 10) // on the world map
      {
        // use default
        // this is for readability but also because I can't seem to fix jumping
        targetColor = HZColorUtil.defaultColor;
      }
      else if (depth >= 11) // underground
      {
        // optimization: only recalculate if depth changed
        // ...or if at default color (e.g. due to death prompt)
        if (depth != lastDepth ||
            targetColor == HZColorUtil.defaultColor)
        {
          // multiply V by value between 0 and 1 that diminishes with depth
          targetColor = HZColorUtil.MultiplyBrightness(
            HZColorUtil.defaultColor, 1f / Mathf.Sqrt(depth - 9));
        }
      }
      else // ground level
      {
        targetColor = SundialColorManager.GetColor(turn);
      }

      // update significance trackers
      lastDepth = depth;
      lastJoppa = inJoppaWorld;
      lastTurn  = turn;

      return targetColor != targetColorOld;
    }

    // public class API

    // public interface to reset background color
    public static void ResetBackgroundColor()
    {
      stopwatch.Reset();
      targetColor = HZColorUtil.defaultColor;
      SetBackgroundColor(targetColor, true);
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
      bool targetColorChanged = UpdateTargetColor();

      // handle potential target color changes
      if (!DoSmoothTransition)
      {
        // not doing smooth transitions
        // stop the stopwatch if it's running (in case user toggled option)
        if (stopwatch.IsRunning) { stopwatch.Reset(); }
        // set new target color directly
        if (targetColor != lastFinalColor)
        {
          SetBackgroundColor(targetColor, true);
        }
        // abort
        return;
      }

      // doing smooth transitions
      if (stopwatch.IsRunning)
      {
        // transition already in progress
        if (targetColorChanged)
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

  // prefix the game summary/scores screen logic with a background color reset
  // this is really only needed for transit to thinworld, as PatchPlayerTurn
  //  already handles regular game end conditions
  [HarmonyPatch(typeof(XRL.Core.XRLCore), nameof(XRL.Core.XRLCore.BuildScore))]
  public static class PatchBuildScore
  {
    public static bool Prefix()
    {
      // set default background color
      BackgroundColorManager.ResetBackgroundColor();

      // allow the original implementation to execute
      return true;
    }
  }

  // set default background color on game end
  [HarmonyPatch(typeof(XRL.Core.XRLCore), nameof(XRL.Core.XRLCore.PlayerTurn))]
  public static class PatchPlayerTurn
  {
    public static void Postfix()
    {
      // set default background color if game is no longer running
      bool? running = XRL.Core.XRLCore.Core?.Game?.Running;
      if (running != null && !running.Value)
      {
        BackgroundColorManager.ResetBackgroundColor();
      }
    }
  }

  // prefix zone render calls with background color update logic
  [HarmonyPatch(typeof(Zone), nameof(Zone.Render), new System.Type[]
  {
    typeof(ConsoleLib.Console.ScreenBuffer),
    typeof(int),
    typeof(int),
    typeof(int),
    typeof(int)
  })]
  public static class PatchZoneRender
  {
    public static bool Prefix()
    {
      BackgroundColorManager.Update();
      return true;
    }
  }
}
