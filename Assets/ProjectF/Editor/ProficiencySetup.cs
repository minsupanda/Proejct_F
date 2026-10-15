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
    public static class ProficiencySetup
    {
        public const string SettingsPath = "Assets/ProjectF/Data/ProficiencySettings.asset";
        [MenuItem("Project F/Select Proficiency Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<ProficiencySettings>(SettingsPath);
        [MenuItem("Project F/Setup/Add Soldier Proficiency")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CombatSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BasicCombat and stop Play before adding proficiency.");
            var hud = Object.FindAnyObjectByType<SelectionStatusHUD>();
            var soldiers = new GameObject[3];
            for (int i = 0; i < soldiers.Length; i++)
            {
                soldiers[i] = GameObject.Find("Test Unit " + (i + 2));
                if (soldiers[i] == null || soldiers[i].GetComponent<UnitCombat>() == null)
                    throw new InvalidOperationException("The three initial soldiers are required.");
            }
            if (hud == null) throw new InvalidOperationException("Selection status is required.");
            var settings = AssetDatabase.LoadAssetAtPath<ProficiencySettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<ProficiencySettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            foreach (var soldier in soldiers) ConfigureUnit(soldier, settings);
            var prefab = PrefabUtility.LoadPrefabContents(RecruitmentSetup.PrefabPath);
            try { ConfigureUnit(prefab, settings); PrefabUtility.SaveAsPrefabAsset(prefab, RecruitmentSetup.PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var rect = (RectTransform)hud.transform;
            Undo.RecordObject(rect, "Make room for proficiency"); rect.sizeDelta = new Vector2(rect.sizeDelta.x, 154);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void ConfigureUnit(GameObject unit, ProficiencySettings settings)
        {
            var proficiency = unit.GetComponent<SoldierProficiency>();
            if (proficiency == null) proficiency = Undo.AddComponent<SoldierProficiency>(unit);
            var data = new SerializedObject(proficiency); data.FindProperty("settings").objectReferenceValue = settings; data.ApplyModifiedProperties();
        }
    }
}
