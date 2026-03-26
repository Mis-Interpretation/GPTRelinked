using UnityEngine;
using LLMUnity;
using System;

public class LLMBrain : MonoBehaviour
{
    [SerializeField] private LLMAgent agent;

    public event Action<string> EventChatCompletion;
    private string llmResponse;

    // execute after Awake(), where LLMManager being initialized
    void Start()
    {
        agent.llm = LLMManager.LLMInstance;
        agent.Warmup();
    }

    public void HandleInput(string input)
    {
        agent.Chat(input, HandleChatCallback, HandleChatCompleted);
    }
    
    public void AddPrompt(string addedPrompt)
    {
        agent.systemPrompt += addedPrompt;
    }

    private void HandleChatCallback(string msg)
    {
        llmResponse = msg;
    }

    private void HandleChatCompleted()
    {
        Debug.Log($"LLM: {llmResponse}");
        EventChatCompletion?.Invoke(llmResponse);
    }
}
