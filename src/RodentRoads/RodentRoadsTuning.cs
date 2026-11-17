namespace RodentRoads
{
    /// <summary>
    /// Adventure tuning. Durations are in Sim minutes; the runtime converts them to behaviour
    /// ticks via <c>MinorPetTuning.BehaviorAlarmCallbackFrequency</c> (5 minutes).
    /// </summary>
    /// <remarks>TODO: move to an <c>_XML</c> tuning resource (data/PLAN.md §2.3).</remarks>
    public static class RodentRoadsTuning
    {
        /// <summary>Use the short <c>Test*</c> ranges. Set <c>false</c> for a release build.</summary>
        public static bool TestMode = true;

        /// <summary>Trip length range, in Sim minutes. Rolled per trip.</summary>
        public static int[] AwaySimMinutesRange = new int[] { 10, 60 };

        /// <summary>Time at home between trips, in Sim minutes. Rolled per return.</summary>
        public static int[] HomeSimMinutesRange = new int[] { 10, 60 };

        /// <summary>Test-mode trip length range, in Sim minutes.</summary>
        public static int[] TestAwaySimMinutesRange = new int[] { 5, 15 };

        /// <summary>Test-mode time at home range, in Sim minutes.</summary>
        public static int[] TestHomeSimMinutesRange = new int[] { 10, 20 };

        /// <summary>Trip-length range in effect (inclusive bounds).</summary>
        public static int[] GetAwaySimMinutesRange()
        {
            return TestMode ? TestAwaySimMinutesRange : AwaySimMinutesRange;
        }

        /// <summary>Time-at-home range in effect (inclusive bounds).</summary>
        public static int[] GetHomeSimMinutesRange()
        {
            return TestMode ? TestHomeSimMinutesRange : HomeSimMinutesRange;
        }
    }
}
