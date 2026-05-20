/// <summary>
/// Updatable interface for objects that need to be updated regularly
/// </summary>
public interface IUpdatable
{
    /// <summary>
    /// Called every frame
    /// </summary>
    void OnUpdate(float deltaTime);

    /// <summary>
    /// Called every fixed frame (for physics)
    /// </summary>
    void OnFixedUpdate(float fixedDeltaTime);

    /// <summary>
    /// Called after all Update calls
    /// </summary>
    void OnLateUpdate(float deltaTime);

    /// <summary>
    /// Whether this object is active and should be updated
    /// </summary>
    bool IsUpdateActive { get; }
}
