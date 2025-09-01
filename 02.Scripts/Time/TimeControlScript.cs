using UnityEngine;

public class TimeControlScript : MonoBehaviour
{    
    public void SetTimeScale(float timeScale)
    {
        if (Mathf.Approximately(Time.timeScale, timeScale)) return;
        Time.timeScale = timeScale;
    }
}
