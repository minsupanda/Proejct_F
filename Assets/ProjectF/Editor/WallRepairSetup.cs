using System;
using ProjectF.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class WallRepairSetup
    {
        [MenuItem("Project F/Setup/Add Wall Repair")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding wall repair.");
            var hud = Object.FindAnyObjectByType<ConstructionHUD>();
            if (hud == null) throw new InvalidOperationException("Existing construction HUD is required.");
            if (hud.transform.Find("Repair Wall") != null) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add wall repair");
            var status = hud.transform.Find("Status").GetComponent<RectTransform>();
            Undo.RecordObjects(new Object[] {status, hud}, "Make room for repair button");
            status.sizeDelta = new Vector2(680, 32);
            var button = new GameObject("Repair Wall", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            Undo.RegisterCreatedObjectUndo(button, "Add repair button");
            var rect = button.GetComponent<RectTransform>(); rect.SetParent(hud.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-14, -6); rect.sizeDelta = new Vector2(192, 32);
            var image = button.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.12f,.33f,.44f);
            var action = button.GetComponent<UnityEngine.UI.Button>(); action.targetGraphic = image;
            var label = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            label.transform.SetParent(rect, false); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 16; label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(.9f,.95f,1f); label.raycastTarget = false; label.text = "REPAIR WALL";
            var data = new SerializedObject(hud); data.FindProperty("repairButton").objectReferenceValue = action;
            data.FindProperty("repairLabel").objectReferenceValue = label; data.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
