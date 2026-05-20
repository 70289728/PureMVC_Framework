using System;
using System.Collections.Generic;

/// <summary>
/// Routes incoming packets to registered callbacks by message ID.
/// All dispatch calls are expected to happen on the Unity main thread.
/// </summary>
public class MessageDispatcher
{
    private readonly Dictionary<int, List<Action<byte[]>>> handlers =
        new Dictionary<int, List<Action<byte[]>>>();

    /// <summary>
    /// Register a callback for the given message ID.
    /// </summary>
    public void Register(int msgId, Action<byte[]> handler)
    {
        if (!handlers.TryGetValue(msgId, out var list))
        {
            list = new List<Action<byte[]>>();
            handlers[msgId] = list;
        }
        if (!list.Contains(handler))
            list.Add(handler);
    }

    /// <summary>
    /// Unregister a previously registered callback.
    /// </summary>
    public void Unregister(int msgId, Action<byte[]> handler)
    {
        if (handlers.TryGetValue(msgId, out var list))
            list.Remove(handler);
    }

    /// <summary>
    /// Dispatch a packet to all registered handlers for the given message ID.
    /// Logs a warning if no handler is found.
    /// </summary>
    public void Dispatch(int msgId, byte[] body)
    {
        if (!handlers.TryGetValue(msgId, out var list) || list.Count == 0)
        {
            Log.w($"No handler registered for msgId={msgId}", "MessageDispatcher");
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            try
            {
                list[i]?.Invoke(body);
            }
            catch (Exception e)
            {
                Log.e($"Handler exception for msgId={msgId}: {e}", "MessageDispatcher");
            }
        }
    }

    /// <summary>
    /// Remove all registered handlers.
    /// </summary>
    public void Clear()
    {
        handlers.Clear();
    }
}
