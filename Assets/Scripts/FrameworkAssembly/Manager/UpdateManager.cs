using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for handling Update, FixedUpdate, and LateUpdate calls
/// Provides better performance control and easier debugging than scattered Update calls
/// </summary>
public class UpdateManager : MonoBehaviour
{
    #region Singleton
    private static UpdateManager instance;
    public static UpdateManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("UpdateManager");
                instance = go.AddComponent<UpdateManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Member Variables
    private class UpdateEntry
    {
        public IUpdatable updatable;
        public UpdateFrequency frequency;
        public float timer;
        public float interval;

        public UpdateEntry(IUpdatable updatable, UpdateFrequency frequency)
        {
            this.updatable = updatable;
            this.frequency = frequency;
            this.timer = 0f;
            this.interval = GetIntervalFromFrequency(frequency);
        }

        private float GetIntervalFromFrequency(UpdateFrequency freq)
        {
            switch (freq)
            {
                case UpdateFrequency.Low: return 0.1f;
                case UpdateFrequency.Medium: return 0.05f;
                case UpdateFrequency.High: return 0.033f;
                default: return 0f; // EveryFrame
            }
        }
    }

    private List<UpdateEntry> updateList = new List<UpdateEntry>();
    private List<UpdateEntry> fixedUpdateList = new List<UpdateEntry>();
    private List<UpdateEntry> lateUpdateList = new List<UpdateEntry>();

    private List<UpdateEntry> updatePendingAdd = new List<UpdateEntry>();
    private List<UpdateEntry> updatePendingRemove = new List<UpdateEntry>();
    private List<UpdateEntry> fixedUpdatePendingAdd = new List<UpdateEntry>();
    private List<UpdateEntry> fixedUpdatePendingRemove = new List<UpdateEntry>();
    private List<UpdateEntry> lateUpdatePendingAdd = new List<UpdateEntry>();
    private List<UpdateEntry> lateUpdatePendingRemove = new List<UpdateEntry>();

    private bool isPaused = false;
    private bool isUpdatingUpdate      = false;
    private bool isUpdatingFixed       = false;
    private bool isUpdatingLate        = false;
    #endregion

    #region Public Methods
    /// <summary>
    /// Register an updatable object
    /// </summary>
    public void Register(IUpdatable updatable, UpdateType type = UpdateType.Update, UpdateFrequency frequency = UpdateFrequency.EveryFrame)
    {
        if (updatable == null)
        {
            Log.w("Cannot register null updatable", "UpdateManager");
            return;
        }

        UpdateEntry entry = new UpdateEntry(updatable, frequency);

        switch (type)
        {
            case UpdateType.Update:
                if (!ContainsUpdatable(updateList, updatable) && !ContainsUpdatable(updatePendingAdd, updatable))
                {
                    if (isUpdatingUpdate)
                        updatePendingAdd.Add(entry);
                    else
                        updateList.Add(entry);
                }
                break;

            case UpdateType.FixedUpdate:
                if (!ContainsUpdatable(fixedUpdateList, updatable) && !ContainsUpdatable(fixedUpdatePendingAdd, updatable))
                {
                    if (isUpdatingFixed)
                        fixedUpdatePendingAdd.Add(entry);
                    else
                        fixedUpdateList.Add(entry);
                }
                break;

            case UpdateType.LateUpdate:
                if (!ContainsUpdatable(lateUpdateList, updatable) && !ContainsUpdatable(lateUpdatePendingAdd, updatable))
                {
                    if (isUpdatingLate)
                        lateUpdatePendingAdd.Add(entry);
                    else
                        lateUpdateList.Add(entry);
                }
                break;
        }

    }

    /// <summary>
    /// Unregister an updatable object
    /// </summary>
    public void Unregister(IUpdatable updatable, UpdateType type = UpdateType.Update)
    {
        if (updatable == null) return;

        switch (type)
        {
            case UpdateType.Update:
                RemoveFromList(updateList, updatePendingRemove, updatable);
                break;

            case UpdateType.FixedUpdate:
                RemoveFromList(fixedUpdateList, fixedUpdatePendingRemove, updatable);
                break;

            case UpdateType.LateUpdate:
                RemoveFromList(lateUpdateList, lateUpdatePendingRemove, updatable);
                break;
        }

    }

    /// <summary>
    /// Pause all updates
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        Log.d("UpdateManager paused", "UpdateManager");
    }

    /// <summary>
    /// Resume all updates
    /// </summary>
    public void Resume()
    {
        isPaused = false;
        Log.d("UpdateManager resumed", "UpdateManager");
    }

    /// <summary>
    /// Get current update statistics
    /// </summary>
    public string GetStatistics()
    {
        return $"Update: {updateList.Count}, FixedUpdate: {fixedUpdateList.Count}, LateUpdate: {lateUpdateList.Count}, Paused: {isPaused}";
    }
    #endregion

    #region Unity Lifecycle
    void Update()
    {
        if (isPaused) return;

        isUpdatingUpdate = true;
        float deltaTime = Time.deltaTime;

        for (int i = updateList.Count - 1; i >= 0; i--)
        {
            UpdateEntry entry = updateList[i];
            if (entry.updatable == null) { updateList.RemoveAt(i); continue; }
            if (!entry.updatable.IsUpdateActive) continue;

            if (entry.frequency == UpdateFrequency.EveryFrame)
            {
                entry.updatable.OnUpdate(deltaTime);
            }
            else
            {
                entry.timer += deltaTime;
                if (entry.timer >= entry.interval)
                {
                    entry.updatable.OnUpdate(entry.timer);
                    entry.timer = 0f;
                }
            }
        }

        isUpdatingUpdate = false;
        ProcessPendingOperations(updateList, updatePendingAdd, updatePendingRemove);
    }

    void FixedUpdate()
    {
        if (isPaused) return;

        isUpdatingFixed = true;
        float fixedDeltaTime = Time.fixedDeltaTime;

        for (int i = fixedUpdateList.Count - 1; i >= 0; i--)
        {
            UpdateEntry entry = fixedUpdateList[i];
            if (entry.updatable == null) { fixedUpdateList.RemoveAt(i); continue; }
            if (!entry.updatable.IsUpdateActive) continue;

            if (entry.frequency == UpdateFrequency.EveryFrame)
            {
                entry.updatable.OnFixedUpdate(fixedDeltaTime);
            }
            else
            {
                entry.timer += fixedDeltaTime;
                if (entry.timer >= entry.interval)
                {
                    entry.updatable.OnFixedUpdate(entry.timer);
                    entry.timer = 0f;
                }
            }
        }

        isUpdatingFixed = false;
        ProcessPendingOperations(fixedUpdateList, fixedUpdatePendingAdd, fixedUpdatePendingRemove);
    }

    void LateUpdate()
    {
        if (isPaused) return;

        isUpdatingLate = true;
        float deltaTime = Time.deltaTime;

        for (int i = lateUpdateList.Count - 1; i >= 0; i--)
        {
            UpdateEntry entry = lateUpdateList[i];
            if (entry.updatable == null) { lateUpdateList.RemoveAt(i); continue; }
            if (!entry.updatable.IsUpdateActive) continue;

            if (entry.frequency == UpdateFrequency.EveryFrame)
            {
                entry.updatable.OnLateUpdate(deltaTime);
            }
            else
            {
                entry.timer += deltaTime;
                if (entry.timer >= entry.interval)
                {
                    entry.updatable.OnLateUpdate(entry.timer);
                    entry.timer = 0f;
                }
            }
        }

        isUpdatingLate = false;
        ProcessPendingOperations(lateUpdateList, lateUpdatePendingAdd, lateUpdatePendingRemove);
    }
    #endregion

    #region Helper Methods
    private bool ContainsUpdatable(List<UpdateEntry> list, IUpdatable updatable)
    {
        foreach (var entry in list)
        {
            if (entry.updatable == updatable)
                return true;
        }
        return false;
    }

    private void RemoveFromList(List<UpdateEntry> list, List<UpdateEntry> pendingRemove, IUpdatable updatable)
    {
        bool busy = isUpdatingUpdate || isUpdatingFixed || isUpdatingLate;
        if (busy)
        {
            // Mark for removal
            foreach (var entry in list)
            {
                if (entry.updatable == updatable)
                {
                    pendingRemove.Add(entry);
                    break;
                }
            }
        }
        else
        {
            // Remove immediately
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].updatable == updatable)
                {
                    list.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void ProcessPendingOperations(List<UpdateEntry> list, List<UpdateEntry> pendingAdd, List<UpdateEntry> pendingRemove)
    {
        // Add pending entries
        if (pendingAdd.Count > 0)
        {
            list.AddRange(pendingAdd);
            pendingAdd.Clear();
        }

        // Remove pending entries
        if (pendingRemove.Count > 0)
        {
            foreach (var entry in pendingRemove)
            {
                list.Remove(entry);
            }
            pendingRemove.Clear();
        }
    }
    #endregion
}
