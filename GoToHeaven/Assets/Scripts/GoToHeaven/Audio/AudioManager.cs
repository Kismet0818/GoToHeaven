using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 程序化生成全部音效与芯片音乐循环，项目因此不需要任何音频资源，
    /// 却仍能为每个动作配上即时反馈。
    /// </summary>
    public sealed class AudioManager
    {
        const int SampleRate = 44100; // 所有片段共用的采样率，时长与相位的换算都以它为基准

        readonly AudioSource musicSource;
        readonly AudioSource sfxSource;
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>(); // 片段只生成一次，之后按枚举查表
        AudioClip musicClip;
        SaveData settings;
        bool ducked; // 过场/对白期间压低音乐，结束后恢复

        /// <summary>创建音频根节点与两条 AudioSource，并一次性生成所有片段。</summary>
        public AudioManager(Transform parent, SaveData saveData)
        {
            settings = saveData;

            var root = new GameObject("Audio");
            root.transform.SetParent(parent, false); // false 表示保留本地变换，别被父级当前的世界位置带偏

            musicSource = root.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f; // 纯 2D 音源，不随镜头距离衰减

            sfxSource = root.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            BuildClips();
            ApplyVolumes();
        }

        /// <summary>设置变更后重新套用音量；不重建片段，省掉重新合成的开销。</summary>
        public void RefreshSettings(SaveData data)
        {
            settings = data;
            ApplyVolumes();
        }

        /// <summary>播放一次性音效，音量在内部乘上主音量与音效音量，调用方只给相对系数。</summary>
        public void Play(Sfx sfx, float volumeScale = 1f)
        {
            // 查不到片段就静默跳过：缺音效不该让玩法逻辑抛异常。
            if (clips.TryGetValue(sfx, out var clip) && clip != null)
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(settings.sfxVolume * settings.masterVolume * volumeScale));
        }

        /// <summary>开始播放背景音乐；已在播放时不重启，避免循环被打断。</summary>
        public void PlayMusic()
        {
            if (musicClip == null) return;
            if (musicSource.clip != musicClip) musicSource.clip = musicClip;
            if (!musicSource.isPlaying) musicSource.Play();
            ApplyVolumes();
        }

        /// <summary>停止背景音乐。</summary>
        public void StopMusic() => musicSource.Stop();

        /// <summary>进/出过场时压低或恢复音乐音量。</summary>
        public void SetMusicDucked(bool value)
        {
            ducked = value;
            ApplyVolumes();
        }

        /// <summary>统一计算两条通道的最终音量，所有设置或 duck 变化都收敛到这里。</summary>
        public void ApplyVolumes()
        {
            // SFX 的实际音量在 Play 里逐次相乘，这里固定为 1，避免重复衰减。
            musicSource.volume = settings.musicVolume * settings.masterVolume * (ducked ? 0.35f : 1f);
            sfxSource.volume = 1f;
        }

        void BuildClips()
        {
            // 一个动作一个片段；名字同时用作资源名，方便在 Profiler 与调试器里辨认。
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

        /// <summary>生成一段扫频音（跳跃、受伤、弹跳等），用于短促的动作反馈。</summary>
        static AudioClip Sweep(string name, float duration, float startFrequency, float endFrequency, Wave wave, float gain)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            double phase = 0.0;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                // t*t 让频率变化先慢后快：起音柔和、收尾“嗖”地滑走，比线性更像个动作。
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t * t);
                // 累加相位而不是直接算 sin(2πft)：频率在变，累加才保证波形连续、不爆音。
                phase += 2.0 * Math.PI * frequency / SampleRate;
                // 指数衰减做尾音，再叠 4ms 淡入，消除起始瞬间的“啪”声。
                float envelope = Mathf.Exp(-4.5f * t) * Mathf.Min(1f, i / (SampleRate * 0.004f));
                data[i] = Oscillate(wave, phase) * envelope * gain;
            }
            return MakeClip(name, data);
        }

        /// <summary>按顺序叠加若干音符（金币、检查点、胜利等），后一个音符压在前一个的尾音上。</summary>
        static AudioClip Arpeggio(string baseName, float[] notes, float noteDuration, Wave wave, float gain)
        {
            float duration = notes.Length * noteDuration + 0.08f; // 末尾多留 80ms，容纳最后一个音的衰减尾巴
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * noteDuration * SampleRate);
                // 长度取 1.6 倍音符时长：让相邻音符的尾音重叠，听感连贯而不是断奏。
                int length = Mathf.RoundToInt(noteDuration * SampleRate * 1.6f);
                double phase = 0.0;
                for (int i = 0; i < length && start + i < samples; i++)
                {
                    float t = i / (float)length;
                    phase += 2.0 * Math.PI * notes[n] / SampleRate;
                    float envelope = Mathf.Exp(-3.2f * t) * Mathf.Min(1f, i / (SampleRate * 0.004f));
                    data[start + i] += Oscillate(wave, phase) * envelope * gain; // 用 += 叠加，尾音自然交叠
                }
            }
            return MakeClip(baseName, data);
        }

        /// <summary>生成低通白噪声（冲刺用），比扫频音更有“风”的质感。</summary>
        static AudioClip Noise(string name, float duration, float gain)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(5); // 固定种子：噪声每次完全一致，听感与调试都可复现
            float filtered = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, white, 0.22f); // 单极点低通：磨掉刺耳高频，只留气流声
                data[i] = filtered * Mathf.Exp(-5f * t) * gain;
            }
            return MakeClip(name, data);
        }

        /// <summary>合成 16 拍（约 8.9 秒）的芯片音乐循环。</summary>
        static AudioClip BuildMusic()
        {
            const float bpm = 108f; // 108 BPM：慢到能长时间循环不烦躁，又不至于拖沓
            float beat = 60f / bpm;
            int totalBeats = 16;
            int samples = Mathf.CeilToInt(totalBeats * beat * SampleRate);
            var data = new float[samples];

            // 和弦进行：A 小调 -> F 大调 -> C 大调 -> G 大调，每个和弦四拍。
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

                    // 低音：三角波，二分音符（隔拍触发），垫住和声的根音。
                    if (beatIndex % 2 == 0)
                    {
                        AddNote(data, globalBeat * beat, beat * 1.9f, bassRoots[chord], Wave.Triangle, 0.12f);
                        AddNote(data, globalBeat * beat, beat * 1.9f, bassRoots[chord] * 2f, Wave.Sine, 0.05f);
                    }

                    // 琶音：每拍两下轻柔的三角波拨弦，铺出旋律的流动感。
                    for (int step = 0; step < 2; step++)
                    {
                        float frequency = notes[(beatIndex * 2 + step) % notes.Length] * 2f;
                        AddNote(data, globalBeat * beat + step * beat * 0.5f, beat * 0.55f, frequency, Wave.Triangle, 0.07f);
                    }

                    // 高两个八度的弱拍点缀，让循环不显得单调。
                    if (globalBeat % 2 == 1)
                        AddNote(data, globalBeat * beat + beat * 0.75f, beat * 0.2f, notes[2] * 4f, Wave.Sine, 0.025f);
                }
            }

            // 多个声部直接相加会超过 0dB，这里统一钳到 [-1,1]，避免削波失真。
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            return MakeClip("heaven_theme", data);
        }

        /// <summary>把一个音符叠加进缓冲区；越界的尾部直接丢弃，供音乐各声部复用。</summary>
        static void AddNote(float[] buffer, float startTime, float duration, float frequency, Wave wave, float gain)
        {
            int start = Mathf.RoundToInt(startTime * SampleRate);
            int length = Mathf.RoundToInt(duration * SampleRate);
            double phase = 0.0;
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index < 0 || index >= buffer.Length) break; // 跨过缓冲区端点就收手，绝不写到界外
                float t = i / (float)length;
                phase += 2.0 * Math.PI * frequency / SampleRate;
                // 10ms 淡入避免爆音，指数衰减模拟拨弦，末尾再轻微衰减让尾音更自然。
                float envelope = Mathf.Min(1f, i / (SampleRate * 0.01f)) * Mathf.Exp(-3.5f * t) * (1f - t * 0.2f);
                buffer[index] += Oscillate(wave, phase) * envelope * gain;
            }
        }

        static float Oscillate(Wave wave, double phase)
        {
            // 用相位取模直接合成波形：不查表、不分配内存，生成几百毫秒的片段也够快。
            switch (wave)
            {
                case Wave.Square:
                    // 方波取 sin 的符号并压到 0.7 幅度，听感接近芯片音又不会太刺耳。
                    return Math.Sin(phase) >= 0 ? 0.7f : -0.7f;
                case Wave.Saw:
                    return (float)(2.0 * (phase / (2.0 * Math.PI) % 1.0) - 1.0);
                case Wave.Triangle:
                    return (float)(2.0 * Math.Abs(2.0 * (phase / (2.0 * Math.PI) % 1.0) - 1.0) - 1.0);
                default:
                    return (float)Math.Sin(phase);
            }
        }

        /// <summary>把采样数组封装成运行时 AudioClip；streaming=false，整段常驻内存。</summary>
        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 振荡器类型，决定同一段旋律的音色。
        enum Wave
        {
            Sine,
            Square,
            Triangle,
            Saw
        }
    }
}
