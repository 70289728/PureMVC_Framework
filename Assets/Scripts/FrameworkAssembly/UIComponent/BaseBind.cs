using System;
using UnityEngine;

public interface IUIBind
{
    Component BoundComponent { get; }
    string BindKey { get; }
    Type TargetComponentType { get; }
}

public abstract class BaseBind<T> : MonoBehaviour, IUIBind where T : Component
{
    [SerializeField] protected string bindKey;
    [SerializeField] protected T component;

    public string BindKey => bindKey;

    public T Component
    {
        get
        {
            if (component == null)
            {
                component = GetComponent<T>();
            }
            return component;
        }
    }

    Component IUIBind.BoundComponent => Component;

    Type IUIBind.TargetComponentType => typeof(T);

    protected virtual void Awake()
    {
        CacheComponent();
    }

    protected virtual void OnValidate()
    {
        CacheComponent();
    }

    private void CacheComponent()
    {
        if (component == null)
        {
            component = GetComponent<T>();
        }

        if (string.IsNullOrEmpty(bindKey))
        {
            bindKey = $"{typeof(T).Name}_{gameObject.name}";
        }
    }
}
