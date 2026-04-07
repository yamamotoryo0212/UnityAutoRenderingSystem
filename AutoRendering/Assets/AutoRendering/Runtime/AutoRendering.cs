using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Timeline;
#endif

namespace UnityAutoRendering
{
	public class AutoRendering : MonoBehaviour
	{
		[SerializeField]
		private PlayableDirector _timeline = null;
		[SerializeField]
		private RenderJobSettings _renderJobSettings = null;

		private bool _isPlaying = false;
#if UNITY_EDITOR
		private TimelineAsset _timelineAsset = null;
#endif

		private void Awake()
		{
#if UNITY_EDITOR
			if (_timeline == null || _renderJobSettings == null)
				return;

			var timelineAsset = _timeline.playableAsset as TimelineAsset;
			if (timelineAsset == null)
				return;

			if (_renderJobSettings.recorderSettings == null)
				return;

			_timelineAsset = timelineAsset;
			SetupRecorderClip(timelineAsset);

			_timeline.stopped += OnTimelineStopped;
			_timeline.RebuildGraph();
			_timeline.Play();
			_isPlaying = true;
#endif
		}

		private void OnTimelineStopped(PlayableDirector director)
		{
#if UNITY_EDITOR
			if (!_isPlaying) return;
			_isPlaying = false;

			CleanupRecorderTracks();
			EditorApplication.Exit(0);
#endif
		}

#if UNITY_EDITOR
		private void SetupRecorderClip(TimelineAsset timelineAsset)
		{
			foreach (var track in timelineAsset.GetOutputTracks())
			{
				if (track is RecorderTrack)
					timelineAsset.DeleteTrack(track);
			}

			var recorderTrack = timelineAsset.CreateTrack<RecorderTrack>(null, "Auto Render");

			var timelineClip = recorderTrack.CreateClip<RecorderClip>();
			timelineClip.start = _renderJobSettings.startSeconds;
			timelineClip.duration = _renderJobSettings.endSeconds - _renderJobSettings.startSeconds;
			timelineClip.displayName = "Auto Render Clip";

			var recorderClip = timelineClip.asset as RecorderClip;
			if (recorderClip != null)
			{
				recorderClip.settings = _renderJobSettings.recorderSettings;
				recorderClip.settings.Take = GetCommandLineTake();
				recorderClip.settings.FrameRate = (float)timelineAsset.editorSettings.frameRate;
				recorderClip.settings.FrameRatePlayback = FrameRatePlayback.Constant;
				recorderClip.settings.CapFrameRate = true;
			}
		}

		private static int GetCommandLineTake()
		{
			var args = Environment.GetCommandLineArgs();
			for (var i = 0; i < args.Length - 1; i++)
			{
				if (args[i] == "-take" && int.TryParse(args[i + 1], out var take))
					return take;
			}
			return 1;
		}

		private void CleanupRecorderTracks()
		{
			if (_timelineAsset == null) return;

			var tracksToDelete = new System.Collections.Generic.List<TrackAsset>();
			foreach (var track in _timelineAsset.GetOutputTracks())
			{
				if (track is RecorderTrack)
					tracksToDelete.Add(track);
			}

			foreach (var track in tracksToDelete)
				_timelineAsset.DeleteTrack(track);

			AssetDatabase.SaveAssets();
		}
#endif
	}
}
