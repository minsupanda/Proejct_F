using System;
using ProjectF.Economy;
using ProjectF.Invasion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class ArmySupplySetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/ArmySupplySettings.asset";
        [MenuItem("Project F/Select Army Supply Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<ArmySupplySettings>(SettingsPath);
        [MenuItem("Project F/Setup/Add Army Supply")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != CombatSceneSetup.ScenePath)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding army supply.");
            if (Object.FindAnyObjectByType<ArmySupplyHUD>() != null) return;
            var invasion = Object.FindAnyObjectByType<PortalInvasion>();
            var stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            var canvas = GameObject.Find("Command HUD");
            if (invasion == null || stockpile == null || canvas == null || canvas.GetComponent<Canvas>() == null)
                throw new InvalidOperationException("Existing invasion, stockpile and Command HUD are required.");
            var settings = AssetDatabase.LoadAssetAtPath<ArmySupplySettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<ArmySupplySettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add army supply");
            var supply = invasion.GetComponent<ArmySupply>();
            if (supply == null) supply = Undo.AddComponent<ArmySupply>(invasion.gameObject);
            Ref(supply, "invasion", invasion); Ref(supply, "stockpile", stockpile); Ref(supply, "settings", settings);
            Ref(invasion, "supply", supply);
            var root = new GameObject("Army Supply", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>(); rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-18, -404); rect.sizeDelta = new Vector2(260, 88);
            Undo.RegisterCreatedObjectUndo(root, "Add army supply HUD");
            root.AddComponent<UnityEngine.UI.Image>().color = new Color(.08f, .15f, .12f, .96f);
            var label = new GameObject("Status", typeof(RectTransform));
            var labelRect = label.GetComponent<RectTransform>(); labelRect.SetParent(rect, false);
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0, 1);
            labelRect.anchoredPosition = new Vector2(12, -8); labelRect.sizeDelta = new Vector2(236, 72);
            var text = label.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 14;
            text.color = new Color(.85f, 1, .9f); text.raycastTarget = false;
            text.text = "ARMY SUPPLY / NEXT DEFENSE\nNEED 4 / HAVE 0 FOOD\nExpected ATK 75%";
            var hud = root.AddComponent<ArmySupplyHUD>(); Ref(hud, "supply", supply); Ref(hud, "status", text);
            var pause = canvas.transform.Find("Pause HUD"); if (pause != null) rect.SetSiblingIndex(pause.GetSiblingIndex());
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void Ref(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
