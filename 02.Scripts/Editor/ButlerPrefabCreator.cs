using UnityEngine;
using UnityEditor;
using LodgingSimulator.AI;

namespace LodgingSimulator.Editor
{
    /// <summary>
    /// 집사 AI 프리팹을 자동으로 생성하는 에디터 스크립트
    /// </summary>
    public class ButlerPrefabCreator : EditorWindow
    {
        [MenuItem("LodgingSimulator/집사 AI/프리팹 생성")]
        public static void CreateButlerPrefab()
        {
            // 집사 AI 게임오브젝트 생성
            GameObject butlerObject = new GameObject("ButlerAI");
            
            // 필요한 컴포넌트들 추가
            ButlerAI butlerAI = butlerObject.AddComponent<ButlerAI>();
            NavMeshAgent navAgent = butlerObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
            Animator animator = butlerObject.AddComponent<Animator>();
            
            // NavMeshAgent 기본 설정
            navAgent.speed = 3.5f;
            navAgent.angularSpeed = 180f;
            navAgent.acceleration = 8f;
            navAgent.stoppingDistance = 0.5f;
            navAgent.radius = 0.5f;
            navAgent.height = 2f;
            
            // 기본 설정 파일 참조 설정
            ButlerSettingsSO settings = AssetDatabase.LoadAssetAtPath<ButlerSettingsSO>("Assets/02.Scripts/SO/ButlerSettings.asset");
            if (settings != null)
            {
                butlerAI.settings = settings;
            }
            
            // 프리팹으로 저장
            string prefabPath = "Assets/02.Scripts/AI/ButlerAI.prefab";
            
            // 기존 프리팹이 있다면 덮어쓰기
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }
            
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(butlerObject, prefabPath);
            
            // 임시 게임오브젝트 제거
            DestroyImmediate(butlerObject);
            
            // 에셋 데이터베이스 새로고침
            AssetDatabase.Refresh();
            
            Debug.Log($"집사 AI 프리팹이 생성되었습니다: {prefabPath}");
            
            // 생성된 프리팹을 선택
            Selection.activeObject = prefab;
        }
        
        [MenuItem("LodgingSimulator/집사 AI/설정 파일 생성")]
        public static void CreateButlerSettings()
        {
            // ButlerSettings ScriptableObject 생성
            ButlerSettingsSO settings = ScriptableObject.CreateInstance<ButlerSettingsSO>();
            
            // 기본값 설정
            settings.butlerPurchaseCost = 1000;
            settings.maxButlerCount = 10;
            settings.butlerMoveSpeed = 3.5f;
            settings.cleaningDuration = 5.0f;
            settings.rotationSpeed = 180f;
            settings.maxAssignmentDistance = 50f;
            settings.counterPosition = Vector3.zero;
            settings.moveAnimationParam = "IsMoving";
            settings.cleaningAnimationParam = "IsCleaning";
            settings.idleAnimationParam = "IsIdle";
            
            // 에셋으로 저장
            string assetPath = "Assets/02.Scripts/SO/ButlerSettings.asset";
            
            // 기존 에셋이 있다면 덮어쓰기
            if (AssetDatabase.LoadAssetAtPath<ButlerSettingsSO>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"집사 AI 설정 파일이 생성되었습니다: {assetPath}");
            
            // 생성된 설정 파일을 선택
            Selection.activeObject = settings;
        }
        
        [MenuItem("LodgingSimulator/집사 AI/시스템 설정 가이드")]
        public static void ShowSetupGuide()
        {
            string message = @"집사 AI 시스템 설정 가이드:

1. 설정 파일 생성:
   - 메뉴에서 'LodgingSimulator/집사 AI/설정 파일 생성' 실행
   - 생성된 ButlerSettings.asset 파일을 확인하고 필요시 값 수정

2. 프리팹 생성:
   - 메뉴에서 'LodgingSimulator/집사 AI/프리팹 생성' 실행
   - 생성된 ButlerAI.prefab 파일을 확인

3. 씬에 배치:
   - ButlerManager 컴포넌트를 가진 게임오브젝트 생성
   - ButlerSettings 참조 설정
   - ButlerAI 프리팹 참조 설정
   - 스폰 위치 설정

4. UI 설정:
   - ButlerPurchaseUI 컴포넌트를 UI에 추가
   - 필요한 UI 요소들 연결

5. 방 시스템 연동:
   - RoomManager의 enableButlerSystem을 true로 설정
   - autoAddCleaningTask를 true로 설정

6. NavMesh 설정:
   - 씬에 NavMesh가 구워져 있는지 확인
   - 집사 AI가 이동할 수 있는 경로가 있는지 확인

7. 애니메이션 설정:
   - 집사 AI 프리팹에 Animator Controller 연결
   - IsMoving, IsCleaning, IsIdle 파라미터 설정

모든 설정이 완료되면 집사 AI 시스템이 자동으로 작동합니다!";

            EditorUtility.DisplayDialog("집사 AI 시스템 설정 가이드", message, "확인");
        }
    }
}
