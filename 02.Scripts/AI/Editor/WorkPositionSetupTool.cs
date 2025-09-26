using UnityEngine;
using UnityEditor;

namespace JY.AI.Editor
{
    /// <summary>
    /// AI 작업 위치 설정 도구 (에디터 전용)
    /// 씬에서 오브젝트에 작업 위치 태그를 쉽게 추가할 수 있는 에디터 툴
    /// </summary>
    public class WorkPositionSetupTool : EditorWindow
    {
        private string[] jobTypes = {
            "Reception", "Kitchen", "Cleaning", "Security", "Maintenance", "Service"
        };
        
        private int selectedJobType = 0;
        private GameObject selectedObject;
        
        [MenuItem("Tools/AI 작업 위치 설정")]
        public static void ShowWindow()
        {
            GetWindow<WorkPositionSetupTool>("AI 작업 위치 설정");
        }
        
        void OnGUI()
        {
            GUILayout.Label("AI 작업 위치 설정 도구", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "1. Hierarchy에서 작업 위치로 사용할 오브젝트를 선택하세요.\n" +
                "2. 직업 타입을 선택하세요.\n" +
                "3. '작업 위치 태그 추가' 버튼을 클릭하세요.",
                MessageType.Info
            );
            
            GUILayout.Space(10);
            
            // 선택된 오브젝트 표시
            selectedObject = Selection.activeGameObject;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("선택된 오브젝트:", selectedObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();
            
            GUILayout.Space(10);
            
            // 직업 타입 선택
            selectedJobType = EditorGUILayout.Popup("직업 타입:", selectedJobType, jobTypes);
            
            GUILayout.Space(10);
            
            // 작업 위치 태그 추가 버튼
            EditorGUI.BeginDisabledGroup(selectedObject == null);
            if (GUILayout.Button("작업 위치 태그 추가", GUILayout.Height(30)))
            {
                AddWorkPositionTag();
            }
            EditorGUI.EndDisabledGroup();
            
            GUILayout.Space(20);
            
            // 일괄 생성 도구
            GUILayout.Label("일괄 작업 위치 생성", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "각 직업 타입별로 기본 작업 위치 오브젝트를 생성합니다.",
                MessageType.Info
            );
            
            if (GUILayout.Button("모든 작업 위치 생성", GUILayout.Height(30)))
            {
                CreateAllWorkPositions();
            }
            
            GUILayout.Space(20);
            
            // 기존 작업 위치 스캔
            GUILayout.Label("기존 작업 위치 스캔", EditorStyles.boldLabel);
            if (GUILayout.Button("씬에서 작업 위치 스캔", GUILayout.Height(25)))
            {
                ScanExistingWorkPositions();
            }
        }
        
        /// <summary>
        /// 선택된 오브젝트에 작업 위치 태그 추가
        /// </summary>
        private void AddWorkPositionTag()
        {
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("오류", "오브젝트를 선택해주세요.", "확인");
                return;
            }
            
            string newTag = $"WorkPosition_{jobTypes[selectedJobType]}";
            
            // 태그가 존재하지 않으면 생성
            if (!TagExists(newTag))
            {
                AddTag(newTag);
            }
            
            // 오브젝트에 태그 적용
            selectedObject.tag = newTag;
            
            // 작업 위치 마커 기능 완전 제거
            // AddWorkPositionMarker(selectedObject);
            
            EditorUtility.SetDirty(selectedObject);
            
            Debug.Log($"작업 위치 태그 추가됨: {selectedObject.name} -> {newTag}");
            EditorUtility.DisplayDialog("완료", $"'{selectedObject.name}'에 '{newTag}' 태그가 추가되었습니다.", "확인");
        }
        
        /// <summary>
        /// 모든 직업 타입의 기본 작업 위치 생성
        /// </summary>
        private void CreateAllWorkPositions()
        {
            GameObject parent = new GameObject("Work Positions");
            parent.transform.position = Vector3.zero;
            
            foreach (string jobType in jobTypes)
            {
                CreateWorkPositionObject(jobType, parent.transform);
            }
            
            Debug.Log("모든 작업 위치가 생성되었습니다.");
            EditorUtility.DisplayDialog("완료", "모든 작업 위치가 생성되었습니다.", "확인");
        }
        
        /// <summary>
        /// 특정 직업 타입의 작업 위치 오브젝트 생성
        /// </summary>
        private void CreateWorkPositionObject(string jobType, Transform parent)
        {
            GameObject workPos = new GameObject($"WorkPosition_{jobType}");
            workPos.transform.SetParent(parent);
            
            // 기본 위치 설정
            Vector3 basePosition = GetDefaultPositionForJobType(jobType);
            workPos.transform.position = basePosition;
            
            // 태그 설정
            string tagName = $"WorkPosition_{jobType}";
            if (!TagExists(tagName))
            {
                AddTag(tagName);
            }
            workPos.tag = tagName;
            
            // 시각적 마커 기능 완전 제거
            // AddWorkPositionMarker(workPos);
            
            Debug.Log($"작업 위치 생성됨: {workPos.name} at {basePosition}");
        }
        
        /// <summary>
        /// 직업 타입별 기본 위치 반환
        /// </summary>
        private Vector3 GetDefaultPositionForJobType(string jobType)
        {
            switch (jobType.ToLower())
            {
                case "reception":
                    return new Vector3(0, 0, 5);
                case "kitchen":
                    return new Vector3(10, 0, 0);
                case "cleaning":
                    return new Vector3(-5, 0, 0);
                case "security":
                    return new Vector3(0, 0, -10);
                case "maintenance":
                    return new Vector3(-10, 0, 0);
                case "service":
                    return new Vector3(5, 0, 5);
                default:
                    return Vector3.zero;
            }
        }
        
        
        
        /// <summary>
        /// 씬에서 기존 작업 위치 스캔
        /// </summary>
        private void ScanExistingWorkPositions()
        {
            int foundCount = 0;
            
            foreach (string jobType in jobTypes)
            {
                string tagName = $"WorkPosition_{jobType}";
                GameObject[] positions = GameObject.FindGameObjectsWithTag(tagName);
                foundCount += positions.Length;
                
                if (positions.Length > 0)
                {
                    Debug.Log($"{jobType}: {positions.Length}개 작업 위치 발견");
                }
            }
            
            if (foundCount > 0)
            {
                Debug.Log($"총 {foundCount}개의 작업 위치가 발견되었습니다.");
                EditorUtility.DisplayDialog("스캔 완료", $"총 {foundCount}개의 작업 위치가 발견되었습니다.\n자세한 내용은 Console을 확인하세요.", "확인");
            }
            else
            {
                Debug.Log("작업 위치가 발견되지 않았습니다.");
                EditorUtility.DisplayDialog("스캔 완료", "작업 위치가 발견되지 않았습니다.", "확인");
            }
        }
        
        /// <summary>
        /// 태그 존재 여부 확인
        /// </summary>
        private bool TagExists(string tag)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(tag))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// 새 태그 추가
        /// </summary>
        private void AddTag(string tag)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            
            // 빈 슬롯 찾기
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                if (t.stringValue == "")
                {
                    t.stringValue = tag;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
            
            // 빈 슬롯이 없으면 새로 추가
            tagsProp.InsertArrayElementAtIndex(0);
            SerializedProperty newTagProp = tagsProp.GetArrayElementAtIndex(0);
            newTagProp.stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
        
        [MenuItem("Tools/Kitchen/Check KitchenDetector Status")]
        private static void CheckKitchenDetectorStatus()
        {
            Debug.Log("=== KitchenDetector 상태 체크 ===");
            
            if (Application.isPlaying)
            {
                if (JY.KitchenDetector.Instance != null)
                {
                    Debug.Log("✅ Instance 존재");
                    Debug.Log($"GameObject 이름: {JY.KitchenDetector.Instance.name}");
                    Debug.Log($"활성 상태: {JY.KitchenDetector.Instance.gameObject.activeInHierarchy}");
                }
                else
                {
                    Debug.LogError("❌ Instance가 null");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ Play Mode에서만 정확한 상태를 확인할 수 있습니다.");
            }
            
            // 씬에서 모든 KitchenDetector 찾기
            var detectors = UnityEngine.Object.FindObjectsByType<JY.KitchenDetector>(FindObjectsSortMode.None);
            Debug.Log($"씬에 있는 KitchenDetector 개수: {detectors.Length}");
            
            for (int i = 0; i < detectors.Length; i++)
            {
                Debug.Log($"KitchenDetector {i + 1}: {detectors[i].name} (활성: {detectors[i].gameObject.activeInHierarchy})");
            }
        }

        [MenuItem("Tools/Kitchen/Add Kitchen Required Tags")]
        private static void AddKitchenRequiredTags()
        {
            string[] kitchenTags = {
                "WorkPosition_Gas",     // 가스레인지 작업 위치
                "Kitchen",              // 주방 영역
                "KitchenCounter",       // 주방 카운터 (주문 받는 곳)
                "Player",               // 플레이어 (트리거용)
                "Customer"              // 고객 (트리거용)
            };
            
            var tool = new WorkPositionSetupTool();
            int addedCount = 0;
            
            foreach (string tag in kitchenTags)
            {
                if (!tool.TagExists(tag))
                {
                    tool.AddTag(tag);
                    addedCount++;
                    Debug.Log($"태그 추가: {tag}");
                }
                else
                {
                    Debug.Log($"태그 이미 존재: {tag}");
                }
            }
            
            if (addedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"🏷️ 주방 관련 태그 {addedCount}개 추가 완료!");
                EditorUtility.DisplayDialog("태그 추가 완료", $"{addedCount}개의 새로운 태그가 추가되었습니다!", "확인");
            }
            else
            {
                Debug.Log("모든 주방 관련 태그가 이미 존재합니다.");
                EditorUtility.DisplayDialog("태그 확인", "모든 주방 관련 태그가 이미 존재합니다.", "확인");
            }
        }

        [MenuItem("Tools/Kitchen/Test Kitchen Detection")]
        private static void TestKitchenDetection()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("This feature only works in Play Mode!");
                return;
            }

            Debug.Log("=== KitchenDetector 테스트 ===");
            
            if (JY.KitchenDetector.Instance != null)
            {
                Debug.Log("✅ KitchenDetector.Instance 존재함");
                
                // 테스트용 오브젝트 생성
                GameObject testObject = new GameObject("TestKitchenObject");
                testObject.tag = "KitchenCounter"; // 주방 카운터 태그
                testObject.transform.position = Vector3.zero;

                Debug.Log($"테스트 오브젝트 생성: {testObject.name}, 태그: {testObject.tag}");

                // KitchenDetector에게 알림
                JY.KitchenDetector.Instance.OnFurnitureePlaced(testObject, Vector3.zero);
                Debug.Log("KitchenDetector에게 테스트 배치 이벤트 전송됨");

                // 테스트 오브젝트 정리
                UnityEngine.Object.DestroyImmediate(testObject);
            }
            else
            {
                Debug.LogError("❌ KitchenDetector.Instance를 찾을 수 없습니다!");
                
                // 씬에서 KitchenDetector 찾기 시도
                var kitchenDetector = UnityEngine.Object.FindFirstObjectByType<JY.KitchenDetector>();
                if (kitchenDetector != null)
                {
                    Debug.Log($"🔍 씬에서 KitchenDetector 발견: {kitchenDetector.name}");
                    Debug.Log("하지만 Instance가 null인 상태입니다. Awake가 호출되지 않았을 수 있습니다.");
                }
                else
                {
                    Debug.LogError("🚫 씬에 KitchenDetector가 전혀 없습니다! KitchenDetector를 씬에 추가하세요.");
                }
            }
        }
    }
}
