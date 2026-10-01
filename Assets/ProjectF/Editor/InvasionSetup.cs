using System;
using System.Linq;
using ProjectF.Combat;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class InvasionSetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/FirstInvasionSettings.asset";
        public const string PrefabPath = "Assets/ProjectF/Prefabs/PortalRaider.prefab";

        [MenuItem("Project F/Select Invasion Settings")]
        public static void SelectSettings()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<InvasionSettings>(SettingsPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Project F/Setup/Add First Portal Invasion")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat, then stop Play before integrating the invasion.");
            if (Object.FindAnyObjectByType<PortalInvasion>() != null) return;
            var hostiles = Object.FindObjectsByType<UnitCombat>().Where(c => c.Faction == UnitFaction.Hostile).OrderBy(c => c.name).ToArray();
            var canvas = GameObject.Find("Command HUD").GetComponent<Canvas>();
            var navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            if (hostiles.Length == 0 || canvas == null || navigation == null)
                throw new InvalidOperationException("Existing combat units, HUD and navigation are required.");

            if (!AssetDatabase.IsValidFolder("Assets/ProjectF/Prefabs")) AssetDatabase.CreateFolder("Assets/ProjectF", "Prefabs");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var copy = Object.Instantiate(hostiles[0].gameObject);
                copy.name = "Portal Raider";
                copy.SetActive(false);
                copy.transform.position = Vector3.zero;
                SetReference(copy.GetComponent<CommandableUnit>(), "navigation", null);
                SetReference(copy.GetComponent<CommandableUnit>(), "destinationVisual", null);
                prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
                Object.DestroyImmediate(copy);
            }
            var settings = AssetDatabase.LoadAssetAtPath<InvasionSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<InvasionSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Integrate first portal invasion");
            var root = Create("Portal Invasion", null);
            var director = Undo.AddComponent<PortalInvasion>(root);
            SetReference(director, "settings", settings);
            SetReference(director, "enemyPrefab", prefab.GetComponent<UnitCombat>());
            SetReference(director, "navigation", navigation);
            var destination = Create("Invasion Destination", root.transform).transform;
            destination.position = new Vector3(-4, 0, 0);
            SetReference(director, "destination", destination);
            var portal = Create("Portal", root.transform);
            portal.transform.position = new Vector3(8, 3, .15f);
            var ring = Undo.AddComponent<LineRenderer>(portal);
            ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectF/Development/CombatEnemy.mat");
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = .16f;
            ring.positionCount = 48;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.3f, Mathf.Sin(angle) * 2.2f, 0));
            }
            var label = Undo.AddComponent<TextMesh>(Create("Portal Label", portal.transform));
            label.transform.localPosition = new Vector3(0, 2.6f, -.2f);
            label.text = "MONSTER PORTAL";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48; label.characterSize = .065f;
            label.anchor = TextAnchor.MiddleCenter;
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            var exits = new Transform[3];
            for (int i = 0; i < exits.Length; i++)
            {
                exits[i] = Create("Exit " + (i + 1), portal.transform).transform;
                exits[i].position = new Vector3(8, 3 + (i - 1) * 1.5f, 0);
            }
            var serialized = new SerializedObject(director);
            var exitArray = serialized.FindProperty("exits"); exitArray.arraySize = exits.Length;
            for (int i = 0; i < exits.Length; i++) exitArray.GetArrayElementAtIndex(i).objectReferenceValue = exits[i];
            var allies = Object.FindObjectsByType<UnitCombat>().Where(c => c.Faction == UnitFaction.Player).ToArray();
            var defenderArray = serialized.FindProperty("defenders"); defenderArray.arraySize = allies.Length;
            for (int i = 0; i < allies.Length; i++) defenderArray.GetArrayElementAtIndex(i).objectReferenceValue = allies[i];
            serialized.ApplyModifiedProperties();

            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) Undo.AddComponent<UnityEngine.UI.GraphicRaycaster>(canvas.gameObject);
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = Create("UI Event System", null);
                Undo.AddComponent<EventSystem>(events);
                Undo.AddComponent<InputSystemUIInputModule>(events).AssignDefaultActions();
            }
            var panel = CreateUI("Invasion Panel", canvas.transform, new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(930, 62));
            var background = Undo.AddComponent<UnityEngine.UI.Image>(panel.gameObject);
            background.color = new Color(.035f, .075f, .095f, .96f);
            var statusRect = CreateUI("Status", panel, new Vector2(0, .5f), new Vector2(18, 0), new Vector2(680, 48));
            var status = Text(statusRect, 18);
            status.text = "FIRST INVASION  /  Position your units, then open the portal";
            var buttonRect = CreateUI("Start Invasion", panel, new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(192, 42));
            var image = Undo.AddComponent<UnityEngine.UI.Image>(buttonRect.gameObject);
            image.color = new Color(.15f, .4f, .38f, 1);
            var button = Undo.AddComponent<UnityEngine.UI.Button>(buttonRect.gameObject);
            button.targetGraphic = image;
            var buttonTextRect = CreateUI("Label", buttonRect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(188, 40));
            var buttonText = Text(buttonTextRect, 18);
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.text = "START INVASION";
            var hud = Undo.AddComponent<InvasionHUD>(panel.gameObject);
            SetReference(hud, "invasion", director);
            SetReference(hud, "status", status);
            SetReference(hud, "action", button);
            SetReference(hud, "actionLabel", buttonText);
            var help = canvas.transform.Find("Controls").GetComponent<UnityEngine.UI.Text>();
            Undo.RecordObject(help, "Update invasion controls");
            help.text = "PROJECT F  /  FIRST PORTAL INVASION\nLMB / drag: select allies   RMB enemy: attack   RMB ground: move / retreat\nStart invasion when ready. Raiders advance from the portal on the right.";

            // The old placed opponents become the reusable prefab. Regression tests instantiate it explicitly.
            foreach (var hostile in hostiles) Undo.DestroyObjectImmediate(hostile.gameObject);
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static GameObject Create(string name, Transform parent)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add invasion object");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }
        private static RectTransform CreateUI(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Add invasion UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
        private static UnityEngine.UI.Text Text(RectTransform rect, int size)
        {
            var text = Undo.AddComponent<UnityEngine.UI.Text>(rect.gameObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = new Color(.9f, .97f, .95f);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }
        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
