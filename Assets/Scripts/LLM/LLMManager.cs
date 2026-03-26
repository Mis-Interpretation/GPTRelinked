using UnityEngine;
using LLMUnity;

public class LLMManager : MonoBehaviour
{
    [SerializeField] private LLM llm;

    private static LLMManager _instance;
    public static LLM LLMInstance;
    
    void Awake()
    {
        LLMInstance = llm;
        // LLM is a DontDestroyOnLoad resources
        if (!_instance)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (llm)
        {
            Destroy(llm.gameObject);
        }
        
    }
}
