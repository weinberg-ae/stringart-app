using UnityEngine;

// Upgrades earned in the exam (a final grade of 90 or more): the player picks one new pressing item.
//  • Silicone iron shoe: protects the fabric — more time before a scorch, a too-hot setting hurts much less.
//  • Automatic water filling: a water line keeps the station's tank between the marks.
//  • Wooden clapper: a new tool on the board (flattens seams, absorbs steam, dries the fabric).
public static class PM_Rewards
{
    public const int SiliconeShoe = 1, AutoFill = 2, Clapper = 4;
    public static readonly int[] All = { SiliconeShoe, AutoFill, Clapper };

    public static int Unlocked { get { return PlayerPrefs.GetInt("PM_Rewards", 0); } }
    public static bool Has(int r) { return (Unlocked & r) != 0; }
    public static void Unlock(int r) { PlayerPrefs.SetInt("PM_Rewards", Unlocked | r); PlayerPrefs.Save(); }

    // Silicone shoe: longer time before a scorch mark, much less damage from one step too hot.
    public static float DwellFactor { get { return Has(SiliconeShoe) ? 2.5f : 1f; } }
    public static float TooHotFactor { get { return Has(SiliconeShoe) ? 0.3f : 1f; } }

    static GameObject shoe, line;

    // Visible upgrades in the scene.
    public static void Apply(PM_Station station)
    {
        if (station == null) return;
        station.ShowClapper(Has(Clapper));
        if (Has(SiliconeShoe) && shoe == null && station.Iron != null) shoe = station.Iron.AddSiliconeShoe();
        if (Has(AutoFill) && line == null && station.Tank != null)
        {
            // Water line from the tank down to the floor and back to the wall.
            Vector3 a = station.Tank.transform.position;
            Vector3 floor = new Vector3(a.x, station.PlayerPos.y + 0.02f, a.z);
            Vector3 wall = floor + station.Forward * 0.9f;
            line = new GameObject("PM_AutoFillLine");
            PM_Util.Line(line.transform, "Pipe", new[] { a, floor, wall, wall + Vector3.up * 0.5f }, new Color(0.3f, 0.75f, 1f), 0.018f, false);
        }
    }

    // Back to no rewards (the reset button by the mirror): forget them and take their items out of the scene.
    public static void ResetAll(PM_Station station)
    {
        PlayerPrefs.DeleteKey("PM_Rewards");
        PlayerPrefs.Save();
        if (shoe != null) { Object.Destroy(shoe); shoe = null; }
        if (line != null) { Object.Destroy(line); line = null; }
        if (station != null)
        {
            station.ShowClapper(false);
            if (station.Iron != null) station.Iron.soleExtra = 0f;
        }
    }

    // Automatic filling: keeps the tank between MIN and MAX.
    public static void Tick(PM_Station station, float dt)
    {
        if (!Has(AutoFill) || station == null || station.Tank == null) return;
        if (station.Tank.Level < 0.45f) station.Tank.Level = Mathf.MoveTowards(station.Tank.Level, 0.62f, dt * 0.06f);
    }
}
