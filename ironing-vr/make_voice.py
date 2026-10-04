# Creates Hebrew narration MP3 files for Pressing Master VR (Microsoft Edge neural voice).
# Livelier reading: every sentence gets a slightly different pitch/speed.
import asyncio, os, re, sys
try:
    import edge_tts
except ImportError:
    print("edge-tts is not installed"); sys.exit(1)

VOICE = "he-IL-HilaNeural"      # female. Male: "he-IL-AvriNeural"
VARIANTS = [("+0%", "+0Hz"), ("+6%", "+6Hz"), ("-3%", "-3Hz"), ("+3%", "+10Hz")]

here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "Assets", "Resources", "PM_Voice")
os.makedirs(out, exist_ok=True)
text = open(os.path.join(here, "narration_he.txt"), encoding="utf-8-sig").read().replace("\r", "")
blocks = [b.strip().split("\n", 1) for b in text.split("\n\n") if b.strip()]

async def speak(sentence, rate, pitch):
    data = b""
    comm = edge_tts.Communicate(sentence, VOICE, rate=rate, pitch=pitch)
    async for chunk in comm.stream():
        if chunk["type"] == "audio":
            data += chunk["data"]
    return data

async def main():
    for i, (name, body) in enumerate(blocks):
        sentences = [s.strip() for s in re.split(r"(?<=[.!?:])\s+", body.strip()) if s.strip()]
        audio = b""
        for k, s in enumerate(sentences):
            rate, pitch = VARIANTS[k % len(VARIANTS)]
            for attempt in range(3):
                try:
                    audio += await speak(s, rate, pitch)
                    break
                except Exception as e:
                    print("  retry", name, e)
                    await asyncio.sleep(2)
        open(os.path.join(out, name.strip()), "wb").write(audio)
        print(f"{i + 1}/{len(blocks)}  {name}")
    print("\nDONE. Files are in Assets/Resources/PM_Voice")

asyncio.run(main())
