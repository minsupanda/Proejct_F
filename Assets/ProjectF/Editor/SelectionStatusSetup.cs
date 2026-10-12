using System;
using ProjectF.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class SelectionStatusSetup
    {
        [MenuItem("Project F/Setup/Add Selection Status")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding selection status.");
            if (Object.FindAnyObjectByType<SelectionStatusHUD>() != null) return;
            var commands = Object.FindAnyObjectByType<UnitCommandController>();
            var canvas = GameObject.Find("Command HUD");
            if (commands == null || canvas == null || canvas.GetComponent<Canvas>() == null)
                throw new InvalidOperationException("Existing commands and Command HUD canvas are required.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add selection status");
            var root = new GameObject("Selection Status", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Add selection status"); root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(18, -100); rect.sizeDelta = new Vector2(230, 110);
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(root.transform, false); Stretch((RectTransform)panel.transform, 0);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.04f, .10f, .11f, .94f);
            // A visible panel owns pointer input so clicks cannot select/order units behind it.
            var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(panel.transform, false); Stretch((RectTransform)label.transform, 10);
            var text = label.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft; text.color = new Color(.9f, .95f, .92f); text.raycastTarget = false;
            panel.SetActive(false);
            var hud = root.AddComponent<SelectionStatusHUD>(); var data = new SerializedObject(hud);
            data.FindProperty("commands").objectReferenceValue = commands;
            data.FindProperty("panel").objectReferenceValue = panel;
            data.FindProperty("status").objectReferenceValue = text; data.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }
    }
}
