using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Generates all sound effects and the chiptune loop procedurally, so the
    /// project needs no audio assets and still ships with feedback for every action.
    /// </summary>
    public sealed class AudioManager
    {
        const int SampleRate = 44100;

        readonly AudioSource musicSource;
        readonly AudioSource sfxSource;
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioClip musicClip;
        SaveData settings;
        bool ducked;

        public AudioManager(Transform parent, SaveData saveData)
        {
            settings = saveData;

            var root = new GameObject("Audio");
            root.transform.SetParent(parent, false);

            musicSource = root.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

            sfxSource = root.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            BuildClips();
            ApplyVolumes();
        }

        public void RefreshSettings(SaveData data)
        {
            settings = data;
            ApplyVolumes();
        }

        public void Play(Sfx sfx, float volumeScale = 1f)
        {
            if (clips.TryGetValue(sfx, out var clip) && clip != null)
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(settings.sfxVolume * settings.masterVolume * volumeScale));
        }

        public void PlayMusic()
        {
            if (musicClip == null) return;
            if (musicSource.clip != musicClip) musicSource.clip = musicClip;
            if (!musicSource.isPlaying) musicSource.Play();
            ApplyVolumes();
        }

        public void StopMusic() => musicSource.Stop();

        public void SetMusicDucked(bool value)
        {
            ducked = value;
            ApplyVolumes();
        }

        public void ApplyVolumes()
        {
            musicSource.volume = settings.musicVolume * settings.masterVolume * (ducked ? 0.35f : 1f);
            sfxSource.volume = 1f;
        }

        void BuildClips()
        {
            clips[Sfx.Jump] = Sweep("jump", 0.14f, 480f, 780f, Wave.Square, 0.22f);
            clips[Sfx.DoubleJump] = Sweep("double_jump", 0.18f, 700f, 1180f, Wave.Square, 0.2f);
            clips[Sfx.Coin] = Arpeggio("coin", new[] { 1046.5f, 1568f }, 0.075f, Wave.Triangle, 0.22f);
            clips[Sfx.Hurt] = Sweep("hurt", 0.28f, 320f, 80f, Wave.Saw, 0.25f);
            clips[Sfx.Checkpoint] = Arpeggio("checkpoint", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, Wave.Triangle, 0.22f);
            clips[Sfx.Goal] = Arpeggio("goal", new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f }, 0.11f, Wave.Square, 0.18f);
            clips[Sfx.Bounce] = Sweep("bounce", 0.2f, 220f, 900f, Wave.Sine, 0.26f);
            clips[Sfx.Dash] = Noise("dash", 0.16f, 0.18f);
            clips[Sfx.Click] = Sweep("click", 0.05f, 1200f, 900f, Wave.Square, 0.12f);
            clips[Sfx.GameOver] = Arpeggio("game_over", new[] { 392f, 329.63f, 261.63f, 196f }, 0.2f, Wave.Triangle, 0.22f);
            clips[Sfx.Victory] = Arpeggio("victory", new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.14f, Wave.Triangle, 0.2f);
            musicClip = BuildMusic();
        }

        static AudioClip Sweep(string name, float duration, float startFrequency, float endFrequency, Wave wave, float gain)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            double phase = 0.0;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t * t);
                phase += 2.0 * Math.PI * frequency / SampleRate;
                float envelope = Mathf.Exp(-4.5f * t) * Mathf.Min(1f, i / (SampleRate * 0.004f));
                data[i] = Oscillate(wave, phase) * envelope * gain;
            }
            return MakeClip(name, data);
        }

        static AudioClip Arpeggio(string baseName, float[] notes, float noteDuration, Wave wave, float gain)
        {
            float duration = notes.Length * noteDuration + 0.08f;
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * noteDuration * SampleRate);
                int length = Mathf.RoundToInt(noteDuration * SampleRate * 1.6f);
                double phase = 0.0;
                for (int i = 0; i < length && start + i < samples; i++)
                {
                    float t = i / (float)length;
                    phase += 2.0 * Math.PI * notes[n] / SampleRate;
                    float envelope = Mathf.Exp(-3.2f * t) * Mathf.Min(1f, i / (SampleRate * 0.004f));
                    data[start + i] += Oscillate(wave, phase) * envelope * gain;
                }
            }
            return MakeClip(baseName, data);
        }

        static AudioClip Noise(string name, float duration, float gain)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(5);
            float filtered = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, white, 0.22f);
                data[i] = filtered * Mathf.Exp(-5f * t) * gain;
            }
            return MakeClip(name, data);
        }

        static AudioClip BuildMusic()
        {
            const float bpm = 108f;
            float beat = 60f / bpm;
            int totalBeats = 16;
            int samples = Mathf.CeilToInt(totalBeats * beat * SampleRate);
            var data = new float[samples];

            // A minor -> F major -> C major -> G major, four beats each.
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f },
                new[] { 174.61f, 220f, 261.63f },
                new[] { 261.63f, 329.63f, 392f },
                new[] { 196f, 246.94f, 293.66f }
            };
            float[] bassRoots = { 110f, 87.31f, 130.81f, 98f };

            for (int chord = 0; chord < 4; chord++)
            {
                var notes = chords[chord];
                for (int beatIndex = 0; beatIndex < 4; beatIndex++)
                {
                    int globalBeat = chord * 4 + beatIndex;

                    // Bass: triangle, half notes.
                    if (beatIndex % 2 == 0)
                    {
                        AddNote(data, globalBeat * beat, beat * 1.9f, bassRoots[chord], Wave.Triangle, 0.12f);
                        AddNote(data, globalBeat * beat, beat * 1.9f, bassRoots[chord] * 2f, Wave.Sine, 0.05f);
                    }

                    // Arpeggio: two soft triangle plucks per beat.
                    for (int step = 0; step < 2; step++)
                    {
                        float frequency = notes[(beatIndex * 2 + step) % notes.Length] * 2f;
                        AddNote(data, globalBeat * beat + step * beat * 0.5f, beat * 0.55f, frequency, Wave.Triangle, 0.07f);
                    }

                    // A gentle off-beat sparkle.
                    if (globalBeat % 2 == 1)
                        AddNote(data, globalBeat * beat + beat * 0.75f, beat * 0.2f, notes[2] * 4f, Wave.Sine, 0.025f);
                }
            }

            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            return MakeClip("heaven_theme", data);
        }

        static void AddNote(float[] buffer, float startTime, float duration, float frequency, Wave wave, float gain)
        {
            int start = Mathf.RoundToInt(startTime * SampleRate);
            int length = Mathf.RoundToInt(duration * SampleRate);
            double phase = 0.0;
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index < 0 || index >= buffer.Length) break;
                float t = i / (float)length;
                phase += 2.0 * Math.PI * frequency / SampleRate;
                float envelope = Mathf.Min(1f, i / (SampleRate * 0.01f)) * Mathf.Exp(-3.5f * t) * (1f - t * 0.2f);
                buffer[index] += Oscillate(wave, phase) * envelope * gain;
            }
        }

        static float Oscillate(Wave wave, double phase)
        {
            switch (wave)
            {
                case Wave.Square:
                    return Math.Sin(phase) >= 0 ? 0.7f : -0.7f;
                case Wave.Saw:
                    return (float)(2.0 * (phase / (2.0 * Math.PI) % 1.0) - 1.0);
                case Wave.Triangle:
                    return (float)(2.0 * Math.Abs(2.0 * (phase / (2.0 * Math.PI) % 1.0) - 1.0) - 1.0);
                default:
                    return (float)Math.Sin(phase);
            }
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        enum Wave
        {
            Sine,
            Square,
            Triangle,
            Saw
        }
    }
}
