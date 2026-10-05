using System;
using ProjectF.Combat;
using ProjectF.Economy;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class WoodGatheringSetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/WoodGatheringSettings.asset";
        [MenuItem("Project F/Select Wood Gathering Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<WoodGatheringSettings>(SettingsPath);

        [MenuItem("Project F/Setup/Add Wood Gathering")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding wood gathering.");
            if (Object.FindAnyObjectByType<WoodResourceNode>() != null) return;
            var stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            var invasion = Object.FindAnyObjectByType<PortalInvasion>();
            var navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            var controls = GameObject.Find("Command HUD/Controls");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectF/Development/TestGeometry.asset");
            var bark = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectF/Development/PalisadeWood.mat");
            if (stockpile == null || invasion == null || navigation == null || controls == null || mesh == null || bark == null)
                throw new InvalidOperationException("Existing gameplay dependencies are required.");
            var settings = AssetDatabase.LoadAssetAtPath<WoodGatheringSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<WoodGatheringSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            const string leafPath = "Assets/ProjectF/Development/WoodFoliage.mat";
            var leaves = AssetDatabase.LoadAssetAtPath<Material>(leafPath);
            if (leaves == null)
            {
                leaves = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "WoodFoliage" };
                leaves.SetColor("_BaseColor", new Color(.18f, .48f, .26f)); AssetDatabase.CreateAsset(leaves, leafPath);
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add wood gathering");
            var root = new GameObject("Wood Resources"); Undo.RegisterCreatedObjectUndo(root, "Add wood resources");
            var positions = new[] { new Vector2(-8, 3), new Vector2(-10, 5), new Vector2(-6, 4) };
            for (int i = 0; i < positions.Length; i++)
            {
                var tree = new GameObject("Wood Grove " + (i + 1)); tree.transform.SetParent(root.transform, false);
                tree.transform.position = positions[i];
                tree.AddComponent<BoxCollider2D>().size = new Vector2(1, 1);
                var node = tree.AddComponent<WoodResourceNode>(); Ref(node, "navigation", navigation);
                Quad("Stump", tree.transform, mesh, bark, new Vector3(.4f, .7f, 1), new Vector3(0, -.1f, -.03f));
                var foliage = Quad("Foliage", tree.transform, mesh, leaves, new Vector3(1, 1, 1), new Vector3(0, .05f, -.05f));
                var label = Label("Wood Remaining", tree.transform, new Vector3(0, -.85f, -.2f)); label.text = "WOOD 40";
                var view = tree.AddComponent<WoodResourceView>(); Ref(view, "label", label); Ref(view, "foliage", foliage);
            }
            foreach (var combat in Object.FindObjectsByType<UnitCombat>())
            {
                if (combat.Faction != UnitFaction.Player) continue;
                var gatherer = Undo.AddComponent<WoodGatherer>(combat.gameObject);
                Ref(gatherer, "settings", settings); Ref(gatherer, "stockpile", stockpile); Ref(gatherer, "invasion", invasion);
                var label = Label("Gathering Status", combat.transform, new Vector3(0, 1.3f, -.2f));
                Undo.RegisterCreatedObjectUndo(label.gameObject, "Add gathering label");
                var view = Undo.AddComponent<WoodGathererView>(combat.gameObject); Ref(view, "label", label);
            }
            var help = controls.GetComponent<UnityEngine.UI.Text>(); Undo.RecordObject(help, "Update gathering controls");
            help.text = "PROJECT F  /  ESTATE DEFENSE\nLMB / drag: select allies   RMB tree: gather   RMB enemy: attack   RMB ground: move\nGather wood and build walls before invasion. RMB / Esc cancels building tools.";
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static GameObject Quad(string name, Transform parent, Mesh mesh, Material material, Vector3 scale, Vector3 position)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material; return go;
        }
        private static TextMesh Label(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name, typeof(TextMesh)); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var text = go.GetComponent<TextMesh>(); text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.fontSize = 48; text.characterSize = .055f; text.color = new Color(.95f, .88f, .55f); text.text = ""; return text;
        }
        private static void Ref(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
