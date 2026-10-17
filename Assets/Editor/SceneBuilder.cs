using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MosquitoGame.EditorTools
{
    /// <summary>
    /// メニュー Mosquito/Build Main Scene で、部屋・人形・蚊・カメラを含む Main シーンを作り直す。
    ///
    /// 人間は「当たり判定（Hitboxes、見た目なし）」と「見た目（Model）」を分けて作る。
    /// Blender 製などの別モデルに差し替えるときは、Human/YawPivot/Upper/Model と Human/ModelLegs の中身を
    /// 入れ替え、Human コンポーネントの eyes / shoulders を付け直す（なくても動く）。
    /// 当たり判定・BodyPart・head（HeadAnchor）・yawPivot・upper はそのまま使う。
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string MatDir = "Assets/Materials";
        const string TexDir = "Assets/Textures";
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("Mosquito/Build Main Scene")]
        public static void Build()
        {
            foreach (var d in new[] { "Assets/Scenes", MatDir, TexDir })
                if (!AssetDatabase.IsValidFolder(d)) AssetDatabase.CreateFolder("Assets", Path.GetFileName(d));
            mats.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.09f);
            RenderSettings.fogDensity = 0.006f;

            BuildLights();
            BuildRoom();
            BuildFurniture();
            var human = BuildHuman();
            var mosquito = BuildMosquito(human, out var cam);

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.mosquito = mosquito;
            gm.human = human;

            var fb = new GameObject("GameFeedback").AddComponent<GameFeedback>();
            fb.mosquito = mosquito;
            fb.human = human;
            fb.cam = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Main シーンを作成しました: " + ScenePath);
        }

        // ---- 素材 ----

        static Material Mat(string name, Color c, bool unlit = false, Texture2D tex = null, Vector2? tiling = null)
        {
            if (mats.TryGetValue(name, out var m)) return m;
            string path = MatDir + "/" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = unlit ? Shader.Find("Unlit/Color") : Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            if (tex != null) { m.mainTexture = tex; m.mainTextureScale = tiling ?? Vector2.one; }
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Texture2D MakeTex(string name, int w, int h, System.Func<int, int, Color> f)
        {
            string path = TexDir + "/" + name + ".png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels(px);
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D FloorTexture() => MakeTex("FloorPlanks", 512, 512, (x, y) =>
        {
            int plank = x / 64;
            float tint = 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(plank * 12.9898f) * 43758.5453f % 1f);
            float grain = 0.9f + 0.2f * Mathf.PerlinNoise(x * 0.03f + plank * 7f, y * 0.35f);
            float seam = (x % 64 < 2 || (y + plank * 97) % 256 < 2) ? 0.45f : 1f;
            float v = tint * grain * seam;
            return new Color(0.95f * v, 0.9f * v, 0.85f * v, 1f);
        });

        static Texture2D WallTexture() => MakeTex("WallStripes", 128, 128, (x, y) =>
        {
            float v = (x / 16) % 2 == 0 ? 1f : 0.93f;
            v *= 0.97f + 0.06f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
            return new Color(v, v, v, 1f);
        });

        // ---- 部品づくり（位置はワールド座標で渡す）----

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            if (parent != null) go.transform.SetParent(parent, true);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider = true)
            => Prim(PrimitiveType.Cube, name, parent, pos, size, Quaternion.identity, m, collider);

        static GameObject Sphere(string name, Transform parent, Vector3 pos, float radius, Material m, bool collider = true)
            => Prim(PrimitiveType.Sphere, name, parent, pos, Vector3.one * radius * 2f, Quaternion.identity, m, collider);

        /// <summary>楕円体（見た目だけ）。size は各軸の全長。</summary>
        static GameObject Ellipsoid(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
            => Prim(PrimitiveType.Sphere, name, parent, pos, size, Quaternion.identity, m, false);

        /// <summary>X 軸方向に伸びたカプセル（見た目だけ）。scaleYZ: Y 方向・Z 方向の太さの倍率</summary>
        static GameObject CapsuleXVisual(string name, Transform parent, Vector3 pos, float radius, float length, Material m, float thickY = 1f, float thickZ = 1f)
            => Prim(PrimitiveType.Capsule, name, parent, pos, new Vector3(radius * 2f * thickY, length * 0.5f, radius * 2f * thickZ), Quaternion.Euler(0f, 0f, 90f), m, false);

        // 当たり判定だけ（見た目なし）
        static GameObject HitBox(string name, Transform parent, Vector3 pos, Vector3 size, PartType? part = null)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.SetParent(parent, true);
            go.AddComponent<BoxCollider>().size = size;
            if (part.HasValue) go.AddComponent<BodyPart>().type = part.Value;
            return go;
        }

        static GameObject HitSphere(string name, Transform parent, Vector3 pos, float radius, PartType? part = null)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.SetParent(parent, true);
            go.AddComponent<SphereCollider>().radius = radius;
            if (part.HasValue) go.AddComponent<BodyPart>().type = part.Value;
            return go;
        }

        static GameObject HitCapsuleX(string name, Transform parent, Vector3 pos, float radius, float length, PartType? part = null)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.SetParent(parent, true);
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 0; c.radius = radius; c.height = length;
            if (part.HasValue) go.AddComponent<BodyPart>().type = part.Value;
            return go;
        }

        // ---- シーン ----

        static void BuildLights()
        {
            // 月明かり：窓から差す青い光（影つき）と、弱い全体の青み
            var window = new GameObject("MoonThroughWindow").AddComponent<Light>();
            window.type = LightType.Spot;
            window.color = new Color(0.55f, 0.68f, 1f);
            window.intensity = 3.2f;
            window.range = 70f;
            window.spotAngle = 75f;
            window.shadows = LightShadows.Soft;
            window.transform.position = new Vector3(28.5f, 15f, -6f);
            window.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 2f, 1f) - window.transform.position);

            var fill = new GameObject("MoonFill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.45f, 0.55f, 0.9f);
            fill.intensity = 0.22f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(60f, 120f, 0f);

            // ナイトスタンドの暖かい灯り
            var lamp = new GameObject("NightLampLight").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.72f, 0.4f);
            lamp.intensity = 2.2f;
            lamp.range = 30f;
            lamp.shadows = LightShadows.Soft;
            lamp.transform.position = new Vector3(-12.5f, 7.2f, 8.5f);
        }

        static void BuildRoom()
        {
            var room = new GameObject("Room").transform;
            var floor = Mat("Floor", new Color(0.62f, 0.5f, 0.42f), false, FloorTexture(), new Vector2(4f, 3f));
            var wall = Mat("Wall", new Color(0.52f, 0.52f, 0.6f), false, WallTexture(), new Vector2(14f, 3f));
            var trim = Mat("Trim", new Color(0.2f, 0.17f, 0.14f));
            Box("Floor", room, new Vector3(0f, -0.5f, 0f), new Vector3(61f, 1f, 41f), floor);
            Box("Ceiling", room, new Vector3(0f, 26.5f, 0f), new Vector3(61f, 1f, 41f), Mat("Ceiling", new Color(0.4f, 0.4f, 0.46f)));
            Box("WallE", room, new Vector3(30.5f, 13f, 0f), new Vector3(1f, 28f, 42f), wall);
            Box("WallW", room, new Vector3(-30.5f, 13f, 0f), new Vector3(1f, 28f, 42f), wall);
            Box("WallN", room, new Vector3(0f, 13f, 20.5f), new Vector3(62f, 28f, 1f), wall);
            Box("WallS", room, new Vector3(0f, 13f, -20.5f), new Vector3(62f, 28f, 1f), wall);

            // 巾木（見た目だけ）
            Box("TrimN", room, new Vector3(0f, 0.6f, 19.85f), new Vector3(60f, 1.2f, 0.3f), trim, false);
            Box("TrimS", room, new Vector3(0f, 0.6f, -19.85f), new Vector3(60f, 1.2f, 0.3f), trim, false);
            Box("TrimE", room, new Vector3(29.85f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 40f), trim, false);
            Box("TrimW", room, new Vector3(-29.85f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 40f), trim, false);

            // 窓（月明かり）と窓枠
            var glass = Mat("Window", new Color(0.55f, 0.7f, 1f), true);
            Box("Window", room, new Vector3(29.9f, 15f, -6f), new Vector3(0.2f, 9f, 8f), glass, false);
            Box("FrameT", room, new Vector3(29.75f, 19.8f, -6f), new Vector3(0.45f, 0.6f, 9.2f), trim, false);
            Box("FrameB", room, new Vector3(29.75f, 10.2f, -6f), new Vector3(0.45f, 0.6f, 9.2f), trim, false);
            Box("FrameL", room, new Vector3(29.75f, 15f, -10.2f), new Vector3(0.45f, 9.6f, 0.6f), trim, false);
            Box("FrameR", room, new Vector3(29.75f, 15f, -1.8f), new Vector3(0.45f, 9.6f, 0.6f), trim, false);
            Box("FrameMid", room, new Vector3(29.75f, 15f, -6f), new Vector3(0.4f, 9f, 0.35f), trim, false);

            // ドア・絵・ラグ（見た目だけ）
            Box("Door", room, new Vector3(-12f, 6.2f, -19.8f), new Vector3(7f, 12.4f, 0.3f), Mat("Door", new Color(0.32f, 0.2f, 0.12f)), false);
            Sphere("DoorKnob", room, new Vector3(-9.4f, 6f, -19.5f), 0.35f, Mat("Brass", new Color(0.8f, 0.65f, 0.25f)), false);
            Box("PictureFrame", room, new Vector3(8f, 14f, 19.8f), new Vector3(9f, 6f, 0.25f), trim, false);
            Box("Picture", room, new Vector3(8f, 14f, 19.65f), new Vector3(8f, 5f, 0.1f), Mat("Picture", new Color(0.3f, 0.5f, 0.45f)), false);
            Box("Rug", room, new Vector3(-1.5f, 0.06f, 0f), new Vector3(30f, 0.12f, 20f), Mat("Rug", new Color(0.42f, 0.14f, 0.16f)), false);
        }

        static void BuildFurniture()
        {
            var f = new GameObject("Furniture").transform;
            var wood = Mat("Wood", new Color(0.42f, 0.28f, 0.17f));
            var sheet = Mat("Sheet", new Color(0.82f, 0.82f, 0.86f));
            var blanket = Mat("Blanket", new Color(0.24f, 0.34f, 0.6f));
            var pillow = Mat("Pillow", new Color(0.92f, 0.92f, 0.94f));

            // ベッド（x -13.5〜10.5、頭が -x 側）。下は高さ 2.4 の空間があり隠れられる
            foreach (float sx in new[] { -11.5f, 8.5f })
                foreach (float sz in new[] { -5.2f, 5.2f })
                    Box("BedLeg", f, new Vector3(sx, 1.2f, sz), new Vector3(0.9f, 2.4f, 0.9f), wood);
            Box("BedFrame", f, new Vector3(-1.5f, 2.7f, 0f), new Vector3(24f, 0.6f, 12f), wood);
            Box("Mattress", f, new Vector3(-1.5f, 3.5f, 0f), new Vector3(24f, 1f, 11.6f), sheet);
            Box("Headboard", f, new Vector3(-13.9f, 6f, 0f), new Vector3(0.8f, 7f, 12f), wood);
            Box("Pillow", f, new Vector3(-10.5f, 4.7f, 0f), new Vector3(4.5f, 1.4f, 7f), pillow);
            Box("Blanket", f, new Vector3(-1f, 5.3f, 0f), new Vector3(11f, 2.6f, 8.6f), blanket);
            Box("BlanketFold", f, new Vector3(-6.2f, 6.2f, 0f), new Vector3(1.4f, 0.9f, 8.8f), Mat("BlanketFold", new Color(0.82f, 0.82f, 0.86f)), false);

            // 机・椅子・ノート PC
            Box("DeskTop", f, new Vector3(20f, 7f, -14f), new Vector3(10f, 0.8f, 5f), wood);
            foreach (float dx in new[] { -4.4f, 4.4f })
                foreach (float dz in new[] { -2f, 2f })
                    Box("DeskLeg", f, new Vector3(20f + dx, 3.5f, -14f + dz), new Vector3(0.8f, 7f, 0.8f), wood);
            Box("Laptop", f, new Vector3(20f, 7.5f, -14.3f), new Vector3(3.4f, 0.2f, 2.4f), Mat("Laptop", new Color(0.12f, 0.12f, 0.14f)), false);
            Box("LaptopScreen", f, new Vector3(20f, 8.8f, -15.4f), new Vector3(3.4f, 2.4f, 0.12f), Mat("Screen", new Color(0.25f, 0.35f, 0.5f), true), false);
            var chair = Mat("Chair", new Color(0.2f, 0.2f, 0.25f));
            Box("ChairSeat", f, new Vector3(20f, 4f, -8.5f), new Vector3(4f, 0.6f, 4f), chair);
            Box("ChairBack", f, new Vector3(20f, 6.8f, -6.7f), new Vector3(4f, 5f, 0.5f), chair);
            foreach (float dx in new[] { -1.6f, 1.6f })
                foreach (float dz in new[] { -1.6f, 1.6f })
                    Box("ChairLeg", f, new Vector3(20f + dx, 1.85f, -8.5f + dz), new Vector3(0.5f, 3.7f, 0.5f), chair);

            // 衣装だんす・ナイトスタンド・カーテン
            Box("Wardrobe", f, new Vector3(-25f, 9f, -15f), new Vector3(6f, 18f, 8f), Mat("Wardrobe", new Color(0.34f, 0.22f, 0.13f)));
            Box("WardrobeDoorLine", f, new Vector3(-21.95f, 9f, -15f), new Vector3(0.12f, 17f, 0.15f), Mat("Line", new Color(0.1f, 0.07f, 0.05f)), false);
            Box("Nightstand", f, new Vector3(-12.5f, 2.5f, 8.5f), new Vector3(4f, 5f, 4f), wood);
            Box("LampBase", f, new Vector3(-12.5f, 5.3f, 8.5f), new Vector3(1.4f, 0.6f, 1.4f), chair, false);
            Sphere("NightLamp", f, new Vector3(-12.5f, 6.6f, 8.5f), 1.3f, Mat("Lamp", new Color(1f, 0.85f, 0.55f), true), false);
            Box("Curtain", f, new Vector3(29.3f, 11f, 8f), new Vector3(0.6f, 18f, 9f), Mat("Curtain", new Color(0.5f, 0.14f, 0.18f)));
            Box("CurtainRod", f, new Vector3(29.3f, 20.4f, 3f), new Vector3(0.3f, 0.3f, 22f), chair, false);
        }

        static Human BuildHuman()
        {
            var root = new GameObject("Human");
            var human = root.AddComponent<Human>();
            var skin = Mat("Skin", new Color(0.93f, 0.74f, 0.62f));
            var skinDark = Mat("SkinDark", new Color(0.85f, 0.6f, 0.5f));
            var shirt = Mat("Shirt", new Color(0.3f, 0.55f, 0.5f));
            var pants = Mat("Pants", new Color(0.25f, 0.3f, 0.5f));
            var hair = Mat("Hair", new Color(0.12f, 0.09f, 0.07f));
            var white = Mat("EyeWhite", new Color(0.95f, 0.95f, 0.95f));
            var dark = Mat("EyeDark", new Color(0.05f, 0.04f, 0.04f));

            var yawPivot = new GameObject("YawPivot").transform;
            yawPivot.SetParent(root.transform, false);
            yawPivot.position = new Vector3(-1f, 5.2f, 0f);
            var upper = new GameObject("Upper").transform;
            upper.SetParent(yawPivot, false);

            // ---- 当たり判定（見た目なし）----
            var hit = new GameObject("Hitboxes").transform;
            hit.SetParent(upper, true);
            HitBox("Torso", hit, new Vector3(-4f, 5.2f, 0f), new Vector3(6.9f, 2.4f, 5.6f));
            HitCapsuleX("Neck", hit, new Vector3(-8f, 5.4f, 0f), 0.9f, 2.0f, PartType.Neck);
            HitSphere("Head", hit, new Vector3(-10.2f, 6.0f, 0f), 1.8f, PartType.Face);
            foreach (float z in new[] { -3.1f, 3.1f })
                HitCapsuleX("Arm", hit, new Vector3(-4.2f, 7.3f, z), 0.65f, 6f, PartType.Arm);

            var headAnchor = new GameObject("HeadAnchor").transform;   // head.up が顔の向き（視線）
            headAnchor.SetParent(upper, true);
            headAnchor.position = new Vector3(-10.2f, 6.0f, 0f);

            var shoulders = new List<Transform>();
            foreach (float z in new[] { -2.7f, 2.7f })
            {
                var s = new GameObject("Shoulder").transform;
                s.SetParent(upper, true);
                s.position = new Vector3(-7.2f, 6.2f, z);
                shoulders.Add(s);
            }

            // ---- 見た目（Model）----
            var model = new GameObject("Model").transform;
            model.SetParent(upper, true);
            CapsuleXVisual("Torso", model, new Vector3(-4f, 5.2f, 0f), 1.2f, 6.9f, shirt, 1f, 2.3f);
            Ellipsoid("Chest", model, new Vector3(-6.3f, 5.5f, 0f), new Vector3(3.6f, 2.5f, 5.4f), shirt);
            Prim(PrimitiveType.Capsule, "Neck", model, new Vector3(-8f, 5.4f, 0f), new Vector3(1.8f, 1.0f, 1.8f), Quaternion.Euler(0f, 0f, 90f), skin, false);
            foreach (float z in new[] { -2.7f, 2.7f }) Sphere("ShoulderBall", model, new Vector3(-7.2f, 6.1f, z), 0.85f, shirt, false);

            Sphere("Head", model, new Vector3(-10.2f, 6.0f, 0f), 1.8f, skin, false);
            Sphere("Hair", model, new Vector3(-11.2f, 6.0f, 0f), 1.85f, hair, false);
            Ellipsoid("HairTop", model, new Vector3(-10.9f, 7.2f, 0f), new Vector3(2.8f, 1.1f, 3.4f), hair);
            foreach (float z in new[] { -1.78f, 1.78f }) Ellipsoid("Ear", model, new Vector3(-10.2f, 5.9f, z), new Vector3(0.9f, 1.0f, 0.45f), skinDark);
            // 顔は +Y 向き。あごは +x 側
            var eyes = new List<Transform>();
            foreach (float z in new[] { -0.7f, 0.7f })
            {
                var eye = Ellipsoid("Eye", model, new Vector3(-9.6f, 7.58f, z), new Vector3(0.9f, 0.3f, 0.6f), white);
                Ellipsoid("Pupil", eye.transform, new Vector3(-9.55f, 7.69f, z), new Vector3(0.45f, 0.3f, 0.3f), dark);   // 目が閉じると一緒に細くなる
                eyes.Add(eye.transform);
                Prim(PrimitiveType.Capsule, "Lash", model, new Vector3(-9.6f, 7.52f, z), new Vector3(0.1f, 0.35f, 0.1f), Quaternion.Euler(90f, 0f, 0f), dark, false);
                Prim(PrimitiveType.Capsule, "Brow", model, new Vector3(-9.9f, 7.55f, z), new Vector3(0.14f, 0.45f, 0.14f), Quaternion.Euler(90f, 0f, 0f), hair, false);
            }
            Ellipsoid("Nose", model, new Vector3(-9.0f, 7.5f, 0f), new Vector3(0.6f, 0.6f, 0.6f), skinDark);
            Prim(PrimitiveType.Capsule, "Mouth", model, new Vector3(-8.5f, 7.28f, 0f), new Vector3(0.12f, 0.45f, 0.12f), Quaternion.Euler(90f, 0f, 0f), Mat("Lips", new Color(0.7f, 0.35f, 0.35f)), false);

            foreach (float z in new[] { -3.1f, 3.1f })
            {
                CapsuleXVisual("Sleeve", model, new Vector3(-5.6f, 7.3f, z), 0.72f, 3.0f, shirt);
                CapsuleXVisual("Forearm", model, new Vector3(-3.0f, 7.3f, z), 0.55f, 2.6f, skin);
                Ellipsoid("RestHand", model, new Vector3(-1.1f, 7.2f, z), new Vector3(1.5f, 0.8f, 1.2f), skin);
            }

            // ---- 脚（布団の下と、出ている足首・足）----
            var legHit = new GameObject("HitboxesLegs").transform;
            legHit.SetParent(root.transform, false);
            var legModel = new GameObject("ModelLegs").transform;
            legModel.SetParent(root.transform, false);
            foreach (float z in new[] { -1.45f, 1.45f })
            {
                HitBox("LegUnderBlanket", legHit, new Vector3(2.05f, 4.95f, z), new Vector3(5.1f, 1.9f, 1.9f));
                HitCapsuleX("Ankle", legHit, new Vector3(6.4f, 4.95f, z), 0.95f, 3.6f, PartType.Ankle);
                HitBox("Foot", legHit, new Vector3(8.6f, 4.5f, z), new Vector3(1.6f, 0.9f, 1.5f), PartType.Ankle);

                CapsuleXVisual("Trousers", legModel, new Vector3(2.4f, 4.95f, z), 0.95f, 5.8f, pants);
                CapsuleXVisual("Shin", legModel, new Vector3(6.4f, 4.95f, z), 0.78f, 3.6f, skin);
                Ellipsoid("Foot", legModel, new Vector3(8.7f, 4.5f, z), new Vector3(2.3f, 1.0f, 1.5f), skin);
            }

            // ---- 叩く手・腕・予兆・「！」（コライダーなし）----
            var hand = new GameObject("Hand").transform;
            hand.SetParent(root.transform, false);
            hand.position = new Vector3(0f, 10f, 0f);
            Ellipsoid("Palm", hand, hand.position, new Vector3(2.2f, 0.9f, 2.4f), skin);
            for (int i = 0; i < 4; i++)
                CapsuleXVisual("Finger", hand, hand.position + new Vector3(1.6f, 0f, -0.85f + 0.57f * i), 0.24f, 1.5f, skin);
            Prim(PrimitiveType.Capsule, "Thumb", hand, hand.position + new Vector3(0.5f, 0f, -1.5f), new Vector3(0.5f, 0.55f, 0.5f), Quaternion.Euler(90f, 0f, 20f), skin, false);

            var arm = Prim(PrimitiveType.Cylinder, "AttackArm", root.transform, new Vector3(0f, 5f, 0f), new Vector3(1.5f, 2f, 1.5f), Quaternion.identity, shirt, false).transform;

            var marker = Prim(PrimitiveType.Cylinder, "StrikeMarker", root.transform, new Vector3(0f, 0.05f, 0f),
                new Vector3(5.6f, 0.02f, 5.6f), Quaternion.identity, Mat("Marker", new Color(1f, 0.1f, 0.1f), true), false).transform;

            var exclaim = new GameObject("Exclaim").transform;
            exclaim.SetParent(root.transform, false);
            var red = Mat("ExclaimRed", new Color(1f, 0.15f, 0.1f), true);
            Prim(PrimitiveType.Capsule, "Bar", exclaim, new Vector3(0f, 1.0f, 0f), new Vector3(0.7f, 0.9f, 0.5f), Quaternion.identity, red, false);
            Prim(PrimitiveType.Sphere, "Dot", exclaim, new Vector3(0f, -0.6f, 0f), Vector3.one * 0.55f, Quaternion.identity, red, false);
            exclaim.gameObject.SetActive(false);

            human.yawPivot = yawPivot;
            human.upper = upper;
            human.head = headAnchor;
            human.hand = hand;
            human.marker = marker;
            human.eyes = eyes.ToArray();
            human.shoulders = shoulders.ToArray();
            human.attackArm = arm;
            human.exclaim = exclaim;
            return human;
        }

        static MosquitoController BuildMosquito(Human human, out ThirdPersonCamera cam)
        {
            var go = new GameObject("Mosquito");
            var start = new Vector3(14f, 8f, 6f);
            go.transform.position = start;
            var mc = go.AddComponent<MosquitoController>();

            var dark = Mat("MosquitoBody", new Color(0.12f, 0.1f, 0.1f));
            var wing = Mat("MosquitoWing", new Color(0.85f, 0.9f, 1f));
            // 向きは +Z が前
            var abdomen = Prim(PrimitiveType.Capsule, "Abdomen", go.transform, start + new Vector3(0f, 0f, -0.2f), new Vector3(0.14f, 0.2f, 0.14f), Quaternion.Euler(90f, 0f, 0f),
                new Material(Shader.Find("Standard")), false);   // 血でふくらむので専用のマテリアル（Start で複製して色を変える）
            Prim(PrimitiveType.Sphere, "Thorax", go.transform, start + new Vector3(0f, 0.02f, 0.12f), Vector3.one * 0.17f, Quaternion.identity, dark, false);
            Prim(PrimitiveType.Sphere, "Head", go.transform, start + new Vector3(0f, 0f, 0.26f), Vector3.one * 0.1f, Quaternion.identity, dark, false);
            Prim(PrimitiveType.Cylinder, "Proboscis", go.transform, start + new Vector3(0f, -0.03f, 0.4f), new Vector3(0.015f, 0.08f, 0.015f), Quaternion.Euler(90f, 0f, 0f), dark, false);
            foreach (float sx in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cylinder, "Antenna", go.transform, start + new Vector3(0.03f * sx, 0.05f, 0.35f), new Vector3(0.008f, 0.06f, 0.008f), Quaternion.Euler(65f, 0f, -15f * sx), dark, false);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cylinder, "Leg", go.transform, start + new Vector3(0.1f * sx, -0.1f, 0.2f - 0.1f * i), new Vector3(0.012f, 0.13f, 0.012f), Quaternion.Euler(0f, 0f, 40f * sx), dark, false);
            }

            var wl = new GameObject("WingL").transform;
            wl.SetParent(go.transform, true); wl.position = start + new Vector3(0.05f, 0.08f, 0.1f);
            Prim(PrimitiveType.Cube, "Blade", wl, wl.position + new Vector3(0.22f, 0f, 0f), new Vector3(0.45f, 0.01f, 0.14f), Quaternion.identity, wing, false);
            var wr = new GameObject("WingR").transform;
            wr.SetParent(go.transform, true); wr.position = start + new Vector3(-0.05f, 0.08f, 0.1f);
            Prim(PrimitiveType.Cube, "Blade", wr, wr.position + new Vector3(-0.22f, 0f, 0f), new Vector3(0.45f, 0.01f, 0.14f), Quaternion.identity, wing, false);

            float yaw = Mathf.Atan2(-1f - start.x, 0f - start.z) * Mathf.Rad2Deg;   // ベッド（中央）の方を向いて出発
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            cam = camGo.AddComponent<ThirdPersonCamera>();
            cam.target = go.transform;
            cam.Yaw = yaw;
            cam.Pitch = 12f;

            mc.wingL = wl;
            mc.wingR = wr;
            mc.belly = abdomen.transform;
            mc.cam = cam;
            mc.human = human;
            human.mosquito = mc;
            return mc;
        }
    }
}
