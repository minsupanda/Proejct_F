using System;
using System.Linq;
using ProjectF.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    public static class EnemyAISetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/EnemyAISettings.asset";

        [MenuItem("Project F/Open Gameplay")]
        public static void OpenGameplay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CombatSceneSetup.CreateOrOpen();
        }

        [MenuItem("Project F/Select Enemy AI Settings")]
        public static void SelectSettings()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<EnemyAISettings>(SettingsPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        /// <summary>One-time integration into the existing combat scene, without replacing units.</summary>
        [MenuItem("Project F/Setup/Add Enemy AI To Combat Scene")]
        public static void ConfigureEnemyAI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before adding enemy AI.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat before adding enemy AI.");
            var settings = AssetDatabase.LoadAssetAtPath<EnemyAISettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<EnemyAISettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var combat in Object.FindObjectsByType<UnitCombat>())
            {
                if (combat.Faction != UnitFaction.Hostile || combat.gameObject.scene != scene) continue;
                var ai = combat.GetComponent<EnemyCombatAI>();
                if (ai == null)
                {
                    ai = Undo.AddComponent<EnemyCombatAI>(combat.gameObject);
                    SetReference(ai, "settings", settings);
                }
                var view = combat.GetComponent<EnemyAIStatusView>();
                if (view != null) continue;
                var labelObject = new GameObject("AI Status", typeof(TextMesh));
                Undo.RegisterCreatedObjectUndo(labelObject, "Add enemy status label");
                labelObject.transform.SetParent(combat.transform, false);
                labelObject.transform.localPosition = new Vector3(0, 1.3f, -.2f);
                var label = labelObject.GetComponent<TextMesh>();
                label.font = font;
                label.fontSize = 48;
                label.characterSize = .055f;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.text = "GUARD";
                label.color = new Color(.7f, .85f, .8f);
                labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                view = Undo.AddComponent<EnemyAIStatusView>(combat.gameObject);
                SetReference(view, "label", label);
            }
            var help = GameObject.Find("Command HUD/Controls").GetComponent<UnityEngine.UI.Text>();
            Undo.RecordObject(help, "Update gameplay controls");
            help.text = "PROJECT F  /  FIELD SKIRMISH\nLMB / drag: select allies   RMB enemy: attack   RMB ground: move / retreat\nEnemies detect nearby allies. GUARD: idle   ATTACK: engaged   RETURN: retreating";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // The integrated gameplay scene is now the normal Build And Run entry point.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CombatSceneSetup.ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != CombatSceneSetup.ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
