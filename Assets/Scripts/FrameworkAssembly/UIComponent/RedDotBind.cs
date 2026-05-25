using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any UI GameObject to auto-display a red dot.
/// Binds to a RedDotNode by key. Updates visuals when count changes.
/// 
/// Inspector:
///   Node Key: "bag" or "bag/newItem"
///   Red Dot Transform: optional child Image (dot visual)
///   Count Text: optional child Text (number display for DisplayType.Number)
/// 
/// Usage: add this component on the same GameObject as a ButtonBind / ImageBind etc.
///   The prefab should have a child "RedDot" (Image) and optionally "RedDotCount" (Text).
/// </summary>
public class RedDotBind : MonoBehaviour
{
    [Header("Red Dot Config")]
    [Tooltip("Red dot node key, e.g. 'bag' or 'bag/newItem'")]
    [SerializeField] private string nodeKey;

    [Tooltip("Child Image used as the red dot. Auto-detects if left empty.")]
    [SerializeField] private Image dotImage;

    [Tooltip("Child Text used for the count number. Auto-detects if left empty.")]
    [SerializeField] private Text countText;

    [Header("Count Threshold")] [Tooltip("Max number to show. Count > this shows '{max}+'")]
    [SerializeField] private int maxDisplayCount = 99;

    private bool isBound = false;

    #region Unity Lifecycle

    private void Awake()
    {
        if (string.IsNullOrEmpty(nodeKey))
        {
            // Try to infer key from parent's BindKey
            var bindComp = GetComponent<IUIBind>();
            if (bindComp != null && !string.IsNullOrEmpty(bindComp.BindKey))
            {
                nodeKey = bindComp.BindKey;
                Log.d($"RedDotBind inferred key from IUIBind: {nodeKey}", "RedDotBind");
            }
        }

        AutoDetectReferences();
    }

    private void OnEnable()
    {
        BindIfReady();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        // Ensure cleanup even if OnDisable wasn't called properly
        Unbind();
    }

    #endregion

    #region Binding

    /// <summary>
    /// Bind to the red dot manager. Called automatically, but can be called
    /// externally if nodeKey is set after Awake.
    /// </summary>
    public void Bind()
    {
        if (isBound) return;
        if (string.IsNullOrEmpty(nodeKey))
        {
            Log.w($"RedDotBind on {name} has no nodeKey — cannot bind", "RedDotBind");
            return;
        }

        if (dotImage == null && countText == null)
        {
            Log.w($"RedDotBind on {name} has no visual components (dotImage+countText both null)", "RedDotBind");
        }

        RedDotManager.Instance.BindUI(nodeKey, OnRedDotCountChanged);
        isBound = true;
    }

    public void Unbind()
    {
        if (!isBound) return;
        if (!string.IsNullOrEmpty(nodeKey))
            RedDotManager.Instance.UnbindUI(nodeKey, OnRedDotCountChanged);
        isBound = false;
    }

    private void BindIfReady()
    {
        if (isBound || !enabled || !gameObject.activeInHierarchy) return;
        Bind();
    }

    #endregion

    #region Visual Update

    private void OnRedDotCountChanged(RedDotNode node)
    {
        int count = node.Count;

        // Dot (image) — visible when count > 0
        if (dotImage != null)
            dotImage.enabled = count > 0;

        // Count text — visible only for Number display type and count > 0
        if (countText != null)
        {
            bool showNumber = node.DisplayType == RedDotDisplayType.Number && count > 0;
            countText.enabled = showNumber;

            if (showNumber)
                countText.text = count > maxDisplayCount ? $"{maxDisplayCount}+" : count.ToString();
        }
    }

    #endregion

    #region Editor Helpers

    private void AutoDetectReferences()
    {
        if (dotImage == null)
        {
            var redDot = transform.Find("RedDot");
            if (redDot != null)
                dotImage = redDot.GetComponent<Image>();
        }

        if (countText == null)
        {
            var redDotCount = transform.Find("RedDotCount");
            if (redDotCount != null)
                countText = redDotCount.GetComponent<Text>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoDetectReferences();
    }
#endif

    #endregion
}
