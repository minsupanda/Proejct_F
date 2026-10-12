using System;
using ProjectF.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class GamePauseSetup
    {
        [MenuItem("Project F/Setup/Add Game Pause")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding pause controls.");
            if (Object.FindAnyObjectByType<GamePause>() != null) return;
            var commands = Object.FindAnyObjectByType<UnitCommandController>();
            var input = Object.FindAnyObjectByType<CommandInput>();
            var canvas = GameObject.Find("Command HUD");
            if (commands == null || input == null || canvas == null || canvas.GetComponent<Canvas>() == null)
                throw new InvalidOperationException("Existing input, commands and Command HUD are required.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add game pause");
            var pause = Undo.AddComponent<GamePause>(commands.gameObject);
            Ref(pause, "input", input); Ref(pause, "commands", commands);
            var root = Rect("Pause HUD", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Add pause HUD"); Stretch(root);
            var pauseButton = Button("Pause", root, new Vector2(1, 1), new Vector2(-18, -18), "PAUSE / SPACE");
            var overlay = Rect("Overlay", root, Vector2.zero, Vector2.zero, Vector2.zero); Stretch(overlay);
            overlay.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.015f, .04f, .05f, .88f);
            Label("Title", overlay, new Vector2(0, 80), new Vector2(600, 55), "PAUSED", 36);
            Label("Hint", overlay, new Vector2(0, 22), new Vector2(600, 50), "Your orders and progress are kept.\nPress SPACE or RESUME to continue.", 20);
            var resume = Button("Resume", overlay, new Vector2(.5f, .5f), new Vector2(0, -55), "RESUME");
            overlay.gameObject.SetActive(false);
            var hud = root.gameObject.AddComponent<PauseHUD>();
            Ref(hud, "pause", pause); Ref(hud, "pauseButton", pauseButton); Ref(hud, "resumeButton", resume); Ref(hud, "overlay", overlay.gameObject);
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Label(string name, Transform parent, Vector2 position, Vector2 size, string caption, int fontSize)
        {
            var rect = Rect(name, parent, new Vector2(.5f, .5f), position, size);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
            text.text = caption; text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.9f, .95f, .92f); text.raycastTarget = false;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, Vector2 anchor, Vector2 position, string caption)
        {
            var rect = Rect(name, parent, anchor, position, new Vector2(160, 42));
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.1f, .32f, .3f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            // Keyboard pause is owned by CommandInput, never by a remembered UI selection.
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Label("Label", rect, Vector2.zero, new Vector2(156, 40), caption, 18); return button;
        }
        private static void Ref(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
