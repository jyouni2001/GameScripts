using UnityEngine;

public enum SoundType
{
    Build,
    Click,
    UI,
    Unlock,
    Failed
}
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }
    [SerializeField] private AudioClip[] soundList;
    [SerializeField] private AudioSource audioSource;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("이미 SFXManager 인스턴스가 존재합니다. 이 인스턴스를 파괴합니다.");
            Destroy(gameObject);
        }
    }

    public static void PlaySound(SoundType sound, float volume = 1)
    {
        if(!Instance.audioSource.isPlaying)
        Instance.audioSource.PlayOneShot(Instance.soundList[(int)sound], volume);
    }
}
