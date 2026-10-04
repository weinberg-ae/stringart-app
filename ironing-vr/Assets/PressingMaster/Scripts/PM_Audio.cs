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
    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

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
        ambient.volume = 0.35f;
        boiler = gameObject.AddComponent<AudioSource>();
        boiler.playOnAwake = false;
        boiler.loop = true;
        boiler.volume = 0.25f;
    }

    void Start()
    {
        AudioClip amb = Clip("ambient_factory");
        if (amb != null) { ambient.clip = amb; ambient.Play(); }
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
        return true;
    }

    public void StopVoice() { voice.Stop(); }
}
