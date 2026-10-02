using System;
using ProjectF.Combat;
using ProjectF.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectF.Editor
{
    public static class WallCombatSetup
    {
        [MenuItem("Project F/Setup/Add Wall Combat")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding wall combat.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectF/Development/TestGeometry.asset");
            var background = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectF/Development/CombatHealthBackground.mat");
            var fillMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectF/Development/CombatHealth.mat");
            var construction = AssetDatabase.LoadAssetAtPath<ConstructionSettings>(ConstructionSetup.SettingsPath);
            var ai = AssetDatabase.LoadAssetAtPath<EnemyAISettings>(EnemyAISetup.SettingsPath);
            if (mesh == null || background == null || fillMaterial == null || construction == null || ai == null)
                throw new InvalidOperationException("Existing construction and combat assets are required.");
            var root = PrefabUtility.LoadPrefabContents(ConstructionSetup.PrefabPath);
            try
            {
                if (root.GetComponent<WallHealthView>() == null)
                {
                    var bar = Quad("Wall Health Bar", root.transform, mesh, background);
                    bar.localPosition = new Vector3(0, .32f, -.12f);
                    bar.localScale = new Vector3(.7f, .08f, 1);
                    var fill = Quad("Health Fill", bar, mesh, fillMaterial);
                    fill.localPosition = new Vector3(0, 0, -.01f);
                    var view = root.AddComponent<WallHealthView>();
                    var data = new SerializedObject(view);
                    data.FindProperty("healthFill").objectReferenceValue = fill;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, ConstructionSetup.PrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Persist the new fields' defaults without replacing any authored settings.
            EditorUtility.SetDirty(construction); EditorUtility.SetDirty(ai);
            var help = GameObject.Find("Command HUD/Controls").GetComponent<UnityEngine.UI.Text>();
            Undo.RecordObject(help, "Update wall combat help");
            help.text = "PROJECT F  /  ESTATE DEFENSE\nLMB / drag: select allies   RMB enemy: attack   RMB ground: move / retreat\nBuild/remove walls before invasion. Enemies can break walls. RMB / Esc cancels tools.";
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static Transform Quad(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
