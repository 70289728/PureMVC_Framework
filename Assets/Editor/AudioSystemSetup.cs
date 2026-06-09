using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using System.IO;

/// <summary>
/// Editor utility for audio system setup.
/// Use menu item "Tools/Audio/Setup Audio System" to create defaults.
/// </summary>
public class AudioSystemSetup : EditorWindow
{
    private const string MIXER_OUTPUT_PATH = "Assets/Resources/Audio/AudioMixer.mixer";
    private const string MIXER_DIR = "Assets/Resources/Audio";

    [MenuItem("Tools/Audio/Setup Audio System")]
    public static void ShowWindow()
    {
        GetWindow<AudioSystemSetup>("Audio System Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Audio System Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "The AudioMixer asset enables independent volume control per channel " +
            "(Master/BGM/SFX/Voice).\n\n" +
            "If no AudioMixer is assigned, AudioManager falls back to direct AudioSource volume control.",
            MessageType.Info);

        // Check current mixer status
        var existingMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_OUTPUT_PATH);
        var sceneMixer = FindObjectOfType<AudioManager>()?.GetComponent<AudioSource>();

        if (existingMixer != null)
        {
            EditorGUILayout.HelpBox("AudioMixer found at: " + MIXER_OUTPUT_PATH, MessageType.None);

            if (GUILayout.Button("Select AudioMixer", GUILayout.Height(30)))
            {
                Selection.activeObject = existingMixer;
                EditorGUIUtility.PingObject(existingMixer);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "No AudioMixer found. Click below to create one, or create manually:\n\n" +
                "1. Right-click in Project → Create → Audio Mixer\n" +
                "2. Name it 'AudioMixer'\n" +
                "3. Place it at: " + MIXER_DIR + "\n" +
                "4. Add groups: Master, BGM, SFX, Voice (child of Master)\n" +
                "5. Expose Volume params: MasterVolume, BGMVolume, SFXVolume, VoiceVolume",
                MessageType.Warning);

            if (GUILayout.Button("Open Mixer Directory", GUILayout.Height(30)))
            {
                if (!Directory.Exists(MIXER_DIR))
                    Directory.CreateDirectory(MIXER_DIR);
                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(MIXER_DIR);
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Audio Config Table", EditorStyles.boldLabel);

        var configJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameConfig/audioConfig.json");
        if (configJson != null)
        {
            EditorGUILayout.HelpBox($"Config loaded: {configJson.name}.json ({configJson.text.Length} bytes)", MessageType.None);
            if (GUILayout.Button("Select Config JSON"))
                Selection.activeObject = configJson;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Config JSON not found. Run Design/ExportTools/export_all.bat to generate.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Audio Resources Directory", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Place audio files (.wav/.ogg/.mp3) in:\n" +
            "  - Assets/ProjectAssets/Base/Audio/       (ships with app)\n" +
            "  - Assets/ProjectAssets/HotUpdate/UIAssets/Audio/ (hot-updatable)\n\n" +
            "For Resources fallback: Assets/Resources/Audio/BGM/, /SFX/, /Voice/",
            MessageType.None);
    }

    void OnEnable()
    {
        // Create Resources/Audio directory if it doesn't exist
        if (!Directory.Exists(MIXER_DIR))
        {
            Directory.CreateDirectory(MIXER_DIR);
            AssetDatabase.Refresh();
        }
    }
}
