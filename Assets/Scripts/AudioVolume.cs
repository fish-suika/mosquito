using UnityEngine;

namespace MosquitoGame
{
    /// <summary>音量の設定（全体・羽音・効果音・環境音）。PlayerPrefs に保存される。</summary>
    public static class AudioVolume
    {
        const string Key = "mosquito.vol.";
        static bool loaded;
        static float master = 0.8f, buzz = 0.6f, sfx = 0.7f, ambient = 0.4f;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            master = PlayerPrefs.GetFloat(Key + "master", master);
            buzz = PlayerPrefs.GetFloat(Key + "buzz", buzz);
            sfx = PlayerPrefs.GetFloat(Key + "sfx", sfx);
            ambient = PlayerPrefs.GetFloat(Key + "ambient", ambient);
        }

        public static float Master { get { Load(); return master; } set { master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Key + "master", master); } }
        public static float Buzz { get { Load(); return buzz; } set { buzz = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Key + "buzz", buzz); } }
        public static float Sfx { get { Load(); return sfx; } set { sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Key + "sfx", sfx); } }
        public static float Ambient { get { Load(); return ambient; } set { ambient = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Key + "ambient", ambient); } }

        public static void Save() => PlayerPrefs.Save();
    }
}
