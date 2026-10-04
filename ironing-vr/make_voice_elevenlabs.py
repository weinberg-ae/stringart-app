# OPTIONAL: very expressive narration with ElevenLabs (needs an ElevenLabs account + API key).
# Run:  python make_voice_elevenlabs.py   and paste your API key when asked.
import json, os, urllib.request

MODEL = "eleven_v3"                       # model with Hebrew support
VOICE_ID = "21m00Tcm4TlvDq8ikWAM"         # any voice id from your ElevenLabs Voice Library

here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "Assets", "Resources", "PM_Voice")
os.makedirs(out, exist_ok=True)
key = input("ElevenLabs API key: ").strip()
text = open(os.path.join(here, "narration_he.txt"), encoding="utf-8-sig").read().replace("\r", "")
blocks = [b.strip().split("\n", 1) for b in text.split("\n\n") if b.strip()]
for i, (name, body) in enumerate(blocks):
    req = urllib.request.Request(
        "https://api.elevenlabs.io/v1/text-to-speech/" + VOICE_ID + "?output_format=mp3_44100_128",
        data=json.dumps({"text": body.strip(), "model_id": MODEL}).encode("utf-8"),
        headers={"xi-api-key": key, "Content-Type": "application/json", "Accept": "audio/mpeg"})
    with urllib.request.urlopen(req) as r:
        open(os.path.join(out, name.strip()), "wb").write(r.read())
    print(f"{i + 1}/{len(blocks)}  {name}")
print("DONE")
input("Press Enter")
