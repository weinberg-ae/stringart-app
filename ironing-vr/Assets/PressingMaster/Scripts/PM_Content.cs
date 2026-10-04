using System.Collections.Generic;
using UnityEngine;

// All game texts (Hebrew) and fabric data. Edit texts here.
public enum PM_Target { None, Power, Gauge, Iron, Boom, TempButtons, SleeveBoard, Board, Ham, PointPresser, Cloth, Fusible, Fabric }
public enum PM_Action { Next, Power, WaitPressure, GrabIron, SteamInAir, PressTemp, IronFabric }
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
}

public static class PM_Content
{
    public const string BtnNext = "הבא";
    public const string BtnRepeat = "שמע שוב";
    public const string BtnLearn = "מסלול לימוד";
    public const string BtnExam = "מבחן";
    public const string BtnMenu = "תפריט";

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

    // ---------- Learning path ----------
    public static List<PM_Step> LearningSteps()
    {
        var s = new List<PM_Step>();
        s.Add(new PM_Step("learn_intro", "מסלול לימוד",
            "במסלול הזה נכיר את עמדת הגיהוץ, את כלי העזר ואת חמשת סוגי הבדים העיקריים.\n" +
            "החלק שצריך לגעת בו מואר. כוונו אליו את היד או את הקרן ולחצו על ההדק.\n" +
            "כדי להמשיך — לחצו על \"הבא\".",
            PM_Target.None, PM_Action.Next));

        s.Add(new PM_Step("power", "הפעלת העמדה",
            "המתג הראשי מפעיל את דוד הקיטור ואת חימום המגהץ.\n" +
            "סובבו את המתג המואר בצד ימין של לוח הבקרה.",
            PM_Target.Power, PM_Action.Power));

        s.Add(new PM_Step("pressure", "מד לחץ הקיטור",
            "המים בדוד מתחממים והלחץ עולה. לחץ עבודה תקין: 3.5–4 בר.\n" +
            "אין להתחיל לגהץ לפני שהמחוג מגיע לטווח: לחץ נמוך נותן קיטור רטוב, שמשאיר כתמי מים על הבד.\n" +
            "המתינו עד שהמחוג יעלה.",
            PM_Target.Gauge, PM_Action.WaitPressure));

        s.Add(new PM_Step("safety", "הוראות בטיחות",
            "• קיטור חם מ-100°C וגורם לכוויות — לעולם לא מכוונים קיטור אל היד או אל הגוף.\n" +
            "• בין פעולה לפעולה המגהץ מונח על משטח ההנחה בלבד, לא על הבד.\n" +
            "• לפני תחילת העבודה פולטים קיטור באוויר, כדי לנקז מים שהתעבו בצינור.\n" +
            "• לא מושכים ולא מקפלים את כבל המגהץ.\n" +
            "• בסיום: מכבים את המתג הראשי ומחכים שהעמדה תתקרר.",
            PM_Target.None, PM_Action.Next));

        s.Add(new PM_Step("boom", "זרוע תליית הכבל",
            "הכבל וצינור הקיטור תלויים על זרוע עם קפיץ.\n" +
            "כך הם לא נגררים על הבד ולא מקמטים אותו, לא מפריעים לתנועת היד, והצינור נשאר ישר — פחות מים מתעבים בתוכו.",
            PM_Target.Boom, PM_Action.Next));

        s.Add(new PM_Step("iron", "המגהץ המקצועי",
            "מגהץ קיטור תעשייתי, כבד יותר ממגהץ ביתי — המשקל עוזר ללחוץ על הבד.\n" +
            "מחזיקים בידית. כפתור הקיטור נמצא מתחת לידית; בשלט — ההדק.\n" +
            "הרימו את המגהץ: כוונו אליו את היד ולחצו על הכפתור הצדדי (Grip) והחזיקו.",
            PM_Target.Iron, PM_Action.GrabIron));

        s.Add(new PM_Step("purge", "פליטת קיטור ראשונית",
            "החזיקו את המגהץ באוויר, הרחק מהבד ומהגוף, ולחצו על ההדק במשך 2 שניות.\n" +
            "כך מנקזים את המים שהתעבו בצינור, ומונעים כתמים בגיהוץ הראשון.",
            PM_Target.Iron, PM_Action.SteamInAir));

        s.Add(new PM_Step("temp", "בחירת טמפרטורה",
            "שלוש דרגות, לפי סימני תווית הטיפול בבגד:\n" +
            "• — עד 110°C: סינתטי (פוליאסטר, ניילון, אקריל)\n" +
            "•• — עד 150°C: צמר, משי\n" +
            "••• — עד 200°C: כותנה, פשתן\n" +
            "גם במגהץ עצמו יש וסת (תרמוסטט): מכוונים לפי הבד הרגיש ביותר בבגד.\n" +
            "לחצו על אחד מכפתורי הטמפרטורה.",
            PM_Target.TempButtons, PM_Action.PressTemp));

        s.Add(new PM_Step("shoe", "סוליית טפלון",
            "כיסוי טפלון שמלבישים על סוליית המגהץ.\n" +
            "מגן על בדים עדינים וכהים מפני ברק (לאס) וחריכה, ומפזר את החום באופן אחיד.\n" +
            "מתאים במיוחד לצמר, למשי ולבדים כהים.",
            PM_Target.Iron, PM_Action.Next));

        s.Add(new PM_Step("sleeve", "שרוולון מובנה",
            "קרש צר שמחובר לעמדה.\n" +
            "מגהצים עליו שרוולים, מכפלות מכנסיים וחלקים צרים — שכבה אחת בכל פעם, בלי ליצור קפל בצד השני.",
            PM_Target.SleeveBoard, PM_Action.Next));

        s.Add(new PM_Step("ham", "כרית חייט (קבנצ'יק)",
            "כרית קשיחה בצורת ביצה, ממולאת בנסורת.\n" +
            "משמשת לגיהוץ ולעיצוב אזורים מעוגלים: חזה בז'קט, פנסים, תפרי כתף, ראש שרוול.\n" +
            "שומרת על הצורה התלת-ממדית של הבגד.",
            PM_Target.Ham, PM_Action.Next));

        s.Add(new PM_Step("point", "חמור — מגהצון לפינות",
            "כלי עץ עם קצה צר ומחודד.\n" +
            "בעזרתו פותחים תפרים בתוך צווארונים, דשים ופינות.\n" +
            "בבסיס העץ (קלאפר) לוחצים על הבד מיד אחרי הקיטור: העץ סופג חום ולחות ומקבע קפל חד ושטוח.",
            PM_Target.PointPresser, PM_Action.Next));

        s.Add(new PM_Step("cloth", "מטלית גיהוץ",
            "בד כותנה דק או אורגנזה שמניחים בין המגהץ לבגד.\n" +
            "חובה בצמר, במשי ובבדים כהים — מונעת ברק וכתמים.\n" +
            "מטלית לחה מוסיפה לחות לבדים שצריכים אותה.",
            PM_Target.Cloth, PM_Action.Next));

        s.Add(new PM_Step("fusible", "דביקונים (פליזלין)",
            "לעולם לא מגהצים דביקון ישירות על כיסוי הקרש או במגע עם סוליית המגהץ!\n" +
            "מניחים נייר אפייה או מטלית מתחת ומעל.\n" +
            "את הדבק מפעילים בלחיצה והחזקה של 10–15 שניות בכל נקודה — לא בהחלקה, כדי שהשכבות לא יזוזו.",
            PM_Target.Fusible, PM_Action.Next));

        s.Add(new PM_Step("fibers", "חמשת סוגי הסיבים",
            "בגדים עשויים מסיבים:\n" +
            "• צמחיים (תאית) — כותנה ופשתן\n" +
            "• מן החי (חלבון) — צמר ומשי\n" +
            "• כימיים (סינתטיים) — פוליאסטר\n" +
            "מקור הסיב קובע איך הבד מגיב לחום, ללחות וללחץ.",
            PM_Target.None, PM_Action.Next));

        AddFabric(s, PM_FabricType.Cotton, "cotton", "כותנה",
            "מקור: צמחי — סיבים שגדלים סביב זרעי צמח הכותנה. חומר: תאית (צלולוז).\n" +
            "צורה: סיב קצר (2–4 ס\"מ), שטוח ומפותל כמו סרט.\n" +
            "מבנה הבד: גם אריגה (פופלין, דנים) וגם סריגה (טריקו, חולצות טי).",
            "חום: גבוה, ••• (עד 200°C).\n" +
            "לחות: קיטור מלא. כותנה יבשה מאוד — מרטיבים מעט לפני הגיהוץ.\n" +
            "לחץ: אפשר ללחוץ חזק.\n" +
            "בדים כהים — מגהצים מהצד ההפוך, כדי למנוע ברק.",
            "נדלקת מהר, בלהבה צהובה, וממשיכה לבעור גם אחרי שמרחיקים את האש.\n" +
            "ריח: נייר שרוף.\n" +
            "שארית: אפר אפור ורך, שמתפורר לאבק.");

        AddFabric(s, PM_FabricType.Linen, "linen", "פשתן",
            "מקור: צמחי — סיבים מגבעול צמח הפשתן. חומר: תאית.\n" +
            "צורה: סיב ארוך וקשיח, כמעט בלי גמישות — ולכן מתקמט מאוד.\n" +
            "מבנה הבד: בעיקר אריגה; לחוט יש עיבויים לא אחידים.",
            "חום: הגבוה ביותר, ••• (200°C).\n" +
            "לחות: הרבה. מגהצים כשהבד עדיין לח, או מרססים מים לפני.\n" +
            "לחץ: חזק. בדים כהים — מהצד ההפוך, אחרת נוצר ברק.\n" +
            "ההבדל מכותנה: פשתן קשיח יותר ומתקמט יותר, ולכן דורש יותר לחות וזמן.",
            "כמו כותנה: בוער מהר בלהבה צהובה, ריח של נייר שרוף, אפר אפור ורך.\n" +
            "את ההבדל מכותנה רואים בבד עצמו: חוט עבה ולא אחיד, מגע קשיח וקריר.");

        AddFabric(s, PM_FabricType.Wool, "wool", "צמר",
            "מקור: מן החי — שיער כבשים. חומר: חלבון (קרטין).\n" +
            "צורה: סיב מסולסל עם קשקשים זעירים; גמיש וקפיצי.\n" +
            "מבנה הבד: אריגה (בדי חליפות) או סריגה (סוודרים).",
            "חום: בינוני, •• (עד 150°C).\n" +
            "לחות: חובה קיטור, ותמיד דרך מטלית גיהוץ.\n" +
            "לחץ: מניחים ומרימים — לא מחליקים, כדי לא למתוח את הבד.\n" +
            "לא מגהצים עד ייבוש מלא: נוצר ברק (לאס) והסיבים נמעכים.\n" +
            "בעזרת קיטור אפשר לעצב צמר: לכווץ או למתוח.",
            "בוער לאט, מתכווץ מהאש, ולרוב כבה מעצמו.\n" +
            "ריח: שיער או נוצות שרופים.\n" +
            "שארית: גוש שחור ופריך, שמתפורר בין האצבעות.");

        AddFabric(s, PM_FabricType.Silk, "silk", "משי",
            "מקור: מן החי — חוט מפקעת של תולעת המשי. חומר: חלבון (פיברואין).\n" +
            "צורה: סיב ארוך מאוד ורציף (פילמנט), חלק ומבריק.\n" +
            "מבנה הבד: בעיקר אריגה — סאטן, שיפון, קרפ.",
            "חום: נמוך עד בינוני, •• (עד 150°C) — מתחילים בחום הנמוך.\n" +
            "לחות: בלי קיטור ובלי ריסוס — טיפות מים משאירות כתמים.\n" +
            "לחץ: קל. מגהצים מהצד ההפוך ודרך מטלית.",
            "בוער לאט ומתכווץ מהאש.\n" +
            "ריח: שיער שרוף, עדין יותר מצמר.\n" +
            "שארית: כדורית שחורה קטנה, שנמעכת בקלות.");

        AddFabric(s, PM_FabricType.Polyester, "polyester", "פוליאסטר",
            "מקור: כימי — מופק מנפט. חומר: פולימר (פלסטיק).\n" +
            "צורה: סיב חלק ואחיד, שנמס בחום (תרמופלסטי).\n" +
            "מבנה הבד: גם אריגה וגם סריגה — בגדי ספורט, בטנות, חולצות.",
            "חום: נמוך, • (עד 110°C).\n" +
            "לחות: לא נחוצה; מעט קיטור מותר.\n" +
            "לחץ: קל. בחום גבוה הסיב נמס: מופיע ברק פלסטיקי והבד נדבק לסוליה.\n" +
            "כדאי לגהץ דרך מטלית.",
            "מתכווץ מהאש ונמס, מטפטף ובוער עם עשן שחור.\n" +
            "ריח: כימי, מתקתק.\n" +
            "שארית: חרוז שחור וקשה, שלא מתפורר.");

        s.Add(new PM_Step("learn_end", "סיום מסלול הלימוד",
            "כל הכבוד! הכרתם את העמדה, את כלי העזר ואת חמשת סוגי הסיבים.\n" +
            "עכשיו אפשר לעבור למבחן: בדים בלי שם — ועליכם לזהות אותם ולגהץ נכון.\n" +
            "לחצו על \"מבחן\" או על \"תפריט\".",
            PM_Target.None, PM_Action.Next));
        return s;
    }

    static void AddFabric(List<PM_Step> s, PM_FabricType t, string id, string name, string fiber, string pressing, string burn)
    {
        s.Add(new PM_Step(id + "_fiber", name + " — הסיב", fiber, PM_Target.Fabric, PM_Action.Next, t));
        s.Add(new PM_Step(id + "_press", name + " — גיהוץ", pressing, PM_Target.Fabric, PM_Action.Next, t));
        s.Add(new PM_Step(id + "_burn", name + " — מבחן שריפה", burn, PM_Target.Fabric, PM_Action.Next, t));
        s.Add(new PM_Step(id + "_practice", "תרגול: " + name,
            "בחרו טמפרטורה בלוח הבקרה, החליטו אם צריך קיטור (הדק), והעבירו את המגהץ על הבד עד שכל הקמטים ייעלמו.",
            PM_Target.Fabric, PM_Action.IronFabric, t));
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
        }
        return fabrics[t];
    }

    public static string ModeLabel(int mode)
    {
        if (mode < 1 || mode > 3) return "";
        return ModeDots[mode] + " (" + ModeTemp[mode] + ")";
    }
}
