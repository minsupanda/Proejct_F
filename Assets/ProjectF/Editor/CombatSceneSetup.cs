using System;
using System.IO;
using System.Linq;
using ProjectF.Combat;
using ProjectF.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectF.Editor
{
    /// <summary>Builds an independent combat sandbox once. Reopening preserves authored changes.</summary>
    public static class CombatSceneSetup
    {
        public const string ScenePath = "Assets/ProjectF/Scenes/BasicCombat.unity";
        public const string PlayerSettingsPath = "Assets/ProjectF/Data/PlayerCombatSettings.asset";
        public const string EnemySettingsPath = "Assets/ProjectF/Data/EnemyCombatSettings.asset";
        private const string GeometryPath = "Assets/ProjectF/Development/TestGeometry.asset";

        [MenuItem("Project F/Open Basic Combat Test")]
        public static void OpenCombatTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateOrOpen();
        }

        [MenuItem("Project F/Select Player Combat Settings")]
        public static void SelectPlayerSettings() => SelectSettings(PlayerSettingsPath);
        [MenuItem("Project F/Select Enemy Combat Settings")]
        public static void SelectEnemySettings() => SelectSettings(EnemySettingsPath);

        private static void SelectSettings(string path)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<CombatSettings>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        // Used by automation too: refuse to discard unsaved scenes, even without a UI prompt.
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before opening the combat test.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your scene changes before opening the combat test.");
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            if (!AssetDatabase.CopyAsset(InputCameraSceneSetup.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy the movement test scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var allies = Object.FindObjectsByType<CommandableUnit>().OrderBy(u => u.name).ToArray();
            var player = Settings(PlayerSettingsPath, false);
            var enemy = Settings(EnemySettingsPath, true);
            var quad = AssetDatabase.LoadAssetAtPath<Mesh>(GeometryPath);
            var enemyMaterial = Material("CombatEnemy", new Color(.85f, .22f, .2f));
            var healthMaterial = Material("CombatHealth", new Color(.25f, .9f, .55f));
            var backgroundMaterial = Material("CombatHealthBackground", new Color(.12f, .15f, .18f));
            var strikeMaterial = AssetDatabase.LoadAllAssetsAtPath(GeometryPath).OfType<Material>().First(m => m.name == "Pale");

            // Clone before adding combat so each unit gets independent visuals and references.
            for (int i = 0; i < 3; i++)
            {
                var hostile = Object.Instantiate(allies[0]);
                hostile.name = "Enemy " + (i + 1);
                hostile.transform.position = new Vector3(i == 2 ? 7 : 4, i == 0 ? 1 : i == 1 ? -2 : -.5f, 0);
                SetReference(hostile, "destinationVisual", null);
                foreach (var renderer in hostile.transform.Find("Placeholder Visual").GetComponentsInChildren<MeshRenderer>())
                    if (renderer.name != "Shadow") renderer.sharedMaterial = enemyMaterial;
                AddCombat(hostile, enemy, UnitFaction.Hostile, true);
            }
            for (int i = 0; i < allies.Length; i++)
            {
                allies[i].transform.position = new Vector3(i % 2 == 0 ? -6 : -3, i < 2 ? 1 : -2, 0);
                AddCombat(allies[i], player, UnitFaction.Player, false);
            }

            void AddCombat(CommandableUnit unit, CombatSettings settings, UnitFaction faction, bool retaliate)
            {
                var combat = unit.gameObject.AddComponent<UnitCombat>();
                var serialized = new SerializedObject(combat);
                serialized.FindProperty("settings").objectReferenceValue = settings;
                serialized.FindProperty("faction").enumValueIndex = (int)faction;
                serialized.FindProperty("retaliateWhenIdle").boolValue = retaliate;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var bar = new GameObject("Health Bar", typeof(MeshFilter), typeof(MeshRenderer));
                bar.transform.SetParent(unit.transform, false);
                bar.transform.localPosition = new Vector3(0, .95f, -.2f);
                bar.transform.localScale = new Vector3(1, .12f, 1);
                bar.GetComponent<MeshFilter>().sharedMesh = quad;
                bar.GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
                var fill = new GameObject("Health Fill", typeof(MeshFilter), typeof(MeshRenderer));
                fill.transform.SetParent(bar.transform, false);
                fill.transform.localPosition = new Vector3(0, 0, -.01f);
                fill.GetComponent<MeshFilter>().sharedMesh = quad;
                fill.GetComponent<MeshRenderer>().sharedMaterial = healthMaterial;
                var strike = new GameObject("Melee Strike", typeof(LineRenderer)).GetComponent<LineRenderer>();
                strike.transform.SetParent(unit.transform, false);
                strike.sharedMaterial = strikeMaterial;
                strike.positionCount = 2;
                strike.widthMultiplier = .065f;
                strike.useWorldSpace = true;
                strike.enabled = false;
                var feedback = unit.gameObject.AddComponent<CombatFeedback>();
                SetReference(feedback, "healthFill", fill.transform);
                SetReference(feedback, "strike", strike);
            }

            foreach (var label in Object.FindObjectsByType<TextMesh>())
                if (label.text == "MOUSE COMMAND TEST") label.text = "BASIC COMBAT TEST";
            var hud = GameObject.Find("Command HUD/Controls").GetComponent<UnityEngine.UI.Text>();
            hud.text = "PROJECT F  /  BASIC COMBAT\nLMB / drag: select allies   RMB enemy: attack   RMB ground: move / retreat\nRed: enemies (retaliate when hit)   Green bars: health   Stop + Play: restart";
            hud.rectTransform.sizeDelta = new Vector2(-24, 90);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = allies[0].gameObject;
        }

        private static CombatSettings Settings(string path, bool enemy)
        {
            var result = AssetDatabase.LoadAssetAtPath<CombatSettings>(path);
            if (result != null) return result;
            result = ScriptableObject.CreateInstance<CombatSettings>();
            if (enemy)
            {
                var serialized = new SerializedObject(result);
                serialized.FindProperty("maxHealth").intValue = 75;
                serialized.FindProperty("attackDamage").intValue = 10;
                serialized.FindProperty("attackInterval").floatValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/ProjectF/Development/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
