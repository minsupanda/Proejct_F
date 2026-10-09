using System;
using ProjectF.Combat;
using ProjectF.Construction;
using ProjectF.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class RecruitmentRallySetup
    {
        [MenuItem("Project F/Setup/Add Recruitment Rally")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding rally controls.");
            if (Object.FindAnyObjectByType<RecruitmentRallyController>() != null) return;
            var recruitment = Object.FindAnyObjectByType<UnitRecruitment>();
            var placement = Object.FindAnyObjectByType<WallPlacementController>();
            var commands = Object.FindAnyObjectByType<UnitCommandController>();
            var canvas = GameObject.Find("Command HUD");
            if (recruitment == null || placement == null || commands == null || canvas == null)
                throw new InvalidOperationException("Existing recruitment and construction scene dependencies are required.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add recruitment rally");
            var root = new GameObject("Recruitment Rally"); Undo.RegisterCreatedObjectUndo(root, "Add rally tool");
            var controller = root.AddComponent<RecruitmentRallyController>();
            Ref(controller, "recruitment", recruitment); Ref(controller, "placement", placement); Ref(controller, "commands", commands);
            Ref(controller, "input", Object.FindAnyObjectByType<CommandInput>()); Ref(controller, "worldCamera", Camera.main);
            // Reuse the line shader already used by the estate boundary. No runtime material instances.
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectF/Development/EstateBoundary.mat");
            var marker = Marker("Rally Flag", root.transform, material, true);
            var preview = Marker("Rally Preview", root.transform, material, false);
            Ref(controller, "marker", marker); Ref(controller, "preview", preview);
            var recruitmentHUD = Object.FindAnyObjectByType<RecruitmentHUD>();
            var normalStatus = recruitmentHUD.transform.Find("Status").GetComponent<Text>();
            Undo.RecordObjects(new Object[] {normalStatus, normalStatus.rectTransform}, "Make room for rally buttons");
            normalStatus.rectTransform.sizeDelta = new Vector2(168,46); normalStatus.fontSize = 13;
            var panel = Rect("Rally Panel", recruitmentHUD.transform, new Vector2(0,.5f), new Vector2(354,0), new Vector2(126,44));
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Add rally HUD");
            var set = Button("Set Rally", panel, 0, 126, "SET RALLY");
            var clear = Button("Clear Rally", panel, 0, 126, "CLEAR RALLY");
            set.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,11);
            clear.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-11);
            var status = Text(Rect("Status", panel, new Vector2(0,.5f), new Vector2(-176,0), new Vector2(168,46)), 13);
            status.text = "Click open ground\nRMB / Esc: cancel"; status.gameObject.SetActive(false);
            var hud = panel.gameObject.AddComponent<RecruitmentRallyHUD>();
            Ref(hud, "recruitment", recruitment); Ref(hud, "controller", controller); Ref(hud, "setButton", set); Ref(hud, "clearButton", clear);
            Ref(hud, "setLabel", set.GetComponentInChildren<Text>()); Ref(hud, "status", status);
            Ref(hud, "recruitmentStatus", normalStatus.gameObject);
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        private static LineRenderer Marker(string name, Transform parent, Material material, bool flag)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false); line.useWorldSpace = false; line.widthMultiplier = .07f;
            line.sharedMaterial = material; line.startColor = line.endColor = new Color(.25f, 1f, .8f);
            var points = flag ? new[] {new Vector3(0,-.4f,0),new Vector3(0,1.05f,0),new Vector3(.65f,.8f,0),new Vector3(0,.55f,0)}
                : new[] {new Vector3(-.4f,0,0),new Vector3(0,.4f,0),new Vector3(.4f,0,0),new Vector3(0,-.4f,0),new Vector3(-.4f,0,0)};
            line.positionCount = points.Length; line.SetPositions(points); line.enabled = false;
            return line;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Text Text(RectTransform rect, int size)
        {
            var text = rect.gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.alignment = TextAnchor.MiddleLeft; text.color = new Color(.9f,.95f,.92f); text.raycastTarget = false; return text;
        }
        private static Button Button(string name, Transform parent, float x, float width, string caption)
        {
            var rect = Rect(name, parent, new Vector2(0,.5f), new Vector2(x,0), new Vector2(width,20));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.1f,.32f,.3f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = Text(Rect("Label", rect, new Vector2(.5f,.5f), Vector2.zero, new Vector2(width-4,20)), 14);
            text.text = caption; text.alignment = TextAnchor.MiddleCenter; return button;
        }
        private static void Ref(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedProperties();
        }
    }
}
