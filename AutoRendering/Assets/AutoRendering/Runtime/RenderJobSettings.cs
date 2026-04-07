using UnityEditor.Recorder;
using UnityEngine;

namespace UnityAutoRendering
{
    [CreateAssetMenu(fileName = "RenderJobSettings", menuName = "Auto Rendering/Render Job Settings")]
    public class RenderJobSettings : ScriptableObject
    {
        [Header("Clip Timing")]
        public float startSeconds;
        public int startFrames;
        public float endSeconds = 5f;
        public int endFrames = 300;

        [Header("Recorder Clip")]
        [SerializeField]
        public RecorderSettings recorderSettings;
    }
}
