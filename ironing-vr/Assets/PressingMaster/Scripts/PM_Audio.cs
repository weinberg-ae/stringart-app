using System.Collections.Generic;
using UnityEngine;

// Plays sounds from Resources/PM_Sounds and narration from Resources/PM_Voice (file name = step id).
public class PM_Audio : MonoBehaviour
{
    public static PM_Audio I;

    AudioSource oneShots;
    AudioSource voice;
    AudioSource ambient;
    AudioSource boiler;
    AudioSource music;
    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    readonly float[] samples = new float[256];
    public float VoiceLevel { get; private set; }   // 0..1, loudness of the narrator right now
    const float MusicVol = 0.12f;   // quiet background (0.3 was too loud)
    AudioSource sewing;
    float nextSew = 12f;

    void Awake()
    {
        I = this;
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        ambient = gameObject.AddComponent<AudioSource>();
        ambient.playOnAwake = false;
        ambient.loop = true;
        ambient.volume = 0.1f;    // the factory noise only far in the background
        boiler = gameObject.AddComponent<AudioSource>();
        boiler.playOnAwake = false;
        boiler.loop = true;
        boiler.volume = 0.12f;
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        sewing = gameObject.AddComponent<AudioSource>();
        sewing.playOnAwake = false;
        music.volume = MusicVol;
    }

    void Start()
    {
        AudioClip amb = Clip("ambient_factory");
        if (amb != null) { ambient.clip = amb; ambient.Play(); }
        AudioClip mus = Clip("music_pad");
        if (mus != null) { music.clip = mus; music.Play(); }
    }

    public AudioClip Clip(string name)
    {
        AudioClip c;
        if (cache.TryGetValue(name, out c)) return c;
        c = Resources.Load<AudioClip>("PM_Sounds/" + name);
        cache[name] = c;
        return c;
    }

    public void Play(string name, float volume = 1f)
    {
        AudioClip c = Clip(name);
        if (c != null) oneShots.PlayOneShot(c, volume);
    }

    public void SetBoiler(bool on)
    {
        if (on && !boiler.isPlaying)
        {
            boiler.clip = Clip("boiler_loop");
            if (boiler.clip != null) boiler.Play();
        }
        else if (!on) boiler.Stop();
    }

    // Narration for a step. Returns false if there is no recording.
    public bool PlayVoice(string id)
    {
        voice.Stop();
        AudioClip c = Resources.Load<AudioClip>("PM_Voice/" + id);
        if (c == null) return false;
        voice.clip = c;
        voice.Play();
        music.volume = MusicVol * 0.45f;   // music steps back while the narrator speaks
        return true;
    }

    public void StopVoice() { voice.Stop(); }

    void Update()
    {
        if (voice.isPlaying)
        {
            voice.GetOutputData(samples, 0);
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            VoiceLevel = Mathf.Lerp(VoiceLevel, Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) * 6f), 0.4f);
        }
        else VoiceLevel = Mathf.Lerp(VoiceLevel, 0f, 0.2f);
        if (music != null && !voice.isPlaying && music.volume < MusicVol) music.volume = Mathf.MoveTowards(music.volume, MusicVol, Time.deltaTime * 0.05f);
        // now and then a sewing machine somewhere in the workshop
        if (Time.time > nextSew)
        {
            nextSew = Time.time + Random.Range(25f, 55f);
            AudioClip c = Clip(Random.value < 0.5f ? "sewing_1" : "sewing_2");
            if (c != null && sewing != null && !voice.isPlaying) sewing.PlayOneShot(c, Random.Range(0.18f, 0.28f));
        }
    }
}
