using System;
using ProjectF.Construction;
using ProjectF.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class FarmSetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/FarmSettings.asset";
        public const string PrefabPath = "Assets/ProjectF/Prefabs/WheatFarm.prefab";
        public const string SpritePath = "Assets/ProjectF/Art/WheatFarm.png";
        [MenuItem("Project F/Select Farm Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<FarmSettings>(SettingsPath);

        [MenuItem("Project F/Setup/Add Farm Production")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != CombatSceneSetup.ScenePath)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding farms.");
            if (Object.FindAnyObjectByType<FarmHUD>() != null) return;
            var construction = Object.FindAnyObjectByType<WallConstruction>();
            var stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            var placement = Object.FindAnyObjectByType<WallPlacementController>();
            var canvas = GameObject.Find("Command HUD");
            if (construction == null || stockpile == null || placement == null || canvas == null || canvas.GetComponent<Canvas>() == null)
                throw new InvalidOperationException("Existing construction, estate and Command HUD are required.");
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Import the farm sprite before setup.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 512;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null) throw new InvalidOperationException("Farm sprite import failed.");
            var settings = AssetDatabase.LoadAssetAtPath<FarmSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<FarmSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab(sprite);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add farm production");
            Ref(construction, "farmSettings", settings);
            Ref(construction, "farmPrefab", prefab.GetComponent<WallStructure>());
            var root = Rect("Farm Production", canvas.transform, new Vector2(1, 1), new Vector2(-18, -264), new Vector2(260, 128));
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Add farm HUD");
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.14f, .12f, .06f, .95f);
            var status = Label("Status", root, new Vector2(12, -8), new Vector2(236, 62), 14);
            status.text = "FOOD 0 / 200\nFARM 30 WOOD / +4 FOOD / 8s\nBuild farms to produce food";
            var buttonRect = Rect("Build Farm", root, new Vector2(0, 1), new Vector2(12, -82), new Vector2(236, 34));
            var background = buttonRect.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(.38f, .28f, .10f);
            var button = buttonRect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = background;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var caption = Label("Label", buttonRect, Vector2.zero, new Vector2(236, 34), 16);
            caption.text = "BUILD FARM"; caption.alignment = TextAnchor.MiddleCenter;
            var hud = root.gameObject.AddComponent<FarmHUD>();
            Ref(hud, "stockpile", stockpile); Ref(hud, "construction", construction); Ref(hud, "placement", placement);
            Ref(hud, "status", status); Ref(hud, "button", button); Ref(hud, "buttonLabel", caption);
            var pause = canvas.transform.Find("Pause HUD"); if (pause != null) root.SetSiblingIndex(pause.GetSiblingIndex());
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static GameObject CreatePrefab(Sprite sprite)
        {
            var farm = new GameObject("Wheat Farm"); farm.SetActive(false);
            try
            {
                farm.AddComponent<BoxCollider2D>().size = new Vector2(1.8f, 1.8f);
                farm.AddComponent<WallStructure>(); farm.AddComponent<FarmProduction>();
                var view = new GameObject("Farm Sprite", typeof(SpriteRenderer)); view.transform.SetParent(farm.transform, false);
                view.transform.localPosition = new Vector3(0, 0, -.05f);
                view.transform.localScale = new Vector3(1.8f / sprite.bounds.size.x, 1.8f / sprite.bounds.size.y, 1);
                var renderer = view.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = 1;
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("URP sprite unlit shader is required.");
                const string materialPath = "Assets/ProjectF/Art/FarmSprite.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                renderer.sharedMaterial = material;
                // Reuse the existing structure health display and combat rules.
                var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConstructionSetup.PrefabPath);
                var health = wallPrefab != null ? wallPrefab.GetComponent<WallHealthView>() : null;
                if (health != null)
                {
                    var sourceFill = (Transform)new SerializedObject(health).FindProperty("healthFill").objectReferenceValue;
                    if (sourceFill != null && sourceFill.parent != null)
                    {
                        var bar = Object.Instantiate(sourceFill.parent.gameObject, farm.transform);
                        bar.name = "Health Bar"; bar.transform.localPosition = new Vector3(0, 1.1f, -.1f);
                        Ref(farm.AddComponent<WallHealthView>(), "healthFill", bar.transform.Find(sourceFill.name));
                    }
                }
                return PrefabUtility.SaveAsPrefabAsset(farm, PrefabPath);
            }
            finally { Object.DestroyImmediate(farm); }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static UnityEngine.UI.Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), position, size);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
            text.color = new Color(1, .93f, .74f); text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
        }
        private static void Ref(Object target, string name, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
