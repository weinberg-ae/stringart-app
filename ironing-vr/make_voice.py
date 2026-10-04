# Creates Hebrew narration MP3 files for Pressing Master VR (Microsoft Edge neural voice).
import asyncio, os, sys
try:
    import edge_tts
except ImportError:
    print("edge-tts is not installed"); sys.exit(1)

VOICE = "he-IL-HilaNeural"      # female voice. Male voice: "he-IL-AvriNeural"
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "Assets", "Resources", "PM_Voice")
os.makedirs(out, exist_ok=True)
text = open(os.path.join(here, "narration_he.txt"), encoding="utf-8-sig").read().replace("\r", "")
blocks = [b.strip().split("\n", 1) for b in text.split("\n\n") if b.strip()]

async def main():
    for i, (name, body) in enumerate(blocks):
        path = os.path.join(out, name.strip())
        for attempt in range(3):
            try:
                await edge_tts.Communicate(body.strip(), VOICE, rate="-5%").save(path)
                break
            except Exception as e:
                print("  retry", name, e)
                await asyncio.sleep(2)
        print(f"{i + 1}/{len(blocks)}  {name}")
    print("\nDONE. Files are in Assets/Resources/PM_Voice")

asyncio.run(main())
