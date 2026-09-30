using System.IO;
using System.Linq;
using ProjectF.CameraSystem;
using ProjectF.Input;
using ProjectF.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectF.Editor
{
    /// <summary>Creates the first movement test once; opening it never rebuilds user edits.</summary>
    public static class InputCameraSceneSetup
    {
        public const string ScenePath = "Assets/ProjectF/Scenes/InputCamera.unity";
        public const string SettingsPath = "Assets/ProjectF/Data/LordMovementSettings.asset";
        private const string GeometryPath = "Assets/ProjectF/Development/TestGeometry.asset";

        [MenuItem("Project F/Open Mouse Command Test")]
        public static void OpenMovementTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath);
            else
                CreateScene();
        }

        [MenuItem("Project F/Select Movement Settings")]
        public static void SelectMovementSettings()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<LordMovementSettings>(SettingsPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private static void CreateScene()
        {
            EnsureFolder("Assets/ProjectF/Scenes");
            EnsureFolder("Assets/ProjectF/Data");
            EnsureFolder("Assets/ProjectF/Development");

            // Switching scenes can unload unreferenced assets; load dependencies afterwards.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var settings = AssetDatabase.LoadAssetAtPath<LordMovementSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LordMovementSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var quad = AssetDatabase.LoadAssetAtPath<Mesh>(GeometryPath);
            if (quad == null)
            {
                quad = new Mesh { name = "Unit Quad" };
                quad.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f), new Vector3(.5f, -.5f) };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                AssetDatabase.CreateAsset(quad, GeometryPath);
            }
            Material MakeMaterial(string name, string hex)
            {
                var existing = AssetDatabase.LoadAllAssetsAtPath(GeometryPath).OfType<Material>().FirstOrDefault(m => m.name == name);
                if (existing != null) return existing;
                ColorUtility.TryParseHtmlString(hex, out var color);
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
                material.SetColor("_BaseColor", color);
                AssetDatabase.AddObjectToAsset(material, GeometryPath);
                return material;
            }
            var ground = MakeMaterial("Ground", "#18272D");
            var grid = MakeMaterial("Grid", "#233B43");
            var stone = MakeMaterial("Stone", "#425A60");
            var pale = MakeMaterial("Pale", "#DAE9E2");
            var gold = MakeMaterial("Lord Gold", "#EDBF67");
            var cloak = MakeMaterial("Lord Cloak", "#AF603E");
            var shadow = MakeMaterial("Shadow", "#101B22");
            var teal = MakeMaterial("Teal", "#4EAEA2");

            var arena = new GameObject("Movement Test Ground").transform;
            GameObject Rect(string name, Transform parent, float x, float y, float z, float w, float h, Material material)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x, y, z);
                go.transform.localScale = new Vector3(w, h, 1);
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                return go;
            }
            Rect("Ground", arena, 0, 0, 2, 50, 34, ground);
            for (int x = -24; x <= 24; x += 2)
                Rect("Grid X " + x, arena, x, 0, 1.8f, .025f, 32, grid);
            for (int y = -16; y <= 16; y += 2)
                Rect("Grid Y " + y, arena, 0, y, 1.8f, 48, .025f, grid);
            Rect("Origin Horizontal", arena, 0, 0, 1.6f, 2, .08f, teal);
            Rect("Origin Vertical", arena, 0, 0, 1.6f, .08f, 2, teal);
            foreach (var wall in new[]
            {
                Rect("North Boundary", arena, 0, 16, 1, 49, 1, stone),
                Rect("South Boundary", arena, 0, -16, 1, 49, 1, stone),
                Rect("West Boundary", arena, -24, 0, 1, 1, 33, stone),
                Rect("East Boundary", arena, 24, 0, 1, 1, 33, stone)
            }) wall.AddComponent<BoxCollider2D>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            void Label(string value, float x, float y, float size, Color color)
            {
                var go = new GameObject(value, typeof(TextMesh));
                go.transform.SetParent(arena, false);
                go.transform.localPosition = new Vector3(x, y, .8f);
                var text = go.GetComponent<TextMesh>();
                text.font = font;
                text.fontSize = 64;
                text.characterSize = size;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = color;
                text.text = value;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            Label("PROJECT F  /  MOVEMENT TEST", 0, 5.2f, .11f, new Color(.85f, .92f, .9f));
            Label("MOUSE COMMAND TEST", 0, -3.7f, .09f, new Color(.55f, .73f, .73f));
            Label("Each grid square = 2 units", 0, -4.6f, .065f, new Color(.45f, .61f, .63f));
            Label("NORTH", 0, 13.5f, .14f, Color.white);
            Label("SOUTH", 0, -13.5f, .14f, Color.white);
            Label("WEST", -20, 0, .14f, Color.white);
            Label("EAST", 20, 0, .14f, Color.white);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -8 : 8;
                float y = i < 2 ? 7 : -7;
                Rect("Landmark Base " + i, arena, x, y, 1.5f, 1.5f, 1.5f, stone);
                Rect("Landmark Inlay " + i, arena, x, y, 1.4f, 1.05f, 1.05f, teal);
            }

            var lord = new GameObject("Lord");
            lord.SetActive(false);
            var body = lord.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            lord.AddComponent<CircleCollider2D>().radius = .38f;
            var movement = lord.AddComponent<CommandableUnit>();
            SetReference(movement, "settings", settings);
            var visual = new GameObject("Placeholder Visual").transform;
            visual.SetParent(lord.transform, false);
            Rect("Shadow", visual, .09f, -.22f, .35f, .9f, .7f, shadow).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Rect("Cloak", visual, 0, -.06f, .2f, .68f, .84f, cloak);
            Rect("Gold Body", visual, 0, .13f, .1f, .65f, .65f, gold).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Rect("Helmet", visual, 0, .38f, 0, .33f, .26f, pale);
            lord.SetActive(true);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0, 0, -10);
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .085f, .11f);
            ConfigureMouseControls();

            EditorSceneManager.SaveScene(scene, ScenePath);
            // Make ordinary Build And Run reproduce this feature; retain the original scene.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = lord;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(24, 14, 1)), false);
            Debug.Log("Project F: mouse command test scene created.");
        }

        /// <summary>Upgrades the existing test scene in place, preserving its asset GUID and geometry.</summary>
        public static void ConfigureMouseControls()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath && !string.IsNullOrEmpty(scene.path))
                throw new System.InvalidOperationException("Open InputCamera before configuring controls.");
            var lord = GameObject.Find("Lord");
            var camera = Camera.main;
            if (lord == null || camera == null)
                throw new System.InvalidOperationException("The test scene requires Lord and Main Camera.");
            // Remove only the previous direct-control components; keep the scene and art objects.
            foreach (var component in lord.GetComponents<MonoBehaviour>().OrderBy(c => c.GetType().Name == "LordMovement" ? 0 : 1))
                if (component.GetType().Name == "LordMovement" || component.GetType().Name == "LordMoveInput")
                    Object.DestroyImmediate(component);
            foreach (var component in camera.GetComponents<MonoBehaviour>())
                if (component.GetType().Name == "FollowCamera2D") Object.DestroyImmediate(component);

            var root = GameObject.Find("Mouse Commands") ?? new GameObject("Mouse Commands");
            var input = root.GetComponent<CommandInput>() ?? root.AddComponent<CommandInput>();
            var commands = root.GetComponent<UnitCommandController>() ?? root.AddComponent<UnitCommandController>();
            var navigation = root.GetComponent<NavigationWorld2D>() ?? root.AddComponent<NavigationWorld2D>();
            var settings = AssetDatabase.LoadAssetAtPath<LordMovementSettings>(SettingsPath);
            var unit = lord.GetComponent<CommandableUnit>() ?? lord.AddComponent<CommandableUnit>();
            SetReference(unit, "settings", settings);
            var units = new CommandableUnit[4];
            units[0] = unit;
            lord.transform.position = new Vector3(-2, 1, 0);
            for (int i = 1; i < units.Length; i++)
            {
                string unitName = "Test Unit " + (i + 1);
                var go = GameObject.Find(unitName);
                if (go == null) { go = Object.Instantiate(lord); go.name = unitName; }
                go.transform.position = new Vector3(i % 2 == 0 ? -2 : 2, i < 2 ? 1 : -2, 0);
                units[i] = go.GetComponent<CommandableUnit>();
            }
            var markerRoot = GameObject.Find("Command Destinations") ?? new GameObject("Command Destinations");
            var material = AssetDatabase.LoadAllAssetsAtPath(GeometryPath).OfType<Material>().First(m => m.name == "Teal");
            GameObject Ring(string name, Transform parent, float radius)
            {
                var existing = parent.Find(name);
                if (existing != null) return existing.gameObject;
                var go = new GameObject(name, typeof(LineRenderer));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0, 0, .5f);
                var line = go.GetComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.loop = true;
                line.widthMultiplier = .055f;
                line.positionCount = 40;
                for (int p = 0; p < 40; p++)
                {
                    float angle = p * Mathf.PI * 2 / 40;
                    line.SetPosition(p, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
                }
                go.SetActive(false);
                return go;
            }
            for (int i = 0; i < units.Length; i++)
            {
                SetReference(units[i], "navigation", navigation);
                SetReference(units[i], "selectionVisual", Ring("Selection Ring", units[i].transform, .67f));
                SetReference(units[i], "destinationVisual", Ring("Destination " + i, markerRoot.transform, .3f).transform);
            }

            var canvasGo = GameObject.Find("Command HUD");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("Command HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = .5f;
                var help = new GameObject("Controls", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                help.transform.SetParent(canvasGo.transform, false);
                var rect = help.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -18);
                rect.sizeDelta = new Vector2(-24, 62);
                var text = help.GetComponent<UnityEngine.UI.Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 18;
                text.alignment = TextAnchor.UpperCenter;
                text.color = new Color(.85f, .95f, .92f);
                text.raycastTarget = false;
                text.text = "PROJECT F  /  MOUSE COMMAND TEST\nLMB: select / drag select    RMB: move    Wheel: zoom    MMB drag: pan";
                var box = new GameObject("Drag Selection", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                box.transform.SetParent(canvasGo.transform, false);
                var image = box.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(.25f, .85f, .7f, .13f);
                image.raycastTarget = false;
                box.SetActive(false);
            }
            var selection = canvasGo.transform.Find("Drag Selection");
            var oldOutline = selection.GetComponent<UnityEngine.UI.Outline>();
            if (oldOutline != null) Object.DestroyImmediate(oldOutline);
            for (int edge = 0; edge < 4; edge++)
            {
                string edgeName = "Border " + edge;
                if (selection.Find(edgeName) != null) continue;
                var border = new GameObject(edgeName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                border.transform.SetParent(selection, false);
                var rect = border.GetComponent<RectTransform>();
                bool horizontal = edge < 2;
                float side = edge % 2;
                rect.anchorMin = horizontal ? new Vector2(0, side) : new Vector2(side, 0);
                rect.anchorMax = horizontal ? new Vector2(1, side) : new Vector2(side, 1);
                rect.sizeDelta = horizontal ? new Vector2(0, 1.5f) : new Vector2(1.5f, 0);
                rect.anchoredPosition = Vector2.zero;
                var image = border.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(.3f, .95f, .8f, .85f);
                image.raycastTarget = false;
            }
            SetReference(commands, "input", input);
            SetReference(commands, "worldCamera", camera);
            SetReference(commands, "selectionBox", canvasGo.transform.Find("Drag Selection"));
            var strategy = camera.GetComponent<StrategyCamera2D>() ?? camera.gameObject.AddComponent<StrategyCamera2D>();
            SetReference(strategy, "input", input);
            SetReference(strategy, "commands", commands);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographicSize = 10.5f;
            foreach (var label in Object.FindObjectsByType<TextMesh>())
                if (label.text.Contains("W A S D") || label.text.Contains("PROJECT F  /")) label.text = "";
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Project F/Add Navigation Test Obstacles")]
        public static void AddNavigationObstacles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != ScenePath) OpenMovementTest();
            if (SceneManager.GetActiveScene().path != ScenePath || GameObject.Find("Navigation Obstacles") != null) return;
            var root = new GameObject("Navigation Obstacles");
            Undo.RegisterCreatedObjectUndo(root, "Add navigation test obstacles");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeometryPath);
            var material = AssetDatabase.LoadAllAssetsAtPath(GeometryPath).OfType<Material>().First(m => m.name == "Stone");
            var positions = new[] { new Vector2(0, 6), new Vector2(10, -2), new Vector2(-10, -2) };
            var sizes = new[] { new Vector2(3, 1.5f), new Vector2(1.5f, 5), new Vector2(1.5f, 5) };
            for (int i = 0; i < positions.Length; i++)
            {
                var obstacle = new GameObject("Navigation Wall " + (i + 1), typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D));
                obstacle.transform.SetParent(root.transform, false);
                obstacle.transform.localPosition = new Vector3(positions[i].x, positions[i].y, .6f);
                obstacle.transform.localScale = new Vector3(sizes[i].x, sizes[i].y, 1);
                obstacle.GetComponent<MeshFilter>().sharedMesh = mesh;
                obstacle.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
