using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZLinq; // Assuming ZLinq provides AsValueEnumerable and Max for your ObjectData list

#if UNITY_EDITOR
[CustomEditor(typeof(ObjectsDatabaseSO))]
public class ObjectDatabaseEditor : Editor
{
    private ObjectsDatabaseSO database;
    private Vector2 scrollPos;
    private int selectedTab = 0;
    private int newObjectKindIndex = 0; // This variable is not used but kept for consistency
    private string newObjectName = "";
    private Vector2Int newObjectSize = Vector2Int.one;
    private GameObject newObjectPrefab = null;
    private int newObjectBuildPrice = 0;
    private int newObjectBasePrice;
    private bool newObjectIsWall = false;
    private int newObjectReputation;
    private Dictionary<ObjectData, bool> foldoutStates = new Dictionary<ObjectData, bool>();

    private void OnEnable()
    {
        database = (ObjectsDatabaseSO)target;
        database.InitializeDictionary();
        // Initialize foldout states for existing objects
        foreach (var obj in database.objectsData)
        {
            if (!foldoutStates.ContainsKey(obj)) foldoutStates[obj] = false;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Tab selection
        string[] tabNames = { "바닥", "가구", "벽", "장식품" };
        selectedTab = GUILayout.SelectionGrid(selectedTab, tabNames, tabNames.Length);

        // Add new object section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("새 데이터 추가", EditorStyles.boldLabel); // Changed label for clarity
        newObjectName = EditorGUILayout.TextField("이름", newObjectName);
        EditorGUILayout.LabelField("종류", tabNames[selectedTab]); // Displays the currently selected tab's name
        newObjectSize = EditorGUILayout.Vector2IntField("크기", newObjectSize);
        newObjectPrefab = (GameObject)EditorGUILayout.ObjectField("프리팹", newObjectPrefab, typeof(GameObject), false);
        newObjectBuildPrice = EditorGUILayout.IntField("건축 가격", newObjectBuildPrice);
        newObjectBasePrice = EditorGUILayout.IntField("기본 가격", newObjectBasePrice);
        newObjectIsWall = EditorGUILayout.Toggle("벽 or Not", newObjectIsWall);
        newObjectReputation = EditorGUILayout.IntField("명성도", newObjectReputation);

        if (GUILayout.Button("새 데이터 추가")) // Changed button text for clarity
        {
            // Create a new ID. If no objects exist, start from 0. Otherwise, find the max ID and add 1.
            int newID = database.objectsData.Count > 0 ? database.objectsData.AsValueEnumerable().Max(o => o.ID) + 1 : 0;

            var newObject = new ObjectData
            {
                Name = newObjectName,
                ID = newID,
                kindIndex = selectedTab,
                Size = newObjectSize,
                Prefab = newObjectPrefab,
                BuildPrice = newObjectBuildPrice,
                BasePrice = newObjectBasePrice,
                IsWall = newObjectIsWall,
                ReputationValue = newObjectReputation
            };
            database.objectsData.Add(newObject);
            foldoutStates[newObject] = false; // Set foldout state for the new object
            database.InitializeDictionary(); // Re-initialize the dictionary after adding
            EditorUtility.SetDirty(database); // Mark the database as dirty to save changes
            AssetDatabase.SaveAssets(); // Immediately save assets

            // Clear input fields after adding
            newObjectName = "";
            newObjectSize = Vector2Int.one;
            newObjectPrefab = null;
            newObjectBuildPrice = 0;
            newObjectBasePrice = 0;
            newObjectIsWall = false;
            newObjectReputation = 1;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("기존 데이터 수정/삭제", EditorStyles.boldLabel); // Added label for clarity
        // Scroll view for objects
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Filter and sort objects by kindIndex and ID
        var filteredObjects = database.objectsData.AsValueEnumerable()
            .Where(obj => obj.kindIndex == selectedTab)
            .OrderBy(obj => obj.ID)
            .ToList();

        // Check if there are any objects to display for the current tab
        if (filteredObjects.Count == 0)
        {
            EditorGUILayout.HelpBox($"'{tabNames[selectedTab]}' 탭에 해당하는 데이터가 없습니다.", MessageType.Info);
        }

        foreach (var obj in filteredObjects)
        {
            // Ensure the foldout state exists for the current object
            if (!foldoutStates.ContainsKey(obj))
            {
                foldoutStates[obj] = false;
            }

            // Display foldout for each object
            bool currentFoldoutState = foldoutStates[obj];
            currentFoldoutState = EditorGUILayout.Foldout(currentFoldoutState, $"ID: {obj.ID} - {obj.Name}", true, EditorStyles.foldoutHeader);
            foldoutStates[obj] = currentFoldoutState; // Update the foldout state

            if (currentFoldoutState)
            {
                EditorGUILayout.BeginVertical("box");
                // Display and allow modification of object properties
                // We don't allow changing ID or kindIndex here as they are foundational
                EditorGUILayout.LabelField("ID", obj.ID.ToString()); // Display ID but don't allow editing
                obj.Name = EditorGUILayout.TextField("이름", obj.Name); // Allow name editing
                obj.Size = EditorGUILayout.Vector2IntField("크기", obj.Size);
                obj.Prefab = (GameObject)EditorGUILayout.ObjectField("프리팹", obj.Prefab, typeof(GameObject), false);
                obj.BuildPrice = EditorGUILayout.IntField("건축 가격", obj.BuildPrice);
                obj.BasePrice = EditorGUILayout.IntField("기본 가격", obj.BasePrice);
                obj.IsWall = EditorGUILayout.Toggle("벽 or Not", obj.IsWall);
                obj.ReputationValue = EditorGUILayout.IntField("명성도", obj.ReputationValue);

                // --- Modification Button ---
                if (GUILayout.Button("수정 (Modify)"))
                {
                    EditorUtility.SetDirty(database); // Mark the database as dirty
                    AssetDatabase.SaveAssets(); // Save the modified assets
                    Debug.Log($"Object ID: {obj.ID}, Name: {obj.Name} has been modified and saved.");
                }

                // Delete button
                if (GUILayout.Button("삭제 (Delete)"))
                {
                    database.objectsData.Remove(obj);
                    foldoutStates.Remove(obj); // Remove its foldout state
                    database.InitializeDictionary(); // Re-initialize the dictionary after removal
                    EditorUtility.SetDirty(database); // Mark dirty
                    AssetDatabase.SaveAssets(); // Save changes
                    break; // Break to avoid issues with collection modification during iteration
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();

        // This block is generally good for catching any unhandled modifications,
        // but explicit SaveAssets on button press is more immediate for user feedback.
        if (serializedObject.hasModifiedProperties)
        {
            EditorUtility.SetDirty(database);
            // AssetDatabase.SaveAssets(); // We are now calling SaveAssets explicitly on button press
        }

        serializedObject.ApplyModifiedProperties(); // Apply any changes made to serialized properties
    }
}
#endif

/*using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZLinq;

#if UNITY_EDITOR
[CustomEditor(typeof(ObjectsDatabaseSO))]
public class ObjectDatabaseEditor : Editor
{
    private ObjectsDatabaseSO database;
    private Vector2 scrollPos;
    private int selectedTab = 0;
    private int newObjectKindIndex = 0;
    private string newObjectName = "";
    private Vector2Int newObjectSize = Vector2Int.one;
    private GameObject newObjectPrefab = null;
    private int newObjectBuildPrice = 0;
    private int newObjectBasePrice;
    private bool newObjectIsWall = false;
    private int newObjectReputation;
    private Dictionary<ObjectData, bool> foldoutStates = new Dictionary<ObjectData, bool>();

    private void OnEnable()
    {
        database = (ObjectsDatabaseSO)target;
        database.InitializeDictionary();
        foreach (var obj in database.objectsData)
        {
            if (!foldoutStates.ContainsKey(obj)) foldoutStates[obj] = false;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Tab selection
        string[] tabNames = {"바닥", "가구", "벽", "장식품"};
        selectedTab = GUILayout.SelectionGrid(selectedTab, tabNames, tabNames.Length);

        // Add new object section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("데이터", EditorStyles.boldLabel);
        newObjectName = EditorGUILayout.TextField("이름", newObjectName);
        EditorGUILayout.LabelField("종류", tabNames[selectedTab]);
        newObjectSize = EditorGUILayout.Vector2IntField("크기", newObjectSize);
        newObjectPrefab = (GameObject)EditorGUILayout.ObjectField("프리팹", newObjectPrefab, typeof(GameObject), false);
        newObjectBuildPrice = EditorGUILayout.IntField("건축 가격", newObjectBuildPrice);
        newObjectBasePrice = EditorGUILayout.IntField("기본 가격", newObjectBasePrice);
        newObjectIsWall = EditorGUILayout.Toggle("벽 or Not", newObjectIsWall);
        newObjectReputation = EditorGUILayout.IntField("명성도", newObjectReputation);

        if (GUILayout.Button("데이터 추가"))
        {
            var newObject = new ObjectData
            {
                Name = newObjectName,
                ID = database.objectsData.Count > 0 ? database.objectsData.AsValueEnumerable().Max(o => o.ID) + 1 : 0,
                kindIndex = selectedTab,
                Size = newObjectSize,
                Prefab = newObjectPrefab,
                BuildPrice = newObjectBuildPrice,
                BasePrice = newObjectBasePrice,
                IsWall = newObjectIsWall,
                ReputationValue = newObjectReputation
            };
            database.objectsData.Add(newObject);
            foldoutStates[newObject] = false;
            database.InitializeDictionary();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            newObjectName = "";
            newObjectSize = Vector2Int.one;
            newObjectPrefab = null;
            newObjectBuildPrice = 0;
            newObjectBasePrice = 0;
            newObjectIsWall = false;
            newObjectReputation = 1;
        }

        // Scroll view for objects
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Filter and sort objects by kindIndex and ID
        var filteredObjects = database.objectsData.AsValueEnumerable()
            .Where(obj => obj.kindIndex == selectedTab)
            .OrderBy(obj => obj.ID)
            .ToList();

        foreach (var obj in filteredObjects)
        {
            foldoutStates.TryGetValue(obj, out bool isFolded);
            isFolded = EditorGUILayout.Foldout(isFolded, $"ID: {obj.ID} - {obj.Name}", true, EditorStyles.foldoutHeader);
            foldoutStates[obj] = isFolded;

            if (isFolded)
            {
                EditorGUILayout.BeginVertical("box");
                obj.Size = EditorGUILayout.Vector2IntField("크기", obj.Size);
                obj.Prefab = (GameObject)EditorGUILayout.ObjectField("프리팹", obj.Prefab, typeof(GameObject), false);
                obj.BuildPrice = EditorGUILayout.IntField("건축 가격", obj.BuildPrice);
                obj.BasePrice = EditorGUILayout.IntField("기본 가격", obj.BasePrice);
                obj.IsWall = EditorGUILayout.Toggle("벽 or Not", obj.IsWall);
                obj.ReputationValue = EditorGUILayout.IntField("명성도", obj.ReputationValue);

                if (GUILayout.Button("Delete"))
                {
                    database.objectsData.Remove(obj);
                    foldoutStates.Remove(obj);
                    EditorUtility.SetDirty(database);
                    break;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();

        if (serializedObject.hasModifiedProperties)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets(); // 데이터 수정 시 저장
        }
        
        serializedObject.ApplyModifiedProperties();

    }
}
#endif*/