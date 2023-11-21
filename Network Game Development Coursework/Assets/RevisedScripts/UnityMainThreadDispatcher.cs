using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*Cite: The following class is extracted from chatGPT for the purpose of running codes in the main thread so that UI elements can be displayed normally*/
public class UnityMainThreadDispatcher : MonoBehaviour 
{
    private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Update()
    {
        lock (ExecutionQueue)
        {
            while (ExecutionQueue.Count > 0)
            {
                ExecutionQueue.Dequeue().Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock (ExecutionQueue)
        {
            ExecutionQueue.Enqueue(action);
        }
    }

    // Optional: A method for convenience
    public static void RunOnMainThread(Action action)
    {
        if (Instance != null)
        {
            Instance.Enqueue(action);
        }
    }
}
