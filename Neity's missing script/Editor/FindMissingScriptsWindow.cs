using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Linq;
using System.Collections.Generic;

public class FindMissingScriptsWindow : EditorWindow
{
    private bool searchInHierarchy = true;
    private bool searchInAssets = true;
    private bool searchInPackages = true;
    private bool deleteMissingScripts = false;
    private bool deleteAllMissingScripts = false;

    private Vector2 scrollPosition;
    private List<GameObject> missingScriptsObjects = new List<GameObject>();

    [MenuItem("Tools/Find Missing Scripts")]
    static void ShowWindow()
    {
        FindMissingScriptsWindow window = GetWindow<FindMissingScriptsWindow>();
        window.titleContent = new GUIContent("Find Missing Scripts");
        window.minSize = new Vector2(400f, 300f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10f);
        GUILayout.Label("Search Options", EditorStyles.boldLabel);

        searchInHierarchy = EditorGUILayout.Toggle("Search in Hierarchy", searchInHierarchy);
        searchInAssets = EditorGUILayout.Toggle("Search in Assets", searchInAssets);
        searchInPackages = EditorGUILayout.Toggle("Search in Packages", searchInPackages);

        EditorGUILayout.Space(20f);

        if (GUILayout.Button("Find Missing Scripts"))
        {
            FindMissingScripts();
        }

        EditorGUILayout.Space(20f);
        GUILayout.Label("Missing Scripts", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (missingScriptsObjects.Count == 0)
        {
            EditorGUILayout.LabelField("No missing scripts found.");
        }
        else
        {
            foreach (GameObject missingObject in missingScriptsObjects)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(missingObject.name, EditorStyles.boldLabel);

                if (GUILayout.Button("Locate", GUILayout.Width(60f)))
                {
                    Selection.activeObject = missingObject;
                    EditorGUIUtility.PingObject(missingObject);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(20f);
        GUILayout.Label("Delete Missing Scripts", EditorStyles.boldLabel);

        deleteMissingScripts = EditorGUILayout.Toggle("Delete Missing Scripts", deleteMissingScripts);

        if (deleteMissingScripts)
        {
            deleteAllMissingScripts = EditorGUILayout.Toggle("Delete All Missing Scripts", deleteAllMissingScripts);

            if (GUILayout.Button("Remove Missing Scripts"))
            {
                RemoveMissingScripts();
            }
        }
    }

    private void FindMissingScripts()
    {
        missingScriptsObjects.Clear();

        if (searchInHierarchy)
        {
            UnityEngine.SceneManagement.Scene currentScene = EditorSceneManager.GetActiveScene();
            GameObject[] rootObjects = currentScene.GetRootGameObjects();

            foreach (GameObject rootObject in rootObjects)
            {
                FindMissingScriptsRecursive(rootObject);
            }
        }

        if (searchInAssets)
        {
            string[] assetGuids = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");

            foreach (string assetGuid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab != null)
                {
                    FindMissingScriptsRecursive(prefab);
                }
            }
        }

        if (searchInPackages)
        {
            ListRequest listRequest = Client.List();
            while (!listRequest.IsCompleted) { }

            if (listRequest.Status == StatusCode.Success)
            {
                foreach (UnityEditor.PackageManager.PackageInfo package in listRequest.Result)
                {
                    if (package.assetPath != null && package.assetPath.StartsWith("Packages/"))
                    {
                        string[] assetGuids = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject", new string[] { package.assetPath });

                        foreach (string assetGuid in assetGuids)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                            if (prefab != null)
                            {
                                FindMissingScriptsRecursive(prefab);
                            }
                        }
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Package Manager Error", "Failed to retrieve package list.", "OK");
            }
        }

        Repaint();
    }

    private void FindMissingScriptsRecursive(GameObject gameObject)
    {
        Component[] components = gameObject.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null)
            {
                missingScriptsObjects.Add(gameObject);
                break;
            }
        }

        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            GameObject childObject = gameObject.transform.GetChild(i).gameObject;
            FindMissingScriptsRecursive(childObject);
        }
    }

    private void RemoveMissingScripts()
    {
        foreach (GameObject missingObject in missingScriptsObjects)
        {
            SerializedObject serializedObject = new SerializedObject(missingObject);
            SerializedProperty serializedProperty = serializedObject.FindProperty("m_Component");

            int removedCount = 0;

            for (int i = 0; i < serializedProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = serializedProperty.GetArrayElementAtIndex(i - removedCount);

                if (elementProperty.objectReferenceValue == null)
                {
                    serializedProperty.DeleteArrayElementAtIndex(i - removedCount);
                    removedCount++;
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (deleteAllMissingScripts)
            {
                Undo.DestroyObjectImmediate(missingObject);
            }
        }

        missingScriptsObjects.Clear();
        Repaint();
    }
}
