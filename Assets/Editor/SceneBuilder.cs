using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MosquitoGame.EditorTools
{
    /// <summary>メニュー Mosquito/Build Main Scene で、部屋・人形・蚊・カメラを含む Main シーンを作り直す。</summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string MatDir = "Assets/Materials";
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("Mosquito/Build Main Scene")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets", "Materials");
            mats.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.27f, 0.36f);

            BuildLights();
            BuildRoom();
            BuildFurniture();
            var human = BuildHuman();
            var mosquito = BuildMosquito(human, out var cam);

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.mosquito = mosquito;
            gm.human = human;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("Main シーンを作成しました: " + ScenePath);
        }

        // ---- 素材 ----

        static Material Mat(string name, Color c, bool unlit = false)
        {
            string key = name;
            if (mats.TryGetValue(key, out var m)) return m;
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
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(m);
            mats[key] = m;
            return m;
        }

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

        /// <summary>X 軸方向に伸びたカプセル。length は全長。</summary>
        static GameObject CapsuleX(string name, Transform parent, Vector3 pos, float radius, float length, Material m, bool collider = true)
            => Prim(PrimitiveType.Capsule, name, parent, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), Quaternion.Euler(0f, 0f, 90f), m, collider);

        static void Part(GameObject go, PartType t) => go.AddComponent<BodyPart>().type = t;

        // ---- シーン ----

        static void BuildLights()
        {
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.intensity = 0.8f;
            moon.shadows = LightShadows.Hard;
            moon.transform.rotation = Quaternion.Euler(50f, 60f, 0f);

            var lamp = new GameObject("NightLampLight").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.75f, 0.45f);
            lamp.intensity = 1.6f;
            lamp.range = 24f;
            lamp.transform.position = new Vector3(-12.5f, 7f, 8.5f);
        }

        static void BuildRoom()
        {
            var room = new GameObject("Room").transform;
            var floor = Mat("Floor", new Color(0.30f, 0.22f, 0.16f));
            var wall = Mat("Wall", new Color(0.50f, 0.50f, 0.56f));
            Box("Floor", room, new Vector3(0f, -0.5f, 0f), new Vector3(61f, 1f, 41f), floor);
            Box("Ceiling", room, new Vector3(0f, 26.5f, 0f), new Vector3(61f, 1f, 41f), wall);
            Box("WallE", room, new Vector3(30.5f, 13f, 0f), new Vector3(1f, 28f, 42f), wall);
            Box("WallW", room, new Vector3(-30.5f, 13f, 0f), new Vector3(1f, 28f, 42f), wall);
            Box("WallN", room, new Vector3(0f, 13f, 20.5f), new Vector3(62f, 28f, 1f), wall);
            Box("WallS", room, new Vector3(0f, 13f, -20.5f), new Vector3(62f, 28f, 1f), wall);
            // 窓（月明かり）：壁に少し浮かせた光る板
            Box("Window", room, new Vector3(29.85f, 15f, -6f), new Vector3(0.2f, 9f, 8f), Mat("Window", new Color(0.55f, 0.7f, 1f), true), false);
        }

        static void BuildFurniture()
        {
            var f = new GameObject("Furniture").transform;
            var wood = Mat("Wood", new Color(0.36f, 0.24f, 0.14f));
            var sheet = Mat("Sheet", new Color(0.78f, 0.78f, 0.82f));
            var blanket = Mat("Blanket", new Color(0.22f, 0.32f, 0.55f));
            var pillow = Mat("Pillow", new Color(0.9f, 0.9f, 0.92f));

            // ベッド（x -13.5〜10.5、頭が -x 側）。下は高さ 2.4 の空間があり隠れられる
            foreach (float sx in new[] { -11.5f, 8.5f })
                foreach (float sz in new[] { -5.2f, 5.2f })
                    Box("BedLeg", f, new Vector3(sx, 1.2f, sz), new Vector3(0.9f, 2.4f, 0.9f), wood);
            Box("BedFrame", f, new Vector3(-1.5f, 2.7f, 0f), new Vector3(24f, 0.6f, 12f), wood);
            Box("Mattress", f, new Vector3(-1.5f, 3.5f, 0f), new Vector3(24f, 1f, 11.6f), sheet);
            Box("Headboard", f, new Vector3(-13.9f, 6f, 0f), new Vector3(0.8f, 7f, 12f), wood);
            Box("Pillow", f, new Vector3(-10.5f, 4.7f, 0f), new Vector3(4.5f, 1.4f, 7f), pillow);
            Box("Blanket", f, new Vector3(-1f, 5.3f, 0f), new Vector3(11f, 2.6f, 8.6f), blanket);

            // 机
            Box("DeskTop", f, new Vector3(20f, 7f, -14f), new Vector3(10f, 0.8f, 5f), wood);
            foreach (float dx in new[] { -4.4f, 4.4f })
                foreach (float dz in new[] { -2f, 2f })
                    Box("DeskLeg", f, new Vector3(20f + dx, 3.5f, -14f + dz), new Vector3(0.8f, 7f, 0.8f), wood);

            // 衣装だんす・ナイトスタンド・カーテン
            Box("Wardrobe", f, new Vector3(-25f, 9f, -15f), new Vector3(6f, 18f, 8f), Mat("Wardrobe", new Color(0.30f, 0.20f, 0.12f)));
            Box("Nightstand", f, new Vector3(-12.5f, 2.5f, 8.5f), new Vector3(4f, 5f, 4f), wood);
            Sphere("NightLamp", f, new Vector3(-12.5f, 6.3f, 8.5f), 1.1f, Mat("Lamp", new Color(1f, 0.85f, 0.55f), true), false);
            Box("Curtain", f, new Vector3(29.3f, 11f, 8f), new Vector3(0.6f, 18f, 9f), Mat("Curtain", new Color(0.45f, 0.12f, 0.15f)));
        }

        static Human BuildHuman()
        {
            var root = new GameObject("Human");
            var human = root.AddComponent<Human>();
            var skin = Mat("Skin", new Color(0.93f, 0.74f, 0.62f));
            var shirt = Mat("Shirt", new Color(0.35f, 0.55f, 0.45f));

            // 腰（左右を向く）→ 上体（起き上がる）
            var yawPivot = new GameObject("YawPivot").transform;
            yawPivot.SetParent(root.transform, false);
            yawPivot.position = new Vector3(-1f, 5.2f, 0f);
            var upper = new GameObject("Upper").transform;
            upper.SetParent(yawPivot, false);

            Box("Torso", upper, new Vector3(-4f, 5.2f, 0f), new Vector3(6.9f, 2.4f, 5.6f), shirt);
            Part(CapsuleX("Neck", upper, new Vector3(-8f, 5.4f, 0f), 0.9f, 2.0f, skin), PartType.Neck);
            var headGo = Sphere("Head", upper, new Vector3(-10.2f, 6.0f, 0f), 1.8f, skin);
            Part(headGo, PartType.Face);
            foreach (float z in new[] { -3.1f, 3.1f })
                Part(CapsuleX("Arm", upper, new Vector3(-4.2f, 7.3f, z), 0.65f, 6f, skin), PartType.Arm);

            // 布団の下の脚（吸血不可）と、出ている足首・足
            foreach (float z in new[] { -1.45f, 1.45f })
            {
                Box("LegUnderBlanket", root.transform, new Vector3(2.05f, 4.95f, z), new Vector3(5.1f, 1.9f, 1.9f), skin);
                Part(CapsuleX("Ankle", root.transform, new Vector3(6.4f, 4.95f, z), 0.95f, 3.6f, skin), PartType.Ankle);
                Part(Box("Foot", root.transform, new Vector3(8.6f, 4.5f, z), new Vector3(1.6f, 0.9f, 1.5f), skin), PartType.Ankle);
            }

            // 叩く手と予兆（コライダーなし）
            var hand = Sphere("Hand", root.transform, new Vector3(0f, 10f, 0f), 1.5f, skin, false).transform;
            var marker = Prim(PrimitiveType.Cylinder, "StrikeMarker", root.transform, new Vector3(0f, 0.05f, 0f),
                new Vector3(5.6f, 0.02f, 5.6f), Quaternion.identity, Mat("Marker", new Color(1f, 0.1f, 0.1f), true), false).transform;

            human.yawPivot = yawPivot;
            human.upper = upper;
            human.head = headGo.transform;
            human.hand = hand;
            human.marker = marker;
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
            Prim(PrimitiveType.Capsule, "Abdomen", go.transform, start + new Vector3(0f, 0f, -0.2f), new Vector3(0.14f, 0.2f, 0.14f), Quaternion.Euler(90f, 0f, 0f), dark, false);
            Prim(PrimitiveType.Sphere, "Thorax", go.transform, start + new Vector3(0f, 0.02f, 0.12f), Vector3.one * 0.17f, Quaternion.identity, dark, false);
            Prim(PrimitiveType.Sphere, "Head", go.transform, start + new Vector3(0f, 0f, 0.26f), Vector3.one * 0.1f, Quaternion.identity, dark, false);
            Prim(PrimitiveType.Cylinder, "Proboscis", go.transform, start + new Vector3(0f, -0.03f, 0.4f), new Vector3(0.015f, 0.08f, 0.015f), Quaternion.Euler(90f, 0f, 0f), dark, false);

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
            mc.cam = cam;
            mc.human = human;
            human.mosquito = mc;
            return mc;
        }
    }
}
