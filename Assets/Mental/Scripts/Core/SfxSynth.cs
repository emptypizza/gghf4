// ============================================================
// Mental — 절차 오디오 신스 (원본 case3.html 의 WebAudio Audio 모듈 포팅)
// 저작권 음원 없음: 효과음·스코어 전부 실시간 합성.
// AudioListener 가 있는 GO 에 붙는다 (OnAudioFilterRead 스트림 합성).
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace Mental
{
    public class SfxSynth : MonoBehaviour
    {
        public enum Wave { Sine, Square, Saw, Triangle, Noise }

        class Voice
        {
            public Wave wave;
            public double freq, freqEnd;     // freqEnd>0 이면 dur 동안 선형 슬라이드
            public double gain;
            public long start, durSamples;
            public double phase;
            public double decayMul;          // 지수 감쇠 per-sample
            public double amp;               // 현재 엔벨로프 값
            public long attackSamples;
            public double hpA, hpPrevIn, hpPrevOut; // 노이즈 하이패스
            public uint rng = 22222;
            public bool score;               // 스코어 버스 여부
        }

        readonly List<Voice> _voices = new List<Voice>();
        readonly object _lock = new object();
        long _sample;
        int _sampleRate = 48000;
        bool _muted;
        bool _unlocked;

        GameConfig _cfg;
        ScoreMood _mood;
        bool _scoreOn;
        int _scoreStep;
        long _nextStepSample;
        AudioSource _unlockSrc;

        public bool Muted => _muted;
        public bool Unlocked => _unlocked;
        public string MoodName => _mood != null ? _mood.name : "";

        void Awake()
        {
            _cfg = GameConfig.Instance;
            _sampleRate = AudioSettings.outputSampleRate;
        }

        // WebGL: 브라우저 AudioContext 는 유저 제스처 전까지 suspend.
        // 무음 클립 PlayOneShot 으로 resume (언어 게이트/케이스 시작 클릭에서 호출).
        // 언락용 AudioSource 는 반드시 별도 GO 에 둔다 — 이 GO 에는 OnAudioFilterRead 가
        // AudioListener 에 바인딩돼 있어, 같은 GO 에 AudioSource 를 추가하면
        // "multiple AudioSources/AudioListeners" 경고와 함께 필터 바인딩이 깨진다.
        public void UnlockAudio()
        {
            if (_unlocked) return;
            _unlocked = true;
            if (_unlockSrc == null)
            {
                var go = new GameObject("SfxUnlock");
                go.transform.SetParent(transform, false);
                _unlockSrc = go.AddComponent<AudioSource>();
                _unlockSrc.playOnAwake = false;
                _unlockSrc.spatialBlend = 0f;
            }
            AudioClip clip = AudioClip.Create("mental_unlock", 256, 1, 22050, false);
            clip.SetData(new float[256], 0);
            _unlockSrc.PlayOneShot(clip, 0.001f);
        }

        // ---------------- 스코어(무드 BGM) ----------------
        public void SetMood(string name)
        {
            ScoreMood m = _cfg.Mood(name);
            if (m == null) return;
            lock (_lock)
            {
                _mood = m;
                _scoreStep = 0;
                if (_nextStepSample < _sample) _nextStepSample = _sample + _sampleRate / 20;
            }
        }

        public void StartScore() { lock (_lock) { _scoreOn = true; if (_nextStepSample < _sample) _nextStepSample = _sample; } }
        public void StopScore() { lock (_lock) { _scoreOn = false; } }
        public bool ToggleMute() { _muted = !_muted; return _muted; }

        void Update()
        {
            if (!_scoreOn || _mood == null || _muted) return;
            long now, until;
            lock (_lock) { now = _sample; until = now + (long)(_sampleRate * 0.5f); }
            while (true)
            {
                long stepAt;
                ScoreMood m;
                int idx;
                lock (_lock)
                {
                    if (_nextStepSample >= until) break;
                    stepAt = _nextStepSample;
                    m = _mood; idx = _scoreStep;
                    _nextStepSample += (long)(m.step * _sampleRate);
                    _scoreStep++;
                }
                ScheduleStep(m, idx, stepAt);
            }
        }

        void ScheduleStep(ScoreMood m, int stepIdx, long at)
        {
            int i = stepIdx % 8;
            float lv = m.level * _cfg.scoreVolume;
            int b = m.bass[i % m.bass.Length];
            if (b != ScoreMood.REST)
                AddVoice(Wave.Triangle, m.root * Mathf.Pow(2f, b / 12f), 0, m.step * 2.2, .95 * lv, at, true);
            int l = m.lead[i % m.lead.Length];
            if (l != ScoreMood.REST)
                AddVoice(Wave.Square, m.root * 2f * Mathf.Pow(2f, l / 12f), 0, m.step * 1.15, .5 * lv, at, true);
            if (m.hit[i % m.hit.Length] == 1)
                AddNoise(m.step * 0.35, .55 * lv, 3200, at, true);
        }

        // ---------------- 보이스 생성 ----------------
        long Now() { lock (_lock) return _sample; }
        long At(double delaySec) { return Now() + (long)(delaySec * _sampleRate); }

        void AddVoice(Wave w, double freq, double freqEnd, double dur, double gain, long at, bool score = false)
        {
            Voice v = new Voice
            {
                wave = w, freq = freq, freqEnd = freqEnd, gain = gain,
                start = at, durSamples = (long)(dur * _sampleRate),
                attackSamples = (long)(0.008 * _sampleRate), amp = 0, score = score
            };
            if (v.durSamples < 8) v.durSamples = 8;
            v.decayMul = System.Math.Exp(System.Math.Log(1e-4) / v.durSamples);
            lock (_lock) _voices.Add(v);
        }

        void AddNoise(double dur, double gain, double hpFreq, long at, bool score = false)
        {
            Voice v = new Voice
            {
                wave = Wave.Noise, gain = gain,
                start = at, durSamples = (long)(dur * _sampleRate),
                attackSamples = (long)(0.002 * _sampleRate), amp = 0, score = score
            };
            if (v.durSamples < 8) v.durSamples = 8;
            v.decayMul = System.Math.Exp(System.Math.Log(1e-4) / v.durSamples);
            double rc = 1.0 / (2.0 * System.Math.PI * System.Math.Max(20.0, hpFreq));
            double dt = 1.0 / _sampleRate;
            v.hpA = rc / (rc + dt);
            lock (_lock) _voices.Add(v);
        }

        public void Tone(double f, double dur, Wave w, double g, double slideTo = 0, double delay = 0)
            => AddVoice(w, f, slideTo, dur, g * _cfg.sfxVolume, At(delay));
        public void NoiseHit(double dur, double g, double hp, double delay = 0)
            => AddNoise(dur, g * _cfg.sfxVolume, hp, At(delay));

        // ---------------- 원본 SFX 레시피 (case3.html Audio.*) ----------------
        public void Tap() { Tone(520, .045, Wave.Square, .09); }
        public void Page() { NoiseHit(.12, .18, 2400); Tone(860, .05, Wave.Triangle, .06); }
        public void Ding()
        {
            Tone(740, .09, Wave.Triangle, .18);
            Tone(1100, .11, Wave.Sine, .16, 0, .062);
            Tone(1480, .16, Wave.Sine, .13, 0, .124);
        }
        public void Buzz()
        {
            Tone(170, .22, Wave.Saw, .25, 82);
            Tone(118, .18, Wave.Square, .16, 64, .105);
        }
        public void Gavel()
        {
            NoiseHit(.07, .48, 360); Tone(105, .11, Wave.Square, .24, 58);
            NoiseHit(.055, .28, 260, .082); Tone(74, .11, Wave.Square, .18, 46, .082);
        }
        public void Objection()
        {
            Tone(392, .12, Wave.Saw, .22, 280);
            Tone(294, .18, Wave.Saw, .2, 196, .064);
            NoiseHit(.05, .12, 1600);
        }
        public void Win()
        {
            double[][] seq = { new double[]{440,0}, new double[]{554,.095}, new double[]{659,.19}, new double[]{880,.3}, new double[]{1108,.42} };
            foreach (double[] s in seq) Tone(s[0], .36, Wave.Triangle, .22, 0, s[1]);
        }
        public void Lose()
        {
            Tone(294, .4, Wave.Saw, .24, 138);
            Tone(174, .48, Wave.Square, .2, 82, .16);
        }
        public void ScaleChime() { Tone(587, .08, Wave.Sine, .12); Tone(784, .09, Wave.Sine, .1, 0, .048); }

        // ---------------- 오디오 스레드 ----------------
        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (_lock)
            {
                int frames = data.Length / channels;
                double master = _muted ? 0 : _cfg.masterVolume;
                for (int f = 0; f < frames; f++)
                {
                    double mix = 0;
                    for (int i = _voices.Count - 1; i >= 0; i--)
                    {
                        Voice v = _voices[i];
                        long t = _sample - v.start;
                        if (t < 0) continue;
                        if (t >= v.durSamples) { _voices.RemoveAt(i); continue; }

                        // 엔벨로프: 어택 후 지수 감쇠
                        if (t < v.attackSamples) v.amp = v.gain * ((double)t / v.attackSamples);
                        else if (t == v.attackSamples || v.amp <= 0) { v.amp = v.gain; }
                        else v.amp *= v.decayMul;

                        double s;
                        if (v.wave == Wave.Noise)
                        {
                            v.rng = v.rng * 1664525u + 1013904223u;
                            double white = ((v.rng >> 9) * (1.0 / 4194304.0)) - 1.0;
                            double hp = v.hpA * (v.hpPrevOut + white - v.hpPrevIn);
                            v.hpPrevIn = white; v.hpPrevOut = hp;
                            s = hp;
                        }
                        else
                        {
                            double fr = v.freq;
                            if (v.freqEnd > 0) fr = v.freq + (v.freqEnd - v.freq) * ((double)t / v.durSamples);
                            v.phase += fr / _sampleRate;
                            if (v.phase >= 1.0) v.phase -= 1.0;
                            double p = v.phase;
                            switch (v.wave)
                            {
                                case Wave.Square: s = p < .5 ? .6 : -.6; break;
                                case Wave.Saw: s = (p * 2.0 - 1.0) * .7; break;
                                case Wave.Triangle: s = (p < .5 ? (p * 4.0 - 1.0) : (3.0 - p * 4.0)) * .8; break;
                                default: s = System.Math.Sin(p * System.Math.PI * 2.0); break;
                            }
                        }
                        mix += s * v.amp;
                    }

                    float outv = (float)(mix * master);
                    if (outv > 1f) outv = 1f; else if (outv < -1f) outv = -1f;
                    for (int c = 0; c < channels; c++) data[f * channels + c] += outv;
                    _sample++;
                }
            }
        }
    }
}
