using System;
using ProjectF.Construction;
using ProjectF.Economy;
using ProjectF.Input;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class ConstructionSetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/ConstructionSettings.asset";
        public const string PrefabPath = "Assets/ProjectF/Prefabs/PalisadeWall.prefab";
        [MenuItem("Project F/Select Construction Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<ConstructionSettings>(SettingsPath);

        [MenuItem("Project F/Setup/Add Wall Construction")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding construction.");
            if (Object.FindAnyObjectByType<WallConstruction>() != null) return;
            var commands = Object.FindAnyObjectByType<UnitCommandController>();
            var invasion = Object.FindAnyObjectByType<PortalInvasion>();
            var canvas = GameObject.Find("Command HUD");
            if (commands == null || invasion == null || canvas == null) throw new InvalidOperationException("Gameplay scene dependencies are missing.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectF/Development/TestGeometry.asset");
            var wood = Material("PalisadeWood", new Color(.5f, .28f, .11f));
            var post = Material("PalisadePosts", new Color(.8f, .57f, .3f));
            var ghost = Material("ConstructionPreview", Color.white);
            var edge = Material("EstateBoundary", new Color(.3f, .85f, .8f));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var wall = new GameObject("Palisade Wall");
                wall.SetActive(false);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(.9f, .9f);
                wall.AddComponent<WallStructure>();
                Quad("Base", wall.transform, mesh, wood, new Vector3(.9f, .9f, 1), Vector3.zero);
                for (int i = 0; i < 4; i++) Quad("Post " + (i + 1), wall.transform, mesh, post,
                    new Vector3(.12f, .82f, 1), new Vector3(-.33f + i * .22f, 0, -.02f));
                prefab = PrefabUtility.SaveAsPrefabAsset(wall, PrefabPath);
                Object.DestroyImmediate(wall);
            }
            var settings = AssetDatabase.LoadAssetAtPath<ConstructionSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<ConstructionSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }

            int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add wall construction");
            var root = new GameObject("Estate Construction");
            Undo.RegisterCreatedObjectUndo(root, "Add construction");
            var stockpile = Undo.AddComponent<EstateStockpile>(root);
            var construction = Undo.AddComponent<WallConstruction>(root);
            var placement = Undo.AddComponent<WallPlacementController>(root);
            var structures = new GameObject("Built Walls").transform;
            structures.SetParent(root.transform, false);
            var preview = Quad("Wall Preview", root.transform, mesh, ghost, new Vector3(.9f, .9f, 1), Vector3.zero);
            preview.enabled = false;
            var boundary = new GameObject("Estate Boundary", typeof(LineRenderer)).GetComponent<LineRenderer>();
            boundary.transform.SetParent(root.transform, false);
            boundary.sharedMaterial = edge;
            boundary.useWorldSpace = true; boundary.loop = true; boundary.widthMultiplier = .055f;
            boundary.enabled = false;
            Ref(construction, "settings", settings); Ref(construction, "stockpile", stockpile);
            Ref(construction, "invasion", invasion); Ref(construction, "navigation", commands.GetComponent<NavigationWorld2D>());
            Ref(construction, "wallPrefab", prefab.GetComponent<WallStructure>()); Ref(construction, "structures", structures);
            Ref(placement, "construction", construction); Ref(placement, "input", commands.GetComponent<CommandInput>());
            Ref(placement, "commands", commands); Ref(placement, "worldCamera", Camera.main);
            Ref(placement, "preview", preview); Ref(placement, "boundary", boundary);

            var panel = UI("Construction Panel", canvas.transform, new Vector2(.5f, 0), new Vector2(0, 84), new Vector2(930, 56));
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Add construction HUD");
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.085f, .065f, .045f, .96f);
            var status = Label(UI("Status", panel, new Vector2(0, .5f), new Vector2(18, 0), new Vector2(680, 48)));
            status.text = "WOOD 80  /  WALL 10  /  Build before invasion";
            var buttonRect = UI("Build Wall", panel, new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(192, 40));
            var image = buttonRect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.4f, .27f, .13f);
            var button = buttonRect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Label(UI("Label", buttonRect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(188, 38)));
            label.text = "BUILD WALL"; label.alignment = TextAnchor.MiddleCenter;
            var hud = panel.gameObject.AddComponent<ConstructionHUD>();
            Ref(hud, "stockpile", stockpile); Ref(hud, "construction", construction); Ref(hud, "placement", placement);
            Ref(hud, "status", status); Ref(hud, "button", button); Ref(hud, "buttonLabel", label);
            var help = canvas.transform.Find("Controls").GetComponent<UnityEngine.UI.Text>();
            Undo.RecordObject(help, "Update construction controls");
            help.text = "PROJECT F  /  ESTATE DEFENSE\nLMB / drag: select allies   RMB enemy: attack   RMB ground: move / retreat\nBuild walls with wood before starting the invasion. RMB or Esc cancels building.";
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static Material Material(string name, Color color)
        {
            string path = "Assets/ProjectF/Development/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            result = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            result.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(result, path); return result;
        }
        private static MeshRenderer Quad(string name, Transform parent, Mesh mesh, Material material, Vector3 scale, Vector3 position)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localScale = scale; go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; return renderer;
        }
        private static RectTransform UI(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static UnityEngine.UI.Text Label(RectTransform rect)
        {
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 17;
            text.alignment = TextAnchor.MiddleLeft; text.color = new Color(.95f, .93f, .86f); text.raycastTarget = false;
            return text;
        }
        private static void Ref(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
