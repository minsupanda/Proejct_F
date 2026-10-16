using System;
using ProjectF.Combat;
using ProjectF.Invasion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class LordGrowthSetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/LordGrowthSettings.asset";
        [MenuItem("Project F/Select Lord Growth Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<LordGrowthSettings>(SettingsPath);
        [MenuItem("Project F/Setup/Add Lord Growth")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding lord growth.");
            if (Object.FindAnyObjectByType<LordGrowthHUD>() != null) return;
            var lord = GameObject.Find("Lord");
            var invasion = Object.FindAnyObjectByType<PortalInvasion>();
            var recruitment = Object.FindAnyObjectByType<UnitRecruitment>();
            var canvas = GameObject.Find("Command HUD");
            if (lord == null || lord.GetComponent<UnitCombat>() == null || invasion == null || recruitment == null
                || canvas == null || canvas.GetComponent<Canvas>() == null)
                throw new InvalidOperationException("Existing lord, invasion, recruitment and Command HUD are required.");
            var settings = AssetDatabase.LoadAssetAtPath<LordGrowthSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<LordGrowthSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add lord growth");
            var growth = lord.GetComponent<LordGrowth>();
            if (growth == null) growth = Undo.AddComponent<LordGrowth>(lord);
            Ref(growth, "settings", settings); Ref(growth, "invasion", invasion); Ref(recruitment, "lordGrowth", growth);
            var root = Rect("Lord Growth", canvas.transform, new Vector2(1,1), new Vector2(-18,-82), new Vector2(260,170));
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Add lord growth panel");
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.04f,.10f,.11f,.95f);
            var status = Label("Status", root, new Vector2(12,-10), new Vector2(236,62), 14);
            status.text = "LORD GROWTH / POINTS 0\nLORD ATK +0 / ARMY +0\nRepel invasions to earn points";
            var martial = Button("Martial", root, -80, "MARTIAL 0/3 / ATK +2");
            var leadership = Button("Leadership", root, -123, "LEADERSHIP 0/3 / ARMY +1");
            var hud = root.gameObject.AddComponent<LordGrowthHUD>();
            Ref(hud,"growth",growth); Ref(hud,"status",status);
            Ref(hud,"martialButton",martial); Ref(hud,"leadershipButton",leadership);
            Ref(hud,"martialLabel",martial.GetComponentInChildren<UnityEngine.UI.Text>());
            Ref(hud,"leadershipLabel",leadership.GetComponentInChildren<UnityEngine.UI.Text>());
            // Pause overlay must remain above every gameplay button.
            var pause = canvas.transform.Find("Pause HUD"); if (pause != null) root.SetSiblingIndex(pause.GetSiblingIndex());
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static UnityEngine.UI.Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Rect(name,parent,new Vector2(0,1),position,size);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft; text.color = new Color(.9f,.95f,.92f); text.raycastTarget = false; return text;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, float y, string caption)
        {
            var rect = Rect(name,parent,new Vector2(0,1),new Vector2(12,y),new Vector2(236,34));
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.12f,.35f,.33f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation {mode = UnityEngine.UI.Navigation.Mode.None};
            var label = Label("Label",rect,new Vector2(2,0),new Vector2(232,34),15);
            label.text = caption; label.alignment = TextAnchor.MiddleCenter; return button;
        }
        private static void Ref(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
