using System.Collections.Generic;
using UnityEngine;

// All game texts (Hebrew) and fabric data. Edit texts here.
public enum PM_Target { None, Power, Gauge, Iron, Boom, TempButtons, SleeveBoard, Board, Ham, PointPresser, Cloth, Fusible, Fabric, Rest }
public enum PM_Action { Next, Power, WaitPressure, GrabIron, SteamInAir, PressTemp, IronFabric, Explore }
public enum PM_HotspotGroup { None, Station, Tools, Fabric, Fibers }
public enum PM_Anchor { Top, Front, FrontBelow, IronFront, BoardLeft }
public enum PM_FabricType { Cotton, Linen, Wool, Silk, Polyester }
public enum PM_Steam { Required, Optional, Forbidden }
public enum PM_Weave { Plain, Slub, Twill, Satin, Smooth }

public class PM_Step
{
    public string id;
    public string title;
    public string body;
    public PM_Target target;
    public PM_Action action;
    public PM_FabricType fabric;
    public PM_HotspotGroup group = PM_HotspotGroup.None;
    public int toolTask;   // 1 = collar on the point presser, 2 = dress bodice on the ham

    public PM_Step(string id, string title, string body, PM_Target target, PM_Action action, PM_FabricType fabric = PM_FabricType.Cotton)
    {
        this.id = id; this.title = title; this.body = body; this.target = target; this.action = action; this.fabric = fabric;
    }
}

public class PM_FabricInfo
{
    public PM_FabricType type;
    public string name;
    public int mode;            // 1 = • (110°C), 2 = •• (150°C), 3 = ••• (200°C)
    public PM_Steam steam;
    public Color color;
    public float smoothness;
    public PM_Weave weave;
    public float ironSeconds;   // time of continuous ironing on one spot to smooth it
    public string cue;          // exam: how it looks
    public string burn;         // exam: burn test result
    public string fiberText, pressText, burnText, lookText;
    public string garment;      // name of the example garment model  // learning: texts of the three points of light
}

public class PM_HotspotInfo
{
    public string id, label, title, body;
    public PM_Target target;
    public PM_Anchor anchor;
    public PM_HotspotGroup group;
    public Color accent = new Color(0.1f, 0.95f, 1f);   // color of the point and of its card
    public bool isFiber;                                  // drawn as a floating fiber symbol
    public PM_FabricType fiber;

    public PM_HotspotInfo(string id, string label, string title, string body, PM_Target target, PM_Anchor anchor)
    {
        this.id = id; this.label = label; this.title = title; this.body = body; this.target = target; this.anchor = anchor;
    }
}

public static class PM_Content
{
    public const string BtnNext = "הבא";
    public const string BtnRepeat = "שמע שוב";
    public const string BtnLearn = "מסלול לימוד";
    public const string BtnExam = "מבחן";
    public const string BtnMenu = "תפריט";
    public const string BtnClose = "סגור";

    public static readonly string[] ModeDots = { "", "•", "••", "•••" };
    public static readonly string[] ModeTemp = { "", "110°C", "150°C", "200°C" };

    public const string MenuTitle = "עמדת גיהוץ מקצועית";
    public const string MenuBody =
        "ברוכים הבאים לסימולטור עמדת הגיהוץ המקצועית.\n" +
        "מסלול לימוד — היכרות צעד אחר צעד עם העמדה, כלי העזר וחמשת סוגי הבדים, עם הסברים וקריינות.\n" +
        "מבחן — זיהוי בדים וגיהוץ בזמן, בלי עזרה.\n" +
        "בחרו מסלול: כוונו את היד או את הקרן אל הכפתור ולחצו על ההדק.";

    // ---------- Status messages ----------
    public const string StNeedPower = "העמדה כבויה — הפעילו את המתג הראשי.";
    public const string StPressureLow = "הלחץ עדיין נמוך — המתינו שהמחוג יגיע ל-3.5 בר.";
    public const string StPressureOk = "הלחץ תקין — אפשר להתחיל לעבוד.";
    public const string StNoMode = "בחרו קודם טמפרטורה בלוח הבקרה.";
    public const string StTooCold = "חום נמוך מדי — הקמטים לא יוצאים. נדרש: {0}";
    public const string StTooHotLearn = "חום גבוה מדי לבד הזה! בבד אמיתי זה היה נשרף. נדרש: {0}";
    public const string StNeedSteam = "הוסיפו קיטור (הדק) — בלי לחות הקמטים יוצאים לאט.";
    public const string StNoSteam = "בלי קיטור! מים משאירים כתמים על {0}.";
    public const string StProgress = "התקדמות: {0}%";
    public const string StDone = "מצוין! הבד חלק.";
    public const string StBurned = "הבד נשרף! הטמפרטורה גבוהה מדי.";
    public const string StMelted = "הבד נמס! פוליאסטר רגיש לחום.";
    public const string StSpots = "כתמי מים על הבד!";
    public const string StTimeUp = "הזמן נגמר.";
    public const string StModeSet = "נבחר: {0} — עד {1}";
    public const string StGrabbed = "המגהץ ביד.";
    public const string StSteamAir = "פליטת קיטור: {0}%";
    public const string StTime = "זמן: {0} שניות";
    public const string StExplored = "נקודות שנלמדו: {0}/{1}";
    public const string StAllExplored = "כל הנקודות נלמדו — מצוין!";

    // ---------- Learning path (main window: only short tasks and critical rules) ----------
    public static List<PM_Step> LearningSteps()
    {
        var s = new List<PM_Step>();
        s.Add(new PM_Step("learn_intro", "מסלול לימוד",
            "על העמדה פזורות נקודות אור.\n" +
            "געו בנקודה ביד — או כוונו אליה את הקרן ולחצו על ההדק — ויופיע הסבר עם קריינות.\n" +
            "בחלון הזה יופיעו רק המשימות וכללי הבטיחות.\n" +
            "כדי להמשיך — לחצו על \"הבא\".",
            PM_Target.None, PM_Action.Next));

        s.Add(new PM_Step("power", "הפעלת העמדה",
            "סובבו את המתג הראשי המואר בצד ימין של לוח הבקרה.",
            PM_Target.Power, PM_Action.Power));

        var explore = new PM_Step("explore_station", "הכירו את העמדה",
            "געו בכל נקודות האור על העמדה כדי ללמוד על חלקיה.\n" +
            "בטיחות — חובה:\n" +
            "• לא מכוונים קיטור אל היד או אל הגוף.\n" +
            "• המגהץ מונח רק על משטח ההנחה, בשכיבה — לא מעמידים אותו על העקב.\n" +
            "• לא מתחילים לגהץ לפני שהלחץ מגיע ל-3.5 בר.",
            PM_Target.None, PM_Action.Explore);
        explore.group = PM_HotspotGroup.Station;
        s.Add(explore);

        s.Add(new PM_Step("iron", "הרמת המגהץ",
            "כוונו את היד אל המגהץ, לחצו על הכפתור הצדדי (Grip) והחזיקו.",
            PM_Target.Iron, PM_Action.GrabIron));

        s.Add(new PM_Step("purge", "פליטת קיטור ראשונית",
            "החזיקו את המגהץ באוויר, הרחק מהבד ומהגוף, ולחצו על ההדק במשך 2 שניות.\n" +
            "כך מנקזים מים שהתעבו בצינור.",
            PM_Target.Iron, PM_Action.SteamInAir));

        s.Add(new PM_Step("temp", "בחירת טמפרטורה",
            "לחצו על אחד מכפתורי הטמפרטורה בלוח הבקרה.\n" +
            "• — עד 110°C\n•• — עד 150°C\n••• — עד 200°C",
            PM_Target.TempButtons, PM_Action.PressTemp));

        var tools = new PM_Step("tools", "כלי עזר לגיהוץ",
            "על משטח הגיהוץ ארבעה כלי עזר: חמור (קבנצ'יק), מגהצון פינות, מטלית גיהוץ ושכבות דביקון.\n" +
            "געו בנקודות האור כדי ללמוד מתי ולמה משתמשים בכל אחד.",
            PM_Target.None, PM_Action.Explore);
        tools.group = PM_HotspotGroup.Tools;
        s.Add(tools);

        var fibers = new PM_Step("fibers", "חמשת סוגי הסיבים",
            "מעל העמדה מרחפים חמישה סיבים זוהרים. געו בכל סיב כדי ללמוד על מקורו ומבנהו.\n" +
            "• ורוד-אדום — צמחיים (תאית): כותנה, פשתן — חום גבוה •••\n" +
            "• צהוב — מן החי (חלבון): צמר, משי — חום בינוני ••\n" +
            "• כחול — כימיים: פוליאסטר — חום נמוך •",
            PM_Target.None, PM_Action.Explore);
        fibers.group = PM_HotspotGroup.Fibers;
        s.Add(fibers);

        foreach (PM_FabricType t in new[] { PM_FabricType.Cotton, PM_FabricType.Linen, PM_FabricType.Wool, PM_FabricType.Silk, PM_FabricType.Polyester })
        {
            PM_FabricInfo f = Fabric(t);
            var st = new PM_Step(t.ToString().ToLower(), f.name,
                "נדרש: " + ModeLabel(f.mode) + " — " + SteamLabel(f.steam) + "\n" +
                "געו בנקודות האור ליד הבד: זיהוי, גיהוץ, מבחן שריפה.\n" +
                "אחר כך — גהצו את הבד עד שכל הקמטים ייעלמו.",
                PM_Target.Fabric, PM_Action.IronFabric, t);
            st.group = PM_HotspotGroup.Fabric;
            s.Add(st);
        }

        var collar = new PM_Step("collar_task", "משימה: צווארון על מגהצון פינות",
            "צווארון של חולצת כותנה מונח על מגהצון הפינות.\n" +
            "בחרו ••• והפעילו קיטור. גהצו מהקצוות פנימה, כדי שלא ייווצרו קפלים בפינות הצווארון.",
            PM_Target.Fabric, PM_Action.IronFabric, PM_FabricType.Cotton);
        collar.toolTask = 1;
        s.Add(collar);
        var chest = new PM_Step("chest_task", "משימה: אזור החזה בשמלה על החמור",
            "חלק החזה של שמלת משי מונח על החמור (קבנצ'יק) — הצורה המעוגלת שומרת על הנפח.\n" +
            "משי: •• בלי קיטור, דרך מטלית. גהצו בתנועות קצרות לאורך הקימור.",
            PM_Target.Fabric, PM_Action.IronFabric, PM_FabricType.Silk);
        chest.toolTask = 2;
        s.Add(chest);

        s.Add(new PM_Step("learn_end", "סיום מסלול הלימוד",
            "כל הכבוד! הכרתם את העמדה, את כלי העזר ואת חמשת סוגי הסיבים.\n" +
            "עכשיו אפשר לעבור למבחן: בדים בלי שם — ועליכם לזהות אותם ולגהץ נכון.",
            PM_Target.None, PM_Action.Next));
        return s;
    }

    public static string SteamLabel(PM_Steam s)
    {
        if (s == PM_Steam.Required) return "עם קיטור";
        if (s == PM_Steam.Forbidden) return "בלי קיטור";
        return "קיטור לא חובה";
    }

    // ---------- Points of light ----------
    public static List<PM_HotspotInfo> StationHotspots()
    {
        var l = new List<PM_HotspotInfo>();
        l.Add(new PM_HotspotInfo("power", "מתג ראשי", "המתג הראשי",
            "מפעיל את דוד הקיטור ואת חימום המגהץ.\n" +
            "בסיום העבודה: מכבים את המתג ומחכים שהעמדה תתקרר לפני ניקוי או הזזה.",
            PM_Target.Power, PM_Anchor.Front));
        l.Add(new PM_HotspotInfo("gauge", "מד לחץ", "מד לחץ הקיטור (מנומטר)",
            "המים בדוד מתחממים והלחץ עולה. לחץ עבודה תקין: 3.5–4 בר.\n" +
            "לא מתחילים לגהץ לפני שהמחוג מגיע לטווח: לחץ נמוך נותן קיטור רטוב, שמשאיר כתמי מים על הבד.\n" +
            "לחץ גבוה מהטווח — מכבים את העמדה ומדווחים.",
            PM_Target.Gauge, PM_Anchor.Front));
        l.Add(new PM_HotspotInfo("temp", "טמפרטורה", "בחירת טמפרטורה",
            "שלוש דרגות, לפי סימני תווית הטיפול בבגד:\n" +
            "• — עד 110°C: סינתטי (פוליאסטר, ניילון, אקריל)\n" +
            "•• — עד 150°C: צמר, משי\n" +
            "••• — עד 200°C: כותנה, פשתן\n" +
            "גם במגהץ עצמו יש וסת (תרמוסטט): מכוונים לפי הבד הרגיש ביותר בבגד.",
            PM_Target.TempButtons, PM_Anchor.FrontBelow));
        l.Add(new PM_HotspotInfo("iron", "מגהץ", "המגהץ המקצועי",
            "מגהץ קיטור תעשייתי, כבד יותר ממגהץ ביתי — המשקל עוזר ללחוץ על הבד.\n" +
            "מחזיקים בידית. כפתור הקיטור נמצא מתחת לידית; בשלט — ההדק.\n" +
            "לפני הגיהוץ הראשון פולטים קיטור באוויר, כדי לנקז מים שהתעבו בצינור.",
            PM_Target.Iron, PM_Anchor.Top));
        l.Add(new PM_HotspotInfo("shoe", "סוליית טפלון", "סוליית טפלון",
            "כיסוי טפלון שמלבישים על סוליית המגהץ.\n" +
            "מגן על בדים עדינים וכהים מפני ברק (לאס) וחריכה, ומפזר את החום באופן אחיד.\n" +
            "מתאים במיוחד לצמר, למשי ולבדים כהים.",
            PM_Target.Iron, PM_Anchor.IronFront));
        l.Add(new PM_HotspotInfo("rest", "משטח הנחה", "משטח ההנחה של המגהץ",
            "משטח עמיד לחום שעליו מניחים את המגהץ בין פעולה לפעולה.\n" +
            "מגהץ מקצועי מניחים תמיד בשכיבה על המשטח — לא מעמידים אותו על העקב כמו מגהץ ביתי: הוא עלול ליפול ולגרום לכוויה.\n" +
            "לעולם לא משאירים מגהץ חם על הבד או על כיסוי הקרש — זו הסיבה העיקרית לכתמי חריכה ולשריפות.",
            PM_Target.Rest, PM_Anchor.Front));
        l.Add(new PM_HotspotInfo("boom", "זרוע הכבל", "זרוע תליית הכבל",
            "הכבל וצינור הקיטור תלויים על זרוע עם קפיץ.\n" +
            "כך הם לא נגררים על הבד ולא מקמטים אותו, לא מפריעים לתנועת היד, והצינור נשאר ישר — פחות מים מתעבים בתוכו.",
            PM_Target.Boom, PM_Anchor.Front));
        l.Add(new PM_HotspotInfo("sleeve", "שרוולון", "שרוולון מובנה",
            "קרש צר שמחובר לעמדה.\n" +
            "מגהצים עליו שרוולים, מכפלות מכנסיים וחלקים צרים — שכבה אחת בכל פעם, בלי ליצור קפל בצד השני.",
            PM_Target.SleeveBoard, PM_Anchor.Top));
        l.Add(new PM_HotspotInfo("board", "משטח גיהוץ", "משטח הגיהוץ",
            "משטח רחב עם ריפוד וכיסוי עמיד לחום.\n" +
            "בעמדות מקצועיות רבות יש במשטח יניקת אוויר (ואקום): היא מחזיקה את הבד במקום ומוציאה ממנו קיטור ולחות, כך שהבד מתייבש ומתקבע מהר.\n" +
            "שומרים על הכיסוי נקי — לכלוך ודבק עוברים לבגד.",
            PM_Target.Board, PM_Anchor.BoardLeft));
        foreach (PM_HotspotInfo h in l) h.group = PM_HotspotGroup.Station;
        return l;
    }

    public static List<PM_HotspotInfo> ToolHotspots()
    {
        var l = new List<PM_HotspotInfo>();
        l.Add(new PM_HotspotInfo("ham", "חמור (קבנצ'יק)", "חמור — כרית חייט (קבנצ'יק)",
            "כרית קשיחה וגדולה בצורת ביצה, ממולאת בנסורת.\n" +
            "משמשת לגיהוץ ולעיצוב אזורים מעוגלים: חזה בז'קט, פנסים, תפרי כתף, ראש שרוול.\n" +
            "שומרת על הצורה התלת-ממדית של הבגד.",
            PM_Target.Ham, PM_Anchor.Top));
        l.Add(new PM_HotspotInfo("point", "מגהצון פינות", "מגהצון לפינות וצווארונים",
            "כלי עץ עם קצה צר ומחודד.\n" +
            "בעזרתו פותחים תפרים בתוך צווארונים, דשים ופינות.\n" +
            "בבסיס העץ (קלאפר) לוחצים על הבד מיד אחרי הקיטור: העץ סופג חום ולחות ומקבע קפל חד ושטוח.",
            PM_Target.PointPresser, PM_Anchor.Top));
        l.Add(new PM_HotspotInfo("cloth", "מטלית גיהוץ", "מטלית גיהוץ",
            "בד כותנה דק או אורגנזה שמניחים בין המגהץ לבגד.\n" +
            "חובה בצמר, במשי ובבדים כהים — מונעת ברק וכתמים.\n" +
            "מטלית לחה מוסיפה לחות לבדים שצריכים אותה.",
            PM_Target.Cloth, PM_Anchor.Top));
        l.Add(new PM_HotspotInfo("fusible", "דביקונים", "דביקונים (פליזלין)",
            "לעולם לא מגהצים דביקון ישירות על כיסוי הקרש או במגע עם סוליית המגהץ!\n" +
            "מניחים נייר אפייה או מטלית מתחת ומעל.\n" +
            "את הדבק מפעילים בלחיצה והחזקה של 10–15 שניות בכל נקודה — לא בהחלקה, כדי שהשכבות לא יזוזו.",
            PM_Target.Fusible, PM_Anchor.Top));
        foreach (PM_HotspotInfo h in l) { h.group = PM_HotspotGroup.Tools; h.accent = PM_Util.Violet; }
        return l;
    }

    public static List<PM_HotspotInfo> FabricHotspots(PM_FabricType t)
    {
        PM_FabricInfo f = Fabric(t);
        string id = t.ToString().ToLower();
        var l = new List<PM_HotspotInfo>
        {
            new PM_HotspotInfo(id + "_look", "זיהוי", f.name + " — זיהוי הבד", f.lookText, PM_Target.Fabric, PM_Anchor.Top),
            new PM_HotspotInfo(id + "_press", "גיהוץ", f.name + " — גיהוץ", f.pressText, PM_Target.Fabric, PM_Anchor.Top),
            new PM_HotspotInfo(id + "_burn", "מבחן שריפה", f.name + " — מבחן שריפה", f.burnText, PM_Target.Fabric, PM_Anchor.Top)
        };
        foreach (PM_HotspotInfo h in l) { h.group = PM_HotspotGroup.Fabric; h.accent = PM_Util.ModeColor(f.mode); }
        return l;
    }

    // Floating fiber symbols above the station.
    public static List<PM_HotspotInfo> FiberHotspots()
    {
        var l = new List<PM_HotspotInfo>();
        foreach (PM_FabricType t in new[] { PM_FabricType.Cotton, PM_FabricType.Linen, PM_FabricType.Wool, PM_FabricType.Silk, PM_FabricType.Polyester })
        {
            PM_FabricInfo f = Fabric(t);
            var h = new PM_HotspotInfo("fiber_" + t.ToString().ToLower(), "סיב " + f.name, "סיב " + f.name, f.fiberText, PM_Target.None, PM_Anchor.Top);
            h.group = PM_HotspotGroup.Fibers;
            h.accent = PM_Util.ModeColor(f.mode);
            h.isFiber = true;
            h.fiber = t;
            l.Add(h);
        }
        return l;
    }

    static void FabricTexts(PM_FabricInfo f)
    {
        switch (f.type)
        {
            case PM_FabricType.Cotton:
                f.fiberText =
                    "מקור: צמחי — סיבים שגדלים סביב זרעי צמח הכותנה. חומר: תאית (צלולוז).\n" +
                    "צורה: סיב קצר (2–4 ס\"מ), שטוח ומפותל כמו סרט.\n" +
                    "מבנה הבד: גם אריגה (פופלין, דנים) וגם סריגה (טריקו, חולצות טי).";
                f.pressText =
                    "חום: גבוה, ••• (עד 200°C).\n" +
                    "לחות: קיטור מלא. כותנה יבשה מאוד — מרטיבים מעט לפני הגיהוץ.\n" +
                    "לחץ: אפשר ללחוץ חזק.\n" +
                    "בדים כהים — מגהצים מהצד ההפוך, כדי למנוע ברק.";
                f.burnText =
                    "נדלקת מהר, בלהבה צהובה, וממשיכה לבעור גם אחרי שמרחיקים את האש.\n" +
                    "ריח: נייר שרוף.\n" +
                    "שארית: אפר אפור ורך, שמתפורר לאבק.";
                break;
            case PM_FabricType.Linen:
                f.fiberText =
                    "מקור: צמחי — סיבים מגבעול צמח הפשתן. חומר: תאית.\n" +
                    "צורה: סיב ארוך וקשיח, כמעט בלי גמישות — ולכן מתקמט מאוד.\n" +
                    "מבנה הבד: בעיקר אריגה; לחוט יש עיבויים לא אחידים.";
                f.pressText =
                    "חום: הגבוה ביותר, ••• (200°C).\n" +
                    "לחות: הרבה. מגהצים כשהבד עדיין לח, או מרססים מים לפני.\n" +
                    "לחץ: חזק. בדים כהים — מהצד ההפוך, אחרת נוצר ברק.\n" +
                    "ההבדל מכותנה: פשתן קשיח יותר ומתקמט יותר, ולכן דורש יותר לחות וזמן.";
                f.burnText =
                    "כמו כותנה: בוער מהר בלהבה צהובה, ריח של נייר שרוף, אפר אפור ורך.\n" +
                    "את ההבדל מכותנה רואים בבד עצמו: חוט עבה ולא אחיד, מגע קשיח וקריר.";
                break;
            case PM_FabricType.Wool:
                f.fiberText =
                    "מקור: מן החי — שיער כבשים. חומר: חלבון (קרטין).\n" +
                    "צורה: סיב מסולסל עם קשקשים זעירים; גמיש וקפיצי.\n" +
                    "מבנה הבד: אריגה (בדי חליפות) או סריגה (סוודרים).";
                f.pressText =
                    "חום: בינוני, •• (עד 150°C).\n" +
                    "לחות: חובה קיטור, ותמיד דרך מטלית גיהוץ.\n" +
                    "לחץ: מניחים ומרימים — לא מחליקים, כדי לא למתוח את הבד.\n" +
                    "לא מגהצים עד ייבוש מלא: נוצר ברק (לאס) והסיבים נמעכים.\n" +
                    "בעזרת קיטור אפשר לעצב צמר: לכווץ או למתוח.";
                f.burnText =
                    "בוער לאט, מתכווץ מהאש, ולרוב כבה מעצמו.\n" +
                    "ריח: שיער או נוצות שרופים.\n" +
                    "שארית: גוש שחור ופריך, שמתפורר בין האצבעות.";
                break;
            case PM_FabricType.Silk:
                f.fiberText =
                    "מקור: מן החי — חוט מפקעת של תולעת המשי. חומר: חלבון (פיברואין).\n" +
                    "צורה: סיב ארוך מאוד ורציף (פילמנט), חלק ומבריק.\n" +
                    "מבנה הבד: בעיקר אריגה — סאטן, שיפון, קרפ.";
                f.pressText =
                    "חום: נמוך עד בינוני, •• (עד 150°C) — מתחילים בחום הנמוך.\n" +
                    "לחות: בלי קיטור ובלי ריסוס — טיפות מים משאירות כתמים.\n" +
                    "לחץ: קל. מגהצים מהצד ההפוך ודרך מטלית.";
                f.burnText =
                    "בוער לאט ומתכווץ מהאש.\n" +
                    "ריח: שיער שרוף, עדין יותר מצמר.\n" +
                    "שארית: כדורית שחורה קטנה, שנמעכת בקלות.";
                break;
            case PM_FabricType.Polyester:
                f.fiberText =
                    "מקור: כימי — מופק מנפט. חומר: פולימר (פלסטיק).\n" +
                    "צורה: סיב חלק ואחיד, שנמס בחום (תרמופלסטי).\n" +
                    "מבנה הבד: גם אריגה וגם סריגה — בגדי ספורט, בטנות, חולצות.";
                f.pressText =
                    "חום: נמוך, • (עד 110°C).\n" +
                    "לחות: לא נחוצה; מעט קיטור מותר.\n" +
                    "לחץ: קל. בחום גבוה הסיב נמס: מופיע ברק פלסטיקי והבד נדבק לסוליה.\n" +
                    "כדאי לגהץ דרך מטלית.";
                f.burnText =
                    "מתכווץ מהאש ונמס, מטפטף ובוער עם עשן שחור.\n" +
                    "ריח: כימי, מתקתק.\n" +
                    "שארית: חרוז שחור וקשה, שלא מתפורר.";
                break;
        }
    }

    // ---------- Exam ----------
    public const string ExamTitle = "מבחן";
    public const string ExamBody =
        "תקבלו 3 בדים בלי שם. לכל בד — 60 שניות.\n" +
        "זהו את הבד לפי המראה ולפי תוצאת מבחן השריפה, בחרו טמפרטורה, החליטו על קיטור — וגהצו.\n" +
        "טעות בטמפרטורה עלולה לשרוף את הבד!\n" +
        "הפעילו את העמדה והמתינו ללחץ — ואז לחצו על \"הבא\".";
    public const string ExamFabricTitle = "בד מספר {0}";
    public const string ExamFabricBody = "מראה: {0}\nמבחן שריפה: {1}\nבחרו טמפרטורה וקיטור — וגהצו.";
    public const string ExamResultTitle = "תוצאות המבחן";
    public const string ExamLineOk = "בד {0}: {1} — הצלחה ({2} שניות)";
    public const string ExamLineBurn = "בד {0}: {1} — נפגע ({2})";
    public const string ExamLineTime = "בד {0}: {1} — הזמן נגמר";
    public static readonly string[] Ranks = { "דרגה: מתחילים — נסו שוב", "דרגה: מתלמד", "דרגה: מקצועי", "דרגה: מאסטר גיהוץ" };

    // ---------- Fabrics ----------
    static Dictionary<PM_FabricType, PM_FabricInfo> fabrics;

    public static PM_FabricInfo Fabric(PM_FabricType t)
    {
        if (fabrics == null)
        {
            fabrics = new Dictionary<PM_FabricType, PM_FabricInfo>();
            fabrics[PM_FabricType.Cotton] = new PM_FabricInfo
            {
                type = PM_FabricType.Cotton, name = "כותנה", mode = 3, steam = PM_Steam.Required,
                color = new Color(0.86f, 0.89f, 0.95f), smoothness = 0.15f, weave = PM_Weave.Plain, ironSeconds = 0.8f,
                cue = "בד מט ורך, אריגה צפופה ואחידה.",
                burn = "בוער מהר, ריח נייר שרוף, אפר אפור ורך."
            };
            fabrics[PM_FabricType.Linen] = new PM_FabricInfo
            {
                type = PM_FabricType.Linen, name = "פשתן", mode = 3, steam = PM_Steam.Required,
                color = new Color(0.80f, 0.74f, 0.62f), smoothness = 0.1f, weave = PM_Weave.Slub, ironSeconds = 1.3f,
                cue = "בד מט וקשיח, חוטים עבים ולא אחידים, קמטים עמוקים.",
                burn = "בוער מהר, ריח נייר שרוף, אפר אפור ורך."
            };
            fabrics[PM_FabricType.Wool] = new PM_FabricInfo
            {
                type = PM_FabricType.Wool, name = "צמר", mode = 2, steam = PM_Steam.Required,
                color = new Color(0.22f, 0.25f, 0.33f), smoothness = 0.05f, weave = PM_Weave.Twill, ironSeconds = 1.0f,
                cue = "בד עבה ורך, מעט שעיר, עם מבנה אלכסוני.",
                burn = "בוער לאט וכבה מעצמו, ריח שיער שרוף, גוש שחור ופריך."
            };
            fabrics[PM_FabricType.Silk] = new PM_FabricInfo
            {
                type = PM_FabricType.Silk, name = "משי", mode = 2, steam = PM_Steam.Forbidden,
                color = new Color(0.85f, 0.45f, 0.58f), smoothness = 0.75f, weave = PM_Weave.Satin, ironSeconds = 0.6f,
                cue = "בד דק, חלק ומבריק מאוד.",
                burn = "בוער לאט, ריח שיער שרוף עדין, כדורית שחורה רכה."
            };
            fabrics[PM_FabricType.Polyester] = new PM_FabricInfo
            {
                type = PM_FabricType.Polyester, name = "פוליאסטר", mode = 1, steam = PM_Steam.Optional,
                color = new Color(0.25f, 0.55f, 0.85f), smoothness = 0.55f, weave = PM_Weave.Smooth, ironSeconds = 0.6f,
                cue = "בד חלק, מבריק מעט, אחיד לגמרי.",
                burn = "נמס ומטפטף, עשן שחור, חרוז קשה שלא מתפורר."
            };
            foreach (PM_FabricInfo fi in fabrics.Values) FabricTexts(fi);
            fabrics[PM_FabricType.Cotton].lookText =
                "מראה: מט, אחיד, בלי ברק.\nמגע: רך ונעים, נושם.\nבדיקת קימוט: מועכים פינה ביד — נשארים קמטים בינוניים.";
            fabrics[PM_FabricType.Linen].lookText =
                "מראה: מט, עם עיבויים בחוט ומרקם לא אחיד.\nמגע: קשיח, קריר ויבש.\nבדיקת קימוט: מתקמט מהר ועמוק — הקמטים נשארים חדים.";
            fabrics[PM_FabricType.Wool].lookText =
                "מראה: מט, לעיתים שעיר מעט; בבדי חליפות — מבנה אלכסוני.\nמגע: חם, קפיצי וגמיש.\nבדיקת קימוט: חוזר לצורתו כמעט בלי קמטים.";
            fabrics[PM_FabricType.Silk].lookText =
                "מראה: ברק עדין ויוקרתי, נופל ברכות.\nמגע: חלק, קריר בהתחלה ומתחמם מהר.\nבדיקת קימוט: מתקמט מעט; בשפשוף נשמע רשרוש אופייני.";
            fabrics[PM_FabricType.Polyester].lookText =
                "מראה: אחיד לגמרי, לעיתים ברק \"פלסטיקי\".\nמגע: חלק, פחות נושם, לפעמים חשמל סטטי.\nבדיקת קימוט: כמעט לא מתקמט — חוזר מיד לצורתו.";
            fabrics[PM_FabricType.Cotton].garment = "דוגמה: מעיל ג'ינס — ג'ינס הוא אריג כותנה";
            fabrics[PM_FabricType.Linen].garment = "צמח הפשתן — מהגבעול מפיקים את סיבי הפשתן";
            fabrics[PM_FabricType.Wool].garment = "דוגמה: ז'קט צמר";
            fabrics[PM_FabricType.Silk].garment = "דוגמה: שמלת משי";
            fabrics[PM_FabricType.Polyester].garment = "דוגמה: שמלה מפוליאסטר";
        }
        return fabrics[t];
    }

    public static string ModeLabel(int mode)
    {
        if (mode < 1 || mode > 3) return "";
        return ModeDots[mode] + " (" + ModeTemp[mode] + ")";
    }
}
