using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;

namespace UnityAutoRendering
{
    [CustomEditor(typeof(RenderJobSettings))]
    public class RenderJobSettingsEditor : Editor
    {
        static readonly (string name, Type settingsType)[] k_RecorderTypes =
        {
            ("Animation",      typeof(AnimationRecorderSettings)),
            ("Movie",          typeof(MovieRecorderSettings)),
            ("Image Sequence", typeof(ImageRecorderSettings)),
            ("Audio",          typeof(AudioRecorderSettings)),
        };

        static readonly string[] k_RecorderNames = k_RecorderTypes.Select(r => r.name).ToArray();

        Editor m_RecorderEditor;
        int m_SelectedIndex;
        bool m_NeedsRebuild;

        void OnEnable()
        {
            m_RecorderEditor = null;

            var settings = (RenderJobSettings)target;
            m_SelectedIndex = 0;
            if (settings.recorderSettings != null)
            {
                var idx = Array.FindIndex(k_RecorderTypes, r => r.settingsType == settings.recorderSettings.GetType());
                if (idx >= 0)
                    m_SelectedIndex = idx;
            }

            RebuildRecorderEditor();
        }

        void OnDisable()
        {
            DestroyRecorderEditor();
        }

        public override void OnInspectorGUI()
        {
            if (m_NeedsRebuild)
            {
                m_NeedsRebuild = false;
                RebuildRecorderEditor();
            }

            serializedObject.Update();
            var settings = (RenderJobSettings)target;

            // --- Clip Timing ---
            DrawClipTiming(settings);
            EditorGUILayout.Space();

            // --- Recorder type selector ---
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                var newIndex = EditorGUILayout.Popup("Selected recorder:", m_SelectedIndex, k_RecorderNames);
                if (newIndex != m_SelectedIndex)
                {
                    m_SelectedIndex = newIndex;
                    ScheduleRecorderChange(settings);
                }

                // --- Recorder settings inspector ---
                if (m_RecorderEditor != null)
                {
                    EditorGUILayout.Separator();
                    m_RecorderEditor.OnInspectorGUI();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void ScheduleRecorderChange(RenderJobSettings settings)
        {
            DestroyRecorderEditor();

            if (settings.recorderSettings != null)
            {
                var old = settings.recorderSettings;
                settings.recorderSettings = null;
                EditorUtility.SetDirty(settings);
                Undo.DestroyObjectImmediate(old);
            }

            var settingsType = k_RecorderTypes[m_SelectedIndex].settingsType;
            var recorderSettings = (RecorderSettings)ObjectFactory.CreateInstance(settingsType);
            recorderSettings.name = k_RecorderTypes[m_SelectedIndex].name;
            recorderSettings.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;

            AssetDatabase.AddObjectToAsset(recorderSettings, settings);

            settings.recorderSettings = recorderSettings;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            m_NeedsRebuild = true;
            Repaint();
        }

        void RebuildRecorderEditor()
        {
            DestroyRecorderEditor();
            var settings = (RenderJobSettings)target;
            if (settings.recorderSettings != null)
            {
                m_RecorderEditor = CreateEditor(settings.recorderSettings);
            }
        }

        void DestroyRecorderEditor()
        {
            if (m_RecorderEditor != null)
            {
                DestroyImmediate(m_RecorderEditor);
                m_RecorderEditor = null;
            }
        }

        void DrawClipTiming(RenderJobSettings settings)
        {
            EditorGUILayout.LabelField("Clip Timing", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var fps = settings.recorderSettings != null ? settings.recorderSettings.FrameRate : 60f;
            if (fps <= 0f) fps = 60f;

            DrawSecondsFramesRow("Start",
                serializedObject.FindProperty("startSeconds"),
                serializedObject.FindProperty("startFrames"),
                fps);

            DrawSecondsFramesRow("End",
                serializedObject.FindProperty("endSeconds"),
                serializedObject.FindProperty("endFrames"),
                fps);

            // Duration (read-only)
            var durS = settings.endSeconds - settings.startSeconds;
            var durF = settings.endFrames - settings.startFrames;
            var rect = EditorGUILayout.GetControlRect();
            var labelW = EditorGUIUtility.labelWidth;
            var fieldW = (rect.width - labelW) * 0.5f;
            var sLabelW = 14f;
            var fLabelW = 14f;

            var r = new Rect(rect.x, rect.y, labelW, rect.height);
            EditorGUI.LabelField(r, "Duration");

            r = new Rect(rect.x + labelW, rect.y, sLabelW, rect.height);
            EditorGUI.LabelField(r, "s");
            r = new Rect(rect.x + labelW + sLabelW, rect.y, fieldW - sLabelW - 4f, rect.height);
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.FloatField(r, durS);

            r = new Rect(rect.x + labelW + fieldW, rect.y, fLabelW, rect.height);
            EditorGUI.LabelField(r, "f");
            r = new Rect(rect.x + labelW + fieldW + fLabelW, rect.y, fieldW - fLabelW, rect.height);
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.IntField(r, durF);

            EditorGUI.indentLevel--;
        }

        void DrawSecondsFramesRow(string label, SerializedProperty secProp, SerializedProperty frameProp, float fps)
        {
            var rect = EditorGUILayout.GetControlRect();
            var labelW = EditorGUIUtility.labelWidth;
            var fieldW = (rect.width - labelW) * 0.5f;
            var sLabelW = 14f;
            var fLabelW = 14f;

            var r = new Rect(rect.x, rect.y, labelW, rect.height);
            EditorGUI.LabelField(r, label);

            r = new Rect(rect.x + labelW, rect.y, sLabelW, rect.height);
            EditorGUI.LabelField(r, "s");
            r = new Rect(rect.x + labelW + sLabelW, rect.y, fieldW - sLabelW - 4f, rect.height);
            EditorGUI.BeginChangeCheck();
            var newSec = EditorGUI.FloatField(r, secProp.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                secProp.floatValue = Mathf.Max(0f, newSec);
                frameProp.intValue = Mathf.RoundToInt(secProp.floatValue * fps);
            }

            r = new Rect(rect.x + labelW + fieldW, rect.y, fLabelW, rect.height);
            EditorGUI.LabelField(r, "f");
            r = new Rect(rect.x + labelW + fieldW + fLabelW, rect.y, fieldW - fLabelW, rect.height);
            EditorGUI.BeginChangeCheck();
            var newFrame = EditorGUI.IntField(r, frameProp.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                frameProp.intValue = Mathf.Max(0, newFrame);
                secProp.floatValue = frameProp.intValue / fps;
            }
        }
    }
}
