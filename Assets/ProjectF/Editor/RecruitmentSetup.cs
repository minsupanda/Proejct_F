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
    public static class RecruitmentSetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/RecruitmentSettings.asset";
        public const string PrefabPath = "Assets/ProjectF/Prefabs/PlayerSoldier.prefab";
        [MenuItem("Project F/Select Recruitment Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<RecruitmentSettings>(SettingsPath);

        [MenuItem("Project F/Setup/Add Unit Recruitment")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding recruitment.");
            if (Object.FindAnyObjectByType<UnitRecruitment>() != null) return;
            var source = GameObject.Find("Test Unit 2");
            var invasion = Object.FindAnyObjectByType<PortalInvasion>();
            var stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            var navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            var panel = GameObject.Find("Command HUD/Construction Panel");
            if (source == null || invasion == null || stockpile == null || navigation == null || panel == null)
                throw new InvalidOperationException("Existing gameplay scene dependencies are required.");
            var settings = AssetDatabase.LoadAssetAtPath<RecruitmentSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<RecruitmentSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var copy = Object.Instantiate(source); copy.name = "Player Soldier"; copy.SetActive(false);
                copy.transform.position = Vector3.zero;
                Ref(copy.GetComponent<CommandableUnit>(), "navigation", null);
                Ref(copy.GetComponent<CommandableUnit>(), "destinationVisual", null);
                Ref(copy.GetComponent<WoodGatherer>(), "stockpile", null);
                Ref(copy.GetComponent<WoodGatherer>(), "invasion", null);
                prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
                Object.DestroyImmediate(copy);
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add unit recruitment");
            var root = new GameObject("Recruit Camp"); Undo.RegisterCreatedObjectUndo(root, "Add recruitment camp");
            root.transform.position = new Vector3(-13, 1, 0);
            var recruitment = Undo.AddComponent<UnitRecruitment>(root);
            Ref(recruitment, "settings", settings); Ref(recruitment, "stockpile", stockpile); Ref(recruitment, "invasion", invasion);
            Ref(recruitment, "navigation", navigation); Ref(recruitment, "soldierPrefab", prefab.GetComponent<UnitCombat>());
            var positions = new[] { new Vector3(-12, 0, 0), new Vector3(-12, 2, 0), new Vector3(-14, 0, 0) };
            var data = new SerializedObject(recruitment); var exits = data.FindProperty("exits"); exits.arraySize = positions.Length;
            for (int i = 0; i < positions.Length; i++)
            {
                var exit = new GameObject("Camp Exit " + (i + 1)).transform;
                exit.SetParent(root.transform, false); exit.position = positions[i];
                exits.GetArrayElementAtIndex(i).objectReferenceValue = exit;
            }
            data.ApplyModifiedProperties();
            var ring = new GameObject("Camp Marker", typeof(LineRenderer)).GetComponent<LineRenderer>();
            ring.transform.SetParent(root.transform, false); ring.useWorldSpace = false; ring.loop = true; ring.widthMultiplier = .055f;
            ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectF/Development/EstateBoundary.mat");
            ring.positionCount = 4; ring.SetPositions(new[] {new Vector3(-.65f,-.65f,.1f), new Vector3(-.65f,.65f,.1f), new Vector3(.65f,.65f,.1f), new Vector3(.65f,-.65f,.1f)});
            var campText = new GameObject("Camp Label", typeof(TextMesh)).GetComponent<TextMesh>();
            campText.transform.SetParent(root.transform, false); campText.transform.localPosition = new Vector3(0, 1.05f, -.2f);
            campText.text = "RECRUIT CAMP"; campText.anchor = TextAnchor.MiddleCenter; campText.fontSize = 48; campText.characterSize = .05f;
            campText.color = new Color(.5f, .9f, .85f);

            var hudRoot = UI("Recruitment", panel.transform, new Vector2(0,.5f), new Vector2(18,-20), new Vector2(480,46));
            Undo.RegisterCreatedObjectUndo(hudRoot.gameObject, "Add recruitment HUD");
            var buttonRect = UI("Train Soldier", hudRoot, new Vector2(0,.5f), Vector2.zero, new Vector2(166,40));
            var image = buttonRect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.12f,.35f,.33f);
            var button = buttonRect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Text(UI("Label", buttonRect, new Vector2(.5f,.5f), Vector2.zero, new Vector2(162,38)), 16);
            label.text = "TRAIN SOLDIER"; label.alignment = TextAnchor.MiddleCenter;
            var status = Text(UI("Status", hudRoot, new Vector2(0,.5f), new Vector2(178,0), new Vector2(298,46)), 14);
            status.text = "ARMY 4/8 / 20 WOOD\nReady / 5s";
            var hud = hudRoot.gameObject.AddComponent<RecruitmentHUD>();
            Ref(hud, "recruitment", recruitment); Ref(hud, "status", status); Ref(hud, "button", button); Ref(hud, "buttonLabel", label);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static RectTransform UI(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static UnityEngine.UI.Text Text(RectTransform rect, int size)
        {
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.alignment = TextAnchor.MiddleLeft; text.color = new Color(.9f,.95f,.92f); text.raycastTarget = false; return text;
        }
        private static void Ref(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
