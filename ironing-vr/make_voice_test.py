# Pronunciation test: every line of voice_test.txt is spoken by both narrators.
# Listen to the files in the voice_test folder and pick the number that sounds right.
import asyncio, os, sys
try:
    import edge_tts
except ImportError:
    print("edge-tts is not installed"); sys.exit(1)

VOICES = {"hila": "he-IL-HilaNeural", "avri": "he-IL-AvriNeural"}
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "voice_test")
os.makedirs(out, exist_ok=True)
lines = [l.strip() for l in open(os.path.join(here, "voice_test.txt"), encoding="utf-8-sig") if l.strip()]

async def main():
    for i, line in enumerate(lines, 1):
        for short, voice in VOICES.items():
            data = b""
            async for chunk in edge_tts.Communicate(line, voice).stream():
                if chunk["type"] == "audio":
                    data += chunk["data"]
            open(os.path.join(out, "%02d_%s.mp3" % (i, short)), "wb").write(data)
        print("%02d  %s" % (i, line))
    print("\nDONE. Listen to the files in the voice_test folder.")

asyncio.run(main())
