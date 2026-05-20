using UnityEditor;
using UnityEngine;

/// <summary>
/// Simple text input dialog for Unity Editor.
/// </summary>
public class EditorInputDialog : EditorWindow
{
    private string _message;
    private string _input;
    private string _result;

    public static string Show(string title, string message, string defaultInput = "")
    {
        var window = GetWindow<EditorInputDialog>(true, title, true);
        window._message = message;
        window._input = defaultInput;
        window._result = null;
        window.minSize = new Vector2(300, 120);
        window.maxSize = new Vector2(400, 140);
        window.ShowModal();
        return window._result;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(5);
        _input = EditorGUILayout.TextField("Node Name:", _input);
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("OK", GUILayout.Width(80)))
        {
            _result = _input;
            Close();
        }
        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            _result = null;
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void OnLostFocus()
    {
        // Don't close on lost focus — user might click elsewhere
    }
}
