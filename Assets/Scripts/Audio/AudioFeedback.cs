using System.Collections.Generic;
using UnityEngine;

public class AudioFeedback : MonoBehaviour
{
    [System.Serializable]
    public class Cue
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private RoundController round;
    [SerializeField] private LureController lure;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
    [SerializeField] private float musicFadeSpeed = 1f;

    [Header("Music stems (same length, same tempo, loopable)")]
    [SerializeField] private AudioClip musicBase;
    [SerializeField] private AudioClip musicDrive;
    [SerializeField] private AudioClip musicTension;
    [SerializeField] private float bpm = 120f;
    [SerializeField] private int subdivisions = 2;
    [SerializeField] private bool quantizeBanking = true;

    [Header("Variation")]
    [SerializeField] private float variationCents = 20f;

    [Header("Cues (empty slots use a built-in placeholder)")]
    [SerializeField] private Cue bank = new Cue();
    [SerializeField] private Cue leak = new Cue();
    [SerializeField] private Cue tick = new Cue();
    [SerializeField] private Cue go = new Cue();
    [SerializeField] private Cue alert = new Cue();
    [SerializeField] private Cue commitOn = new Cue();
    [SerializeField] private Cue commitOff = new Cue();
    [SerializeField] private Cue lureDrop = new Cue();
    [SerializeField] private Cue stingerS = new Cue();
    [SerializeField] private Cue stingerA = new Cue();
    [SerializeField] private Cue stingerB = new Cue();
    [SerializeField] private Cue stingerC = new Cue();
    [SerializeField] private Cue stingerD = new Cue();

    private static readonly int[] Scale = { 0, 2, 4, 7, 9, 12, 14, 16 };
    private const int PoolSize = 12;

    private readonly Dictionary<string, AudioClip> fallbacks = new Dictionary<string, AudioClip>();
    private AudioSource[] pool;
    private AudioSource[] music;
    private float[] musicLevel;
    private double musicStart;
    private bool musicRunning;
    private int poolIndex;
    private int lastTick = -1;
    private bool wasSealed;
    private bool wasLure;
    private bool ready;

    private void Start()
    {
        if (round == null)
        {
            Debug.LogError("AudioFeedback needs a RoundController.", this);
            enabled = false;
            return;
        }

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            pool[i] = MakeSource($"Voice {i}", false);
        }

        music = new[] { MakeSource("Music Base", true), MakeSource("Music Drive", true), MakeSource("Music Tension", true) };
        music[0].clip = musicBase;
        music[1].clip = musicDrive;
        music[2].clip = musicTension;
        musicLevel = new float[3];

        round.PhaseChanged += OnPhaseChanged;
        round.GlimmerBanked += OnBanked;
        round.GlimmerLeaked += OnLeaked;
        ready = true;
    }

    private void OnDestroy()
    {
        if (round == null) return;
        round.PhaseChanged -= OnPhaseChanged;
        round.GlimmerBanked -= OnBanked;
        round.GlimmerLeaked -= OnLeaked;
    }

    private void Update()
    {
        if (!ready) return;

        RoundPhase phase = round.Phase;
        UpdateMusic(phase);
        UpdateTick(phase);
        UpdateSeal(phase);
        UpdateLure();
    }

    private void UpdateMusic(RoundPhase phase)
    {
        if (!musicRunning) return;

        bool playing = phase == RoundPhase.Herd || phase == RoundPhase.Seal;
        bool active = playing || phase == RoundPhase.Intro;
        float fill = round.GlimmersTotal > 0 ? (float)round.GlimmersInPen / round.GlimmersTotal : 0f;

        float[] targets =
        {
            active ? 1f : 0f,
            playing ? Mathf.Clamp01((fill - 0.15f) / 0.45f) : 0f,
            phase == RoundPhase.Seal ? 1f : 0f
        };

        float total = 0f;
        for (int i = 0; i < music.Length; i++)
        {
            musicLevel[i] = Mathf.MoveTowards(musicLevel[i], targets[i], musicFadeSpeed * Time.deltaTime);
            music[i].volume = musicLevel[i] * musicVolume * masterVolume;
            total += musicLevel[i];
        }

        if (!active && total <= 0f) StopMusic();
    }

    private void UpdateTick(RoundPhase phase)
    {
        if (phase != RoundPhase.Intro) return;

        int number = Mathf.CeilToInt(round.IntroTimeLeft);
        if (number == lastTick || number <= 0) return;

        lastTick = number;
        Play(tick, "tick", () => Blip(660f, 0f, 0.25f, 14f), Jitter(), false);
    }

    private void UpdateSeal(RoundPhase phase)
    {
        bool sealedNow = phase == RoundPhase.Seal && round.GateSealed;
        if (sealedNow && !wasSealed)
        {
            Play(commitOn, "commitOn", () => Chord(new[] { 220f, 330f, 440f, 660f }, 0.9f), 1f, false);
        }
        else if (!sealedNow && wasSealed && phase == RoundPhase.Seal)
        {
            Play(commitOff, "commitOff", () => Blip(330f, -0.5f, 0.4f, 8f), 1f, false);
        }
        wasSealed = sealedNow;
    }

    private void UpdateLure()
    {
        if (lure == null) return;

        if (lure.Active && !wasLure) Play(lureDrop, "lure", () => Whoosh(0.7f), Jitter(), false);
        wasLure = lure.Active;
    }

    private void OnPhaseChanged(RoundPhase phase)
    {
        if (phase == RoundPhase.Intro)
        {
            lastTick = -1;
            StartMusic();
        }
        else if (phase == RoundPhase.Herd)
        {
            Play(go, "go", () => Blip(1320f, 0f, 0.6f, 6f), 1f, false);
        }
        else if (phase == RoundPhase.Seal)
        {
            Play(alert, "alert", () => Sequence(new[] { 880f, 660f }, 0.14f, 0.12f, 0.22f), 1f, false);
        }
        else if (phase == RoundPhase.Result)
        {
            PlayStinger(round.Result.score.grade);
        }
    }

    private void OnBanked(GlimmerAgent agent)
    {
        if (agent == null || agent.Archetype == null) return;
        Play(bank, "bank", () => Blip(440f, 0f, 0.35f, 9f), BankPitch(agent.Archetype.Multiplier), quantizeBanking);
    }

    private void OnLeaked(GlimmerAgent agent)
    {
        if (agent == null || agent.Archetype == null) return;
        Play(leak, "leak", () => Blip(330f, -0.5f, 0.35f, 8f), Mathf.Pow(2f, TierOffset(agent.Archetype.Multiplier) / 12f), false);
    }

    private void PlayStinger(char grade)
    {
        switch (grade)
        {
            case 'S': Play(stingerS, "stingerS", () => Arpeggio(new[] { 0, 4, 7, 12, 16 }), 1f, false); break;
            case 'A': Play(stingerA, "stingerA", () => Arpeggio(new[] { 0, 4, 7, 12 }), 1f, false); break;
            case 'B': Play(stingerB, "stingerB", () => Arpeggio(new[] { 0, 4, 7 }), 1f, false); break;
            case 'C': Play(stingerC, "stingerC", () => Arpeggio(new[] { 0, 7 }), 1f, false); break;
            default: Play(stingerD, "stingerD", () => Arpeggio(new[] { 0, -5 }), 1f, false); break;
        }
    }

    private float BankPitch(int multiplier)
    {
        float fill = round.GlimmersTotal > 0 ? (float)round.GlimmersInPen / round.GlimmersTotal : 0f;
        int degree = Mathf.Clamp(Mathf.RoundToInt(fill * (Scale.Length - 1)), 0, Scale.Length - 1);
        int semitones = Mathf.Min(Scale[degree] + TierOffset(multiplier), 24);
        return Mathf.Pow(2f, semitones / 12f);
    }

    private static int TierOffset(int multiplier)
    {
        return Mathf.RoundToInt(12f * Mathf.Log(Mathf.Max(1, multiplier) / 3f, 2f));
    }

    private float Jitter()
    {
        return Mathf.Pow(2f, Random.Range(-variationCents, variationCents) / 1200f);
    }

    private void StartMusic()
    {
        StopMusic();
        musicStart = AudioSettings.dspTime + 0.15;
        for (int i = 0; i < music.Length; i++)
        {
            musicLevel[i] = 0f;
            music[i].volume = 0f;
            if (music[i].clip != null) music[i].PlayScheduled(musicStart);
        }
        musicRunning = musicBase != null || musicDrive != null || musicTension != null;
    }

    private void StopMusic()
    {
        for (int i = 0; i < music.Length; i++)
        {
            music[i].Stop();
        }
        musicRunning = false;
    }

    private double NextGridTime()
    {
        if (!musicRunning) return 0.0;

        double step = 60.0 / bpm / Mathf.Max(1, subdivisions);
        double now = AudioSettings.dspTime;
        if (now < musicStart) return musicStart;
        return musicStart + System.Math.Ceiling((now - musicStart) / step) * step;
    }

    private void Play(Cue cue, string key, System.Func<float[]> synth, float pitch, bool onGrid)
    {
        AudioClip clip = Pick(cue, key, synth);
        if (clip == null) return;

        AudioSource source = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;
        source.Stop();
        source.clip = clip;
        source.volume = cue.volume * sfxVolume * masterVolume;
        source.pitch = pitch;

        double when = onGrid ? NextGridTime() : 0.0;
        if (when > AudioSettings.dspTime) source.PlayScheduled(when);
        else source.Play();
    }

    private AudioClip Pick(Cue cue, string key, System.Func<float[]> synth)
    {
        if (cue.clips != null && cue.clips.Length > 0)
        {
            int start = Random.Range(0, cue.clips.Length);
            for (int i = 0; i < cue.clips.Length; i++)
            {
                AudioClip candidate = cue.clips[(start + i) % cue.clips.Length];
                if (candidate != null) return candidate;
            }
        }

        if (!fallbacks.TryGetValue(key, out AudioClip clip))
        {
            clip = ToClip(key, synth());
            fallbacks.Add(key, clip);
        }
        return clip;
    }

    private AudioSource MakeSource(string sourceName, bool loop)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    private static int Rate => AudioSettings.outputSampleRate;

    private static float[] Blip(float hz, float glide, float seconds, float decay)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        float[] data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / Rate;
            float frequency = hz * (1f + glide * t / seconds);
            phase += 2f * Mathf.PI * frequency / Rate;
            float attack = Mathf.Min(1f, t / 0.004f);
            float envelope = attack * Mathf.Exp(-t * decay);
            data[i] = 0.6f * envelope * (Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase));
        }
        return data;
    }

    private static float[] Sequence(float[] freqs, float spacing, float noteSeconds, float decay)
    {
        int count = Mathf.CeilToInt((spacing * (freqs.Length - 1) + noteSeconds) * Rate);
        float[] data = new float[count];
        for (int n = 0; n < freqs.Length; n++)
        {
            float[] note = Blip(freqs[n], 0f, noteSeconds, decay);
            int offset = Mathf.RoundToInt(n * spacing * Rate);
            for (int i = 0; i < note.Length && offset + i < count; i++)
            {
                data[offset + i] += note[i];
            }
        }
        return data;
    }

    private static float[] Arpeggio(int[] semitones)
    {
        float[] freqs = new float[semitones.Length];
        for (int i = 0; i < semitones.Length; i++)
        {
            freqs[i] = 330f * Mathf.Pow(2f, semitones[i] / 12f);
        }
        return Sequence(freqs, 0.11f, 0.5f, 6f);
    }

    private static float[] Chord(float[] freqs, float seconds)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / Rate;
            float envelope = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 3f);
            float sum = 0f;
            for (int f = 0; f < freqs.Length; f++)
            {
                sum += Mathf.Sin(2f * Mathf.PI * freqs[f] * t);
            }
            data[i] = 0.5f * envelope * sum / freqs.Length;
        }
        return data;
    }

    private static float[] Whoosh(float seconds)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        float[] data = new float[count];
        float state = 0f;
        for (int i = 0; i < count; i++)
        {
            float hump = Mathf.Sin(Mathf.PI * i / count);
            float coefficient = Mathf.Lerp(0.02f, 0.3f, hump);
            state += coefficient * (Random.Range(-1f, 1f) - state);
            data[i] = state * 3f * Mathf.Pow(hump, 1.5f);
        }
        return data;
    }

    private static AudioClip ToClip(string clipName, float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Mathf.Clamp(data[i], -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}