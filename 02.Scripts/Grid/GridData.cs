using System;
using UnityEngine;
using System.Collections.Generic;
using ZLinq;

/// <summary>
/// 그리드 데이터를 저장하는 클래스
/// </summary>
public class GridData
{
    // 설치된 오브젝트 데이터가 담긴 딕셔너리
    //private Dictionary<Vector3Int, PlacementData> placedObjects = new();

    // 설치된 오브젝트 데이터가 담긴 딕셔너리
    public Dictionary<Vector3Int, List<PlacementData>> placedObjects = new();

    #region 딕셔너리에 설치된 오브젝트 포함

    /// <summary>
    /// 설치된 오브젝트의 데이터를 딕셔너리에 추가한다.
    /// </summary>
    /// <param name="gridPosition"></param>
    /// <param name="objectSize"></param>
    /// <param name="ID"></param>
    /// <param name="placedObjectIndex"></param>
    /// <param name="rotation"></param>
    /// <param name="grid"></param>
    /// <exception cref="Exception"></exception>
     public void AddObjectAt(Vector3Int gridPosition, Vector2Int objectSize, int ID, int placedObjectIndex, int kindOfIndex, Quaternion rotation, Grid grid, bool isWall)
    {
        List<Vector3Int> positions = CalculatePosition(gridPosition, objectSize, rotation, grid);
        // PlacementData 생성 시 rotation 전달
        PlacementData data = new PlacementData(positions, ID, placedObjectIndex, kindOfIndex, rotation);

        foreach (var pos in positions)
        {
            if (!placedObjects.ContainsKey(pos))
            {
                placedObjects[pos] = new List<PlacementData>();
            }   
            
            var sameTypeExistingObjects = placedObjects[pos]
                .AsValueEnumerable().Where(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall == isWall);
            //.Where(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall == isWall);
            var existingObjects = placedObjects[pos];

            if (isWall) // 벽 추가 시, 같은 각도의 벽 중복 방지 (이전 답변 내용 유지)
            {
                if (sameTypeExistingObjects.Any(obj => Mathf.Approximately(Quaternion.Angle(obj.Rotation, rotation), 0)))
                {
                    // 필요 시 예외 발생
                    // throw new Exception($"이 셀({pos})에는 이미 같은 각도의 벽이 존재합니다.");
                    // 여기서는 예외를 발생시키지 않고 Add를 진행 (CanPlaceObjectAt에서 이미 걸렀어야 함)
                }
            }
            else // 가구 추가 시, 같은 위치에 가구 중복 방지
            {
                if (sameTypeExistingObjects.Any()) // 같은 타입(가구)이 이미 있다면
                {
                    // 필요 시 예외 발생
                    // throw new Exception($"이 셀({pos})은 이미 가구로 점유되어 있습니다.");
                    // 여기서는 예외를 발생시키지 않고 Add를 진행 (CanPlaceObjectAt에서 이미 걸렀어야 함)
                }
            }
            
            // --- 벽/가구 추가 시 예외 처리 수정 ---
            /*if (isWall) // 벽을 추가하는 경우
            {
                // 같은 위치에 같은 회전값을 가진 벽이 이미 있는지 확인
                if (existingObjects.Any(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall && Mathf.Approximately(Quaternion.Angle(obj.Rotation, rotation), 0)))
                {
                    // 설치 불가 사운드 추가
                    // 주석 처리: 같은 회전값의 벽이 이미 있다면 예외 발생 (필요 시 활성화)
                    // throw new Exception($"이 셀({pos})에는 이미 같은 각도의 벽이 존재합니다.");
                    // 현재 요구사항: 각도만 다르면 추가 가능하므로, 같은 각도 벽 검사 후 예외 발생 로직 제거
                }
                // 해당 위치에 가구가 이미 있으면 예외 발생 (벽과 가구 겹침 방지)
                if (existingObjects.Any(obj => !PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                {
                    throw new Exception($"이 셀({pos})은 가구로 점유되어 있어 벽을 설치할 수 없습니다.");
                }
            }
            else // 가구를 추가하는 경우
            {
                // 같은 위치에 가구가 이미 있으면 예외 발생
                if (existingObjects.Any(obj => !PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                {
                    throw new Exception($"이 셀({pos})은 이미 가구로 점유되어 있습니다.");
                }
                // 같은 위치에 벽이 있으면 예외 발생 (가구와 벽 겹침 방지)
                if (existingObjects.Any(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                {
                    throw new Exception($"이 셀({pos})은 벽으로 점유되어 있어 가구를 설치할 수 없습니다.");
                }
            }*/

            placedObjects[pos].Add(data); // 예외가 발생하지 않으면 데이터 추가
        }
    }
    #endregion

    #region 그리드 좌표 계산

    /// <summary>
    /// 설치할 오브젝트의 상하좌우 길이를 실시간으로 변경하여 딕셔너리와 비교할 수 있도록 계산한다.
    /// </summary>
    /// <param name="gridPosition"></param>
    /// <param name="objectSize"></param>
    /// <param name="rotation"></param>
    /// <param name="grid"></param>
    /// <returns></returns>
    public List<Vector3Int> CalculatePosition(Vector3Int gridPosition, Vector2Int objectSize, Quaternion rotation, Grid grid)
    {
        List<Vector3Int> positions = new List<Vector3Int>();

        // 회전 각도를 0, 90, 180, 270도로 반올림
        float angle = Mathf.Round(rotation.eulerAngles.y / 90) * 90;

        // 회전된 오브젝트 크기 계산
        Vector2Int size = objectSize;
        if (Mathf.Approximately(angle % 180, 90)) // 90도 또는 270도일 때
        {
            size = new Vector2Int(objectSize.y, objectSize.x); // 너비와 높이 교환
        }

        // 회전 각도에 따른 기준점 오프셋 계산
        Vector2Int offset;
        if (Mathf.Approximately(angle, 0))
        {
            offset = new Vector2Int(0, 0); // 0도: 기준점 그대로
        }
        else if (Mathf.Approximately(angle, 90))
        {
            offset = new Vector2Int(0, size.y - 1); // 90도: Z축으로 조정
        }
        else if (Mathf.Approximately(angle, 180))
        {
            offset = new Vector2Int(size.x - 1, size.y - 1); // 180도: X와 Z 모두 조정
        }
        else // 270도
        {
            offset = new Vector2Int(size.x - 1, 0); // 270도: X축으로 조정
        }

        Vector3Int startPos = new Vector3Int(gridPosition.x - offset.x, gridPosition.y, gridPosition.z - offset.y);

        // 점유 셀 위치 계산
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++) // 루프 변수를 z로 변경하여 명확하게 합니다.
            {
                Vector3Int cellPos = new Vector3Int(startPos.x + x, startPos.y, startPos.z + z);
                positions.Add(cellPos);
            }
        }


        return positions;
    }
   
    #endregion

    #region 점유 확인 여부 플래그

    /// <summary>
    /// 설치하려는 위치에 오브젝트가 존재 유무를 판단한다.
    /// </summary>
    /// <param name="gridPosition"></param>
    /// <param name="objectSize"></param>
    /// <param name="rotation"></param>
    /// <param name="grid"></param>
    /// <returns></returns>
    public bool CanPlaceObjectAt(Vector3Int gridPosition, Vector2Int objectSize, Quaternion rotation, Grid grid, bool isWall)
    {
        List<Vector3Int> positions = CalculatePosition(gridPosition, objectSize, rotation, grid);

        foreach (var pos in positions)
        {
            if (placedObjects.ContainsKey(pos) && placedObjects[pos].Count > 0)
            {
                // 이 GridData가 관리하는 타입과 같은 타입의 오브젝트만 고려
                var sameTypeExistingObjects = placedObjects[pos]
                    .AsValueEnumerable().Where(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall == isWall);
                //.Where(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall == isWall);

                if (isWall) // 벽 배치 가능 여부 확인 (같은 각도 벽 충돌 체크)
                {
                    if (sameTypeExistingObjects.Any(obj => Mathf.Approximately(Quaternion.Angle(obj.Rotation, rotation), 0)))
                    {
                        return false; // 같은 각도의 벽이 이미 있음
                    }
                }
                else // 가구 배치 가능 여부 확인 (다른 가구 충돌 체크)
                {
                    if (sameTypeExistingObjects.Any()) // 이미 같은 타입(가구) 오브젝트가 있다면
                    {
                        return false; // 배치 불가
                    }
                }
                // --- 다른 타입과의 충돌 검사는 여기서 제거 ---
            }
        }

        // 모든 위치 검사를 통과하면 배치 가능
        return true;
    }
    #endregion

    #region 오브젝트 제거

    /// <summary>
    /// 설치된 오브젝트 제거
    /// </summary>
    /// <param name="placedObjectIndex">설치된 오브젝트의 인덱스</param>
    /// <returns></returns>
    public bool RemoveObjectByIndex(int placedObjectIndex)
    {
        bool removed = false;
        List<Vector3Int> keysToRemove = new List<Vector3Int>();

        foreach (var kvp in placedObjects)
        {
            Vector3Int pos = kvp.Key;
            List<PlacementData> objectsAtPos = kvp.Value;

            // 해당 인덱스의 PlacementData 찾기
            for (int i = objectsAtPos.Count - 1; i >= 0; i--)
            {
                if (objectsAtPos[i].PlacedObjectIndex == placedObjectIndex)
                {
                    objectsAtPos.RemoveAt(i);
                    removed = true;
                }
            }

            // 해당 위치에 더 이상 오브젝트가 없으면 키 제거 준비
            if (objectsAtPos.Count == 0)
            {
                keysToRemove.Add(pos);
            }
        }

        // 빈 키 제거
        foreach (var key in keysToRemove)
        {
            placedObjects.Remove(key);
        }

        return removed;
    }

    #endregion
}