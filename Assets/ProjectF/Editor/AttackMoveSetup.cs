using System;
using ProjectF.Combat;
using ProjectF.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class AttackMoveSetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/AttackMoveSettings.asset";
        [MenuItem("Project F/Select Attack Move Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<AttackMoveSettings>(SettingsPath);
        [MenuItem("Project F/Setup/Add Attack Move")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding attack move.");
            var canvas = GameObject.Find("Command HUD"); var summary = Object.FindAnyObjectByType<SelectionStatusHUD>();
            if (canvas == null || summary == null) throw new InvalidOperationException("Command HUD and selection status are required.");
            var settings = AssetDatabase.LoadAssetAtPath<AttackMoveSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<AttackMoveSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            foreach (var combat in Object.FindObjectsByType<UnitCombat>())
                if (combat.Faction == UnitFaction.Player && combat.gameObject.scene == scene) ConfigureUnit(combat.gameObject, settings);
            var prefab = PrefabUtility.LoadPrefabContents(RecruitmentSetup.PrefabPath);
            try { ConfigureUnit(prefab, settings); PrefabUtility.SaveAsPrefabAsset(prefab, RecruitmentSetup.PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var summaryRect = (RectTransform)summary.transform;
            Undo.RecordObject(summaryRect, "Make room for advance count"); summaryRect.sizeDelta = new Vector2(summaryRect.sizeDelta.x, 132);
            if (canvas.transform.Find("Attack Move Help") == null)
            {
                var rect = new GameObject("Attack Move Help", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "Add attack move help"); rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
                rect.anchoredPosition = new Vector2(0, -80); rect.sizeDelta = new Vector2(700, 24);
                var label = rect.GetComponent<UnityEngine.UI.Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 18; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(.8f, .92f, .85f); label.raycastTarget = false;
                label.text = "Hold A + RMB: attack move / Fight nearby enemies, then continue";
                var pause = canvas.transform.Find("Pause HUD"); if (pause != null) rect.SetSiblingIndex(pause.GetSiblingIndex());
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void ConfigureUnit(GameObject unit, AttackMoveSettings settings)
        {
            var order = unit.GetComponent<AttackMoveOrder>(); if (order != null) return;
            order = Undo.AddComponent<AttackMoveOrder>(unit);
            var data = new SerializedObject(order); data.FindProperty("settings").objectReferenceValue = settings; data.ApplyModifiedProperties();
        }
    }
}
