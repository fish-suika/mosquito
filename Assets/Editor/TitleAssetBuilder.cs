using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MosquitoGame.EditorTools
{
    /// <summary>
    /// タイトル画面用の TextMeshPro 素材を作る（日本語フォントのアセットと、ロゴ用のマテリアル）。
    /// メニュー Mosquito/Build Title Assets。SceneBuilder からも呼ばれる。
    /// マテリアルはビルドに入るようエディタで作って保存する（実行時にキーワードを足すとシェーダーの種類が抜けるため）。
    /// </summary>
    public static class TitleAssetBuilder
    {
        public const string FontPath = "Assets/Fonts/NotoSansJP_TMP.asset";
        public const string LogoMatPath = "Assets/Fonts/TitleLogo.mat";
        public const string RedMatPath = "Assets/Fonts/TitleRed.mat";
        public const string SoftMatPath = "Assets/Fonts/TitleSoft.mat";

        // タイトル画面で使う文字（ここにない文字は表示されないので、文言を変えたら足す）
        const string Chars =
            "蚊のステルスゲーム夜の寝室で人間に見つからないように血を吸え" +
            "はじめるEnterキーでも始められます" +
            "操作視点マウス移動WASD上昇Space下降C吸血左クリック長押し肌に着地して回避ダッシュShift右クリックポーズEsc" +
            "遊び方ルール血のメーターを満タンにすればクリアお腹いっぱいで帰れる" +
            "吸う場所が危ないほど多く吸える顔首腕足首点秒" +
            "かゆみ視界羽音気づきが溜まり満タンで見つかる" +
            "見つかったら手で叩いてくるダッシュでよけて物陰に隠れれば逃げ切り叩かれたらゲームオーバー" +
            "音量全体羽音効果音環境音ベストスコアタイム" +
            " 　、。・：／（）「」！？％×+-0123456789.:/" +
            "遊び方設定再開やり直し戻る閉じるポーズ中蚊の横に出ます吸っている間たくさん血危ない場所ほど" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        [MenuItem("Mosquito/Build Title Assets")]
        public static void Build()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP.ttf");
            if (ttf == null) { Debug.LogError("Assets/Fonts/NotoSansJP.ttf がありません"); return; }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(ttf, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                AssetDatabase.CreateAsset(font, FontPath);
                font.material.name = "NotoSansJP_TMP Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                font.atlasTextures[0].name = "NotoSansJP_TMP Atlas";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            }
            // 使う文字をあらかじめ焼き込む（WebGL でも実行時に文字を足さなくて済む）
            font.TryAddCharacters(Chars, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("フォントにない文字: " + missing);
            EditorUtility.SetDirty(font);

            MakeMaterial(font, LogoMatPath, m =>
            {
                m.DisableKeyword("OUTLINE_ON"); m.SetFloat("_OutlineWidth", 0f); m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_OutlineColor", new Color(0.35f, 0.5f, 0.85f, 1f));
                m.SetFloat("_OutlineWidth", 0.10f);
                m.SetColor("_UnderlayColor", new Color(0.2f, 0.35f, 0.9f, 0.85f));
                m.SetFloat("_UnderlayOffsetX", 0f); m.SetFloat("_UnderlayOffsetY", 0f);
                m.SetFloat("_UnderlayDilate", 0.6f); m.SetFloat("_UnderlaySoftness", 0.9f);
            });
            MakeMaterial(font, RedMatPath, m =>
            {
                m.DisableKeyword("OUTLINE_ON"); m.SetFloat("_OutlineWidth", 0f); m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_OutlineColor", new Color(0.45f, 0.02f, 0.05f, 1f));
                m.SetFloat("_OutlineWidth", 0.12f);
                m.SetColor("_UnderlayColor", new Color(1f, 0.1f, 0.15f, 0.9f));
                m.SetFloat("_UnderlayOffsetX", 0f); m.SetFloat("_UnderlayOffsetY", 0f);
                m.SetFloat("_UnderlayDilate", 0.7f); m.SetFloat("_UnderlaySoftness", 0.9f);
            });
            MakeMaterial(font, SoftMatPath, m =>
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0f, 0f, 0.05f, 0.85f));
                m.SetFloat("_UnderlayOffsetX", 0.4f); m.SetFloat("_UnderlayOffsetY", -0.4f);
                m.SetFloat("_UnderlayDilate", 0.3f); m.SetFloat("_UnderlaySoftness", 0.5f);
            });

            AssetDatabase.SaveAssets();
            Debug.Log("タイトル用の TMP 素材を作成しました（文字数 " + Chars.Length + "）");
        }

        static void MakeMaterial(TMP_FontAsset font, string path, System.Action<Material> setup)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = font.material.shader;
            m.SetTexture("_MainTex", font.atlasTexture);
            setup(m);
            EditorUtility.SetDirty(m);
        }
    }
}
