using UnityEngine;

namespace MosquitoGame
{
    /// <summary>音の素材をコードで作る（音声ファイルなし。WebGL でもそのまま動く）。</summary>
    public static class ProceduralAudio
    {
        const int Rate = 22050;

        static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample)
        {
            int n = Mathf.RoundToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i, i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>蚊の羽音（約440Hz＋ゆらぎ）。ループしても継ぎ目が出ない長さ。</summary>
        public static AudioClip Buzz()
        {
            var rnd = new System.Random(1);
            float phase = 0f;
            float noiseLp = 0f;
            return Make("Buzz", 1f, (i, t) =>
            {
                float f = 440f + 25f * Mathf.Sin(2f * Mathf.PI * 6f * t);   // 1秒に6回ゆらぐ＝ちょうど周期が合う
                phase += 2f * Mathf.PI * f / Rate;
                float saw = (phase / (2f * Mathf.PI)) % 1f * 2f - 1f;
                noiseLp += 0.2f * (((float)rnd.NextDouble() * 2f - 1f) - noiseLp);
                return (saw * 0.35f + Mathf.Sin(phase * 2f) * 0.25f + noiseLp * 0.25f) * 0.45f;
            });
        }

        /// <summary>吸血中のごくごく音（低い脈動）。</summary>
        public static AudioClip Gulp()
        {
            return Make("Gulp", 0.8f, (i, t) =>
            {
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 2.5f * t)), 2f);
                return Mathf.Sin(2f * Mathf.PI * (110f + 60f * pulse) * t) * pulse * 0.6f;
            });
        }

        /// <summary>手が振りかぶられる風切り音。</summary>
        public static AudioClip Whoosh()
        {
            var rnd = new System.Random(2);
            float lp = 0f;
            return Make("Whoosh", 0.55f, (i, t) =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.55f));
                float a = Mathf.Lerp(0.03f, 0.35f, t / 0.55f);
                lp += a * (((float)rnd.NextDouble() * 2f - 1f) - lp);
                return lp * env * 1.6f;
            });
        }

        /// <summary>叩く音（ノイズの破裂＋低いドン）。</summary>
        public static AudioClip Slap()
        {
            var rnd = new System.Random(3);
            return Make("Slap", 0.4f, (i, t) =>
            {
                float noise = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 28f);
                float thump = Mathf.Sin(2f * Mathf.PI * 80f * t) * Mathf.Exp(-t * 14f);
                return noise * 0.7f + thump * 0.8f;
            });
        }

        /// <summary>見つかったときの警告音（2音）。</summary>
        public static AudioClip Alert()
        {
            return Make("Alert", 0.45f, (i, t) =>
            {
                float f = t < 0.2f ? 660f : 880f;
                float local = t < 0.2f ? t : t - 0.2f;
                float env = Mathf.Exp(-local * 9f) * Mathf.Clamp01(local * 200f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
            });
        }

        /// <summary>クリアのファンファーレ（ド・ミ・ソ・高いド）。</summary>
        public static AudioClip Clear()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            return Make("Clear", 1.2f, (i, t) =>
            {
                int k = Mathf.Min(3, (int)(t / 0.2f));
                float local = t - k * 0.2f;
                float env = Mathf.Exp(-local * (k == 3 ? 2.5f : 6f)) * Mathf.Clamp01(local * 300f);
                float f = notes[k];
                return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f) * env * 0.5f;
            });
        }

        /// <summary>寝息（ゆっくりふくらむ低いノイズ）。ループ。</summary>
        public static AudioClip Breath()
        {
            var rnd = new System.Random(4);
            float lp = 0f;
            return Make("Breath", 4f, (i, t) =>
            {
                float env = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 4f);
                lp += 0.06f * (((float)rnd.NextDouble() * 2f - 1f) - lp);
                return lp * env * 2.2f;
            });
        }

        /// <summary>遠くの虫の声（夜の静けさ）。ループ。</summary>
        public static AudioClip Crickets()
        {
            return Make("Crickets", 2f, (i, t) =>
            {
                float local = t % 0.5f;
                float gate = local < 0.3f ? (Mathf.Sin(2f * Mathf.PI * 22f * local) > 0f ? 1f : 0f) : 0f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(local / 0.3f));
                return Mathf.Sin(2f * Mathf.PI * 4300f * t) * gate * env * 0.25f;
            });
        }
    }
}
