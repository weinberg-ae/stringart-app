# Creates Hebrew narration MP3 files for Pressing Master VR (Microsoft Edge neural voice).
# Livelier reading: every sentence gets a slightly different pitch/speed.
import asyncio, os, re, sys
try:
    import edge_tts
except ImportError:
    print("edge-tts is not installed"); sys.exit(1)

# Two narrators: a materials expert (female) for fibers and fabrics, a station technician (male) for the rest.
EXPERT = "he-IL-HilaNeural"
TECHNICIAN = "he-IL-AvriNeural"
EXPERT_PREFIXES = ("menu", "fibers", "hs_fiber_", "cotton", "linen", "wool", "silk", "polyester",
                   "hs_cotton", "hs_linen", "hs_wool", "hs_silk", "hs_polyester", "chest_task", "learn_end")

def voice_for(name):
    return EXPERT if name.startswith(EXPERT_PREFIXES) else TECHNICIAN
# Pronunciation fixes: words get vowel marks (nikud) ONLY for the voice. Add more words here if needed.
# Each word also matches with prefixes ו ה ב ל מ ש כ (for example: הפשתן, בעמדה, והלחץ).
PRONOUNCE = {
    # fibers and fabrics
    "פשתן": "פישטאן",
    "פוליאסטר": "פולי אסטר",
    "כותנה": "כּוּתְנָה",
    "צמר": "צֶמֶר",
    "משי": "מֶשִׁי",
    "סיב": "סִיב",
    "סיבים": "סִיבִים",
    "תאית": "תָּאִית",
    "צלולוז": "צֶלוּלוֹז",
    "חלבון": "חֶלְבּוֹן",
    "פולימר": "פּוֹלִימֶר",
    "נפט": "נֵפְט",
    "אריגה": "אֲרִיגָה",
    "סריגה": "סְרִיגָה",
    "ויסקוזה": "וִיסְקוֹזָה",
    "אצטט": "אָצֶטָט",
    "אקריליק": "אַקְרִילִיק",
    "אלסטן": "אֶלַסְטָן",
    "ליוסל": "לְיוֹסֶל",
    # station and tools
    "עמדה": "עֶמְדָּה",
    "עמדת": "עֶמְדַּת",
    "עמדות": "עֶמְדּוֹת",
    "מגהץ": "מַגְהֵץ",
    "מגהצון": "מַגְהֲצוֹן",
    "גיהוץ": "גִּיהוּץ",
    "קיטור": "קִיטוֹר",
    "לחץ": "לַחַץ",
    "מנומטר": "מָנוֹמֶטֶר",
    "מיכל": "מֵיכָל",
    "אבנית": "אַבְנִית",
    "דוד": "דּוּד",
    "הדק": "הֶדֶק",
    "ידית": "יָדִית",
    "סוליה": "סוּלְיָה",
    "משטח": "מִשְׁטָח",
    "מטלית": "מַטְלִית",
    "דביקון": "דְּבִיקוֹן",
    "שרוולון": "שַׁרְווּלוֹן",
    "חמור": "חֲמוֹר",
    "כבל": "כֶּבֶל",
    "טמפרטורה": "טֶמְפֶּרָטוּרָה",
    # garments and the sewing room
    "צווארון": "צַוָּארוֹן",
    "שמלה": "שִׂמְלָה",
    "שמלת": "שִׂמְלַת",
    "חולצה": "חוּלְצָה",
    "חולצת": "חוּלְצַת",
    "בובה": "בּוּבָּה",
    "בובת": "בּוּבַּת",
    "תפירה": "תְּפִירָה",
    "סינר": "סִינָר",
    "קמטים": "קְמָטִים",
    "שריפה": "שְׂרֵפָה",
    "כוויה": "כְּוִיָּה",
    "בטיחות": "בְּטִיחוּת",
}

def fix_pronunciation(text):
    for word, voiced in PRONOUNCE.items():
        text = re.sub(r"(?<![\u05D0-\u05EA])([והבלמשכ]{0,2})" + word + r"(?![\u05D0-\u05EA])", lambda m: m.group(1) + voiced, text)
    return text

VARIANTS = [("+0%", "+0Hz"), ("+6%", "+6Hz"), ("-3%", "-3Hz"), ("+3%", "+10Hz")]

here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "Assets", "Resources", "PM_Voice")
os.makedirs(out, exist_ok=True)
text = open(os.path.join(here, "narration_he.txt"), encoding="utf-8-sig").read().replace("\r", "")
blocks = [b.strip().split("\n", 1) for b in text.split("\n\n") if b.strip()]

async def speak(sentence, rate, pitch, voice):
    data = b""
    comm = edge_tts.Communicate(sentence, voice, rate=rate, pitch=pitch)
    async for chunk in comm.stream():
        if chunk["type"] == "audio":
            data += chunk["data"]
    return data

async def main():
    for i, (name, body) in enumerate(blocks):
        sentences = [s.strip() for s in re.split(r"(?<=[.!?:])\s+", fix_pronunciation(body.strip())) if s.strip()]
        audio = b""
        for k, s in enumerate(sentences):
            rate, pitch = VARIANTS[k % len(VARIANTS)]
            for attempt in range(3):
                try:
                    audio += await speak(s, rate, pitch, voice_for(name.strip()))
                    break
                except Exception as e:
                    print("  retry", name, e)
                    await asyncio.sleep(2)
        open(os.path.join(out, name.strip()), "wb").write(audio)
        print(f"{i + 1}/{len(blocks)}  {name}")
    print("\nDONE. Files are in Assets/Resources/PM_Voice")

asyncio.run(main())
