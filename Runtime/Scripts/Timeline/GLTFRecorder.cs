#nullable enable
#define USE_ANIMATION_POINTER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GLTF.Schema;
using JetBrains.Annotations;
using Unity.Profiling;
using UnityEngine;
using UnityGLTF.Timeline.Samplers;
using UnityGLTF.Plugins;
using Object = UnityEngine.Object;

namespace UnityGLTF.Timeline
{
	public class GLTFRecorder
	{
		private readonly bool recordBlendShapes;
		private readonly bool recordAnimationPointer;
		
		public GLTFRecorder(
			Transform root,
			Func<Transform, bool> recordTransformInWorldSpace,
			bool recordBlendShapes = true,
			bool recordAnimationPointer = false,
			bool recordVisibility = false,
			IEnumerable<CustomComponentAnimationSampler>? additionalSamplers = null
		) {
			if (!root)
				throw new ArgumentNullException(nameof(root), "Please provide a root transform to record.");

			this.animationSamplers = AnimationSamplers.From(
				recordTransformInWorldSpace,
				recordVisibility,
				recordBlendShapes,
				recordAnimationPointer,
				additionalSamplers?.Select(customSampler => new CustomAnimationSamplerWrapper(customSampler))
			);
			this.root = root;
			this.recordBlendShapes = recordBlendShapes;
			this.recordAnimationPointer = recordAnimationPointer;
		}

		/// <summary>
		/// Optionally assign a list of transforms to be recorded, other transforms will be ignored
		/// </summary>
		internal ICollection<Transform>? recordingList = null;
		private bool allowRecordingTransform(Transform tr) => recordingList == null || recordingList.Contains(tr);

		private Transform root;
		private Dictionary<Transform, AnimationData> recordingAnimatedTransforms = new Dictionary<Transform, AnimationData>(64);

		private readonly AnimationSamplers animationSamplers;

		private double startTime;
		private double lastRecordedTime;
		private bool hasRecording;
		private bool isRecording;
		
		public bool HasRecording => hasRecording;
		public bool IsRecording => isRecording;

		
		/// <summary>
		/// Application Time when the most recent sample was recorded
		/// </summary>;
		public double LastRecordedTime => lastRecordedTime;
		
		public double RecordingStartTime => startTime;
		

		public string AnimationName = "Recording";

		public delegate void OnBeforeAddAnimationDataDelegate(PostAnimationData animationData);
		public delegate void OnPostExportDelegate(PostExportArgs animationData);
		
		/// <summary>
		/// Callback to modify the animation data before it is added to the animation.
		/// Is called once for each track after the recording has ended. This is a non destructive callback,
		/// so the original recorded data is not modified. Every time you call EndRecording to the save the gltf/glb,
		/// you can modify the data again. 
		/// </summary>
		public OnBeforeAddAnimationDataDelegate OnBeforeAddAnimationData;
		
		/// <summary>
		/// Callback to modify or add additional data to the gltf root after the recording has ended and animation
		/// data is added to the animation.
		/// </summary>
		public OnPostExportDelegate? OnPostExport;

		public class PostExportArgs
		{
			public Bounds AnimationTranslationBounds { get; private set; }
			public GLTFSceneExporter Exporter { get; private set; }
			public GLTFRoot GltfRoot { get; private set; }		
			
			internal PostExportArgs(Bounds animationTranslationBounds, GLTFSceneExporter exporter, GLTFRoot gltfRoot)
			{
				this.AnimationTranslationBounds = animationTranslationBounds;
				this.Exporter = exporter;
				this.GltfRoot = gltfRoot;
			}
		}

		public class PostAnimationData
		{
			public double[] Times;
			public object[] Values;
			
			public Object AnimatedObject { get; }
			public string PropertyName { get; }
			
			internal PostAnimationData(Object animatedObject, string propertyName, double[] times, object[] values) {
				this.AnimatedObject = animatedObject;
				this.PropertyName = propertyName;
				this.Times = times;
				this.Values = values;
			}
		}
		
		public void StartRecording(double time)
		{
			startTime = time;
			lastRecordedTime = 0;
			var transforms = root.GetComponentsInChildren<Transform>(true);
			recordingAnimatedTransforms.Clear();

			foreach (var tr in transforms)
			{
				if (!allowRecordingTransform(tr)) continue;
				recordingAnimatedTransforms.Add(tr, new AnimationData(animationSamplers, tr, lastRecordedTime));
			}

			isRecording = true;
			hasRecording = true;
		}

		public void UpdateRecording(double time)
		{
			if (!isRecording)
			{
				throw new InvalidOperationException($"{nameof(GLTFRecorder)} isn't recording, but {nameof(UpdateRecording)} was called. This is invalid.");
			}

			if (time <= lastRecordedTime)
			{
				Debug.LogWarning("Can't record backwards in time, please avoid this.");
				return;
			}

			var timeSinceStart = time - startTime;
			var trs = root.GetComponentsInChildren<Transform>(true);
			foreach (var tr in trs)
			{
				if (!allowRecordingTransform(tr)) continue;
				if (!recordingAnimatedTransforms.ContainsKey(tr))
				{
					// insert "empty" frame with scale=0,0,0 because this object might have just appeared in this frame
					var emptyData = new AnimationData(animationSamplers, tr, lastRecordedTime);
					recordingAnimatedTransforms.Add(tr, emptyData);
				}
				recordingAnimatedTransforms[tr].Update(timeSinceStart);
			}

			lastRecordedTime = time;
		}
		
		internal void endRecording(out Dictionary<Transform, AnimationData>? param)
		{
			param = null;
			if (!hasRecording) return;
			param = recordingAnimatedTransforms;
		}

		public bool EndRecording()
		{
			if (!isRecording) return false;
			if (!hasRecording) return false;
			isRecording = false;
			
			#if UNITY_EDITOR
			Debug.Log("Gltf Recording saved. "
				+ "Tracks: " + recordingAnimatedTransforms.Count + ", "
				+ "Total Keyframes: " + recordingAnimatedTransforms.Sum(x => x.Value.tracks.Sum(y => y.Values.Count())));
			#endif

			return true;
		}
		
		public GLTFSceneExporter CreateSceneExporterAfterRecording(GLTFSettings? settings = null, ILogger? logger = null) 
		{
			if (settings == null)
			{
				var adjustedSettings = Object.Instantiate(GLTFSettings.GetOrCreateSettings());
				adjustedSettings.ExportDisabledGameObjects = true;
				adjustedSettings.ExportAnimations = false;
				settings = adjustedSettings;
			}

			logger ??= new Logger(new StringBuilderLogHandler());
		
			// ensure correct animation pointer plugin settings are used
			if (!recordAnimationPointer)
				settings.ExportPlugins.RemoveAll(x => x is AnimationPointerExport);
			else if (!settings.ExportPlugins.Any(x => x is AnimationPointerExport))
				settings.ExportPlugins.Add(ScriptableObject.CreateInstance<AnimationPointerExport>());

			if (!recordBlendShapes)
				settings.BlendShapeExportProperties = GLTFSettings.BlendShapeExportPropertyFlags.None;
			
			var exportContext =
				new ExportContext(settings) { AfterSceneExport = PostExport, logger = logger };

			return new GLTFSceneExporter(new Transform[] { root }, exportContext);
		}

		public void EndRecordingAndSaveToFile(string filepath, string sceneName = "scene", GLTFSettings? settings = null)
		{
			if (!isRecording) return;
			if (!hasRecording) return;

			var dir = Path.GetDirectoryName(filepath);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
			using (var filestream = new FileStream(filepath, FileMode.Create, FileAccess.Write))
			{
				EndRecordingAndSaveToStream(filestream, sceneName, settings);
			}
		}

		public void EndRecordingAndSaveToStream(Stream stream, string sceneName = "scene", GLTFSettings? settings = null)
		{
			if (!EndRecording()) return;
			
			var logHandler = new StringBuilderLogHandler();
			var exporter = CreateSceneExporterAfterRecording(settings, new Logger(logHandler));
			exporter.SaveGLBToStream(stream, sceneName);

			logHandler.LogAndClear();
		}

		private void PostExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
		{
			// this would include animation from the original root
			// exporter.ExportAnimationFromNode(ref root);

			GLTFAnimation anim = new GLTFAnimation();
			anim.Name = AnimationName;

			CollectAndProcessAnimation(exporter, anim, true, out Bounds translationBounds);

			if (anim.Channels.Count > 0 && anim.Samplers.Count > 0)
				gltfRoot.Animations.Add(anim);
			
			OnPostExport?.Invoke( new PostExportArgs(translationBounds, exporter, gltfRoot));
		}

		private static void SafeDestroy(Object obj)
		{
			if (Application.isEditor)
				Object.DestroyImmediate(obj);
			else
				Object.Destroy(obj);
		}

		private static ProfilerMarker processAnimationMarker = new ProfilerMarker("Process Animation");
		private static ProfilerMarker simplifyKeyframesMarker = new ProfilerMarker("Simplify Keyframes");
		private static ProfilerMarker convertValuesMarker = new ProfilerMarker("Convert Values to Arrays");

		private void CollectAndProcessAnimation(GLTFSceneExporter gltfSceneExporter, GLTFAnimation anim, bool calculateTranslationBounds, out Bounds translationBounds)
		{
			var gotFirstValue = false;
			translationBounds = new Bounds();

			foreach (var kvp in recordingAnimatedTransforms)
			{
				processAnimationMarker.Begin();
				foreach (var track in kvp.Value.tracks)
				{
					if (track.Times.Length == 0) continue;

					var animatedObject = track.AnimatedObject;
					var trackName = track.PropertyName;
					var trackTimes = track.Times;
					var trackValues = track.Values;
					
					// AnimationData always has a visibility track, and if there is also a scale track, merge them
					if (track.PropertyName == "scale" && track is AnimationTrack<Transform, Vector3> scaleTrack) {
						// GLTF does not internally support a visibility state (animation).
						// So to simulate support for that, merge the visibility track with the scale track
						// forcing the scale to (0,0,0) whenever the model is invisible
						var (mergedTimes, mergedScales) = mergeVisibilityAndScaleTracks(kvp.Value.visibilityTrack, scaleTrack);
						if (mergedTimes == null || mergedScales == null)
							continue;
						trackTimes = mergedTimes;
						trackValues = mergedScales.Cast<object>().ToArray();
					}
					OnBeforeAddAnimationData?.Invoke(new PostAnimationData(animatedObject, trackName, trackTimes, trackValues));

					if (calculateTranslationBounds && track.PropertyName == "translation") {
						
						foreach (var t in trackValues) {
							var vec = (Vector3) t;
							if (!gotFirstValue)
							{
								translationBounds = new Bounds(vec, Vector3.zero);
								gotFirstValue = true;
							}
							else
							{
								translationBounds.Encapsulate(vec);
							}
						}
					}

					GLTFSceneExporter.RemoveUnneededKeyframes(ref trackTimes, ref trackValues);
					gltfSceneExporter.AddAnimationData(track.AnimatedObject, track.PropertyName, anim, trackTimes, trackValues);
				}
				processAnimationMarker.End();
			}
		}

		private static (double[]? times, Vector3[]? mergedScales) mergeVisibilityAndScaleTracks(
			VisibilityTrack? visibilityTrack,
			BaseAnimationTrack<Transform, Vector3>? scaleTrack
		) {
			if (visibilityTrack == null && scaleTrack == null) return (null, null);
			if (visibilityTrack == null) return (scaleTrack?.Times, scaleTrack?.values);
			var visTimes = visibilityTrack.Times;
			var visValues = visibilityTrack.values;
			
			if (scaleTrack == null) {
				var visScaleValues = visValues.Select(vis => vis ? Vector3.one : Vector3.zero).ToArray();
				return (visTimes, visScaleValues);
			}
			// both tracks are present, need to merge, but visibility always takes precedence
			
			var scaleTimes = scaleTrack.Times;
			var scaleValues = scaleTrack.values;

			var mergedTimes = new List<double>(visTimes.Length + scaleTimes.Length);
			var mergedScales = new List<Vector3>(visValues.Length + scaleValues.Length);

			var visIndex = 0;
			var scaleIndex = 0;
            
			var lastVisible = false;
			var lastScale = Vector3.zero;
			
			// process both
			while (visIndex < visTimes.Length && scaleIndex < scaleTimes.Length) {
				var visTime = visTimes[visIndex];
				var scaleTime = scaleTimes[scaleIndex];
				var visible = visValues[visIndex];
				var scale = scaleValues[scaleIndex];
				
				if (visTime.nearlyEqual(scaleTime)) {
					// both samples have the same timestamp
					// choose output value depending on visibility, but use scale value if visible
					record(visTime, visible ? scale : Vector3.zero);
					visIndex++;
					scaleIndex++;
					
				} else if (visTime < scaleTime) {
					// the next visibility change occurs sooner than the next scale change
					// record a change using visibility state and (if visible) previous scale value
					record(visTime, visible ? lastScale : Vector3.zero);
					visIndex++;
				}
				else if (scaleTime < visTime) {
					// the next scale change occurs sooner than the next visibility change
					// However, if the model is currently invisible, we simply dont care
                    if (lastVisible) 
	                    record(scaleTime, scale);
                    scaleIndex++;
				}

				lastScale = scale;
				lastVisible = visible;
			}
			
			// process remaining visibility changes - this will only enter if scale end was reached first
			while (visIndex < visTimes.Length) {
				var visTime = visTimes[visIndex];
				var visible = visValues[visIndex];
					
				// next vis change is sooner than next scale change
				// time: -> visTime
				// res: visible -> lastScale : 0
				record(visTime, visible ? lastScale : Vector3.zero);
				visIndex++;
				
				lastVisible = visible;
			}
			
			// process remaining scale changes - this will only enter if vis end was reached first -
			// if last visibility was invisible then there is no point in adding these
			while (lastVisible && scaleIndex < scaleTimes.Length) {
				var scaleTime = scaleTimes[scaleIndex];
				var scale = scaleValues[scaleIndex];
				record(scaleTime, scale);
			}
			
			return (mergedTimes.ToArray(), mergedScales.ToArray());

			void record(double time, Vector3 scale) {
				mergedTimes.Add(time);
				mergedScales.Add(scale);
			}
		}
		
		private class StringBuilderLogHandler : ILogHandler
		{
			private readonly StringBuilder sb = new StringBuilder();

			private string LogTypeToLog(LogType logType)
			{
#if UNITY_EDITOR
				// create strings with <color> tags
				switch (logType)
				{
					case LogType.Error:
						return "<color=red>[" + logType + "]</color>";
					case LogType.Assert:
						return "<color=red>[" + logType + "]</color>";
					case LogType.Warning:
						return "<color=yellow>[" + logType + "]</color>";
					case LogType.Log:
						return "[" + logType + "]";
					case LogType.Exception:
						return "<color=red>[" + logType + "]</color>";
					default:
						return "[" + logType + "]";
				}
#else
				return "[" + logType + "]";
#endif
			}

			public void LogFormat(LogType logType, Object context, string format, params object[] args) => sb.AppendLine($"{LogTypeToLog(logType)} {string.Format(format, args)} [Context: {context}]");
			public void LogException(Exception exception, Object context) => sb.AppendLine($"{LogTypeToLog(LogType.Exception)} {exception} [Context: {context}]");

			public void LogAndClear()
			{
				if(sb.Length > 0)
				{
					var str = sb.ToString();
#if UNITY_2019_1_OR_NEWER
					var logType = LogType.Log;
#if UNITY_EDITOR
					if (str.IndexOf("[Error]", StringComparison.Ordinal) > -1 ||
					    str.IndexOf("[Exception]", StringComparison.Ordinal) > -1 ||
					    str.IndexOf("[Assert]", StringComparison.Ordinal) > -1)
						logType = LogType.Error;
					else if (str.IndexOf("[Warning]", StringComparison.Ordinal) > -1)
						logType = LogType.Warning;
#endif
					Debug.LogFormat(logType, LogOption.NoStacktrace, null, "Export Messages:" + "\n{0}", sb.ToString());
#else
					Debug.Log(string.Format("Export Messages:" + "\n{0}", str));
#endif
				}
				sb.Clear();
			}
		}
	}
	
	internal static class DoubleExtensions {
		internal static bool nearlyEqual(this double a, double b, double epsilon = double.Epsilon) => Math.Abs(a - b) < epsilon;
	}
}
