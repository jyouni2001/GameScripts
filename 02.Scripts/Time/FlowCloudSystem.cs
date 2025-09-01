using UnityEngine;
using UnityEngine.Rendering;

public class FlowCloudSystem : MonoBehaviour
{
    public Volume volume;
    private VolumetricClouds volumetricClouds;
    public float offsetPerSeconds;

    private void Start()
    {
        // Volume에서 VolumetricClouds 컴포넌트 가져오기
        if (volume != null)
        {
            volume.profile.TryGet(out volumetricClouds);
            if (volumetricClouds == null)
            {
                Debug.LogWarning("VolumetricClouds 컴포넌트를 찾을 수 없습니다. Volume 프로필을 확인해주세요.");
            }
        }
        else
        {
            Debug.LogWarning("Volume이 할당되지 않았습니다. 인스펙터에서 Volume을 설정해주세요.");
        }
    }

    private void Update()
    {
        if (volumetricClouds != null)
        {
            // 현재 Shape Offset 값 가져오기
            Vector3 currentOffset = volumetricClouds.shapeOffset.value;

            // 1초에 0.1씩 증가하도록 계산 (Time.deltaTime으로 프레임 독립적으로 동작)
            float increaseAmount = offsetPerSeconds * Time.deltaTime; // 1초에 0.1 증가
            currentOffset += new Vector3(increaseAmount, increaseAmount, increaseAmount);

            // 새로운 값 적용
            volumetricClouds.shapeOffset.value = currentOffset;
        }        
    }
    
    
}
