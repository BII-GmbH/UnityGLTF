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
using UnityEngine.Profiling;
using UnityGLTF.Timeline.Samplers;
using UnityGLTF.Plugins;
using Object = UnityEngine.Object;

namespace UnityGLTF.Timeline
{
	public class GLTFRecorder
	{
		public sealed class Factory
		{
			private readonly List<AnimationSampler> customAnimationSamplers = new();

			public Factory AddCustomAnimationSampler<TComponent, TData>(CustomComponentAnimationSampler<TComponent, TData> sampler)
			where TComponent : Component {
				customAnimationSamplers.Add(new CustomAnimationSamplerWrapper<TComponent, TData>(sampler));
				return this;
			}

			public GLTFRecorder Create(
				Transform root,
				Func<Transform, bool> recordTransformInWorldSpace,
				bool recordBlendShapes = true,
				bool recordAnimationPointer = false,
				bool recordVisibility = false
			) => new(
				root,
				recordTransformInWorldSpace,
				recordBlendShapes,
				recordAnimationPointer,
				recordVisibility,
				customAnimationSamplers
			);
		}
	
		
		private readonly bool recordBlendShapes;
		private readonly bool recordAnimationPointer;
		
		internal GLTFRecorder(
			Transform root,
			Func<Transform, bool> recordTransformInWorldSpace,
			bool recordBlendShapes = true,
			bool recordAnimationPointer = false,
			bool recordVisibility = false,
			IEnumerable<AnimationSampler>? additionalSamplers = null
		) {
			if (!root)
				throw new ArgumentNullException(nameof(root), "Please provide a root transform to record.");

			this.animationSamplers = AnimationSamplers.From(
				recordTransformInWorldSpace,
				recordVisibility,
				recordBlendShapes,
				recordAnimationPointer,
				additionalSamplers
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

		// this is a cache for the otherwise very allocation-heavy GetComponentsInChildren calls for every frame while recording
		private readonly List<Transform> transformCache = new List<Transform>();
		
		private readonly AnimationSamplers animationSamplers;

		private float startTime;
		private float lastRecordedTime;
		private bool hasRecording;
		private bool isRecording;
		
		public bool HasRecording => hasRecording;
		public bool IsRecording => isRecording;

		
		/// <summary>
		/// Application Time when the most recent sample was recorded
		/// </summary>;
		public float LastRecordedTime => lastRecordedTime;
		
		public float RecordingStartTime => startTime;
		

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
			public float[] Times;
			public object[] Values;
			
			public Object AnimatedObject { get; }
			public string PropertyName { get; }
			
			internal PostAnimationData(Object animatedObject, string propertyName, float[] times, object[] values) {
				this.AnimatedObject = animatedObject;
				this.PropertyName = propertyName;
				this.Times = times;
				this.Values = values;
			}
		}
		
		public void StartRecording(float time, bool includeInactiveTransforms = true)
		{
			startTime = time;
			lastRecordedTime = 0;
			
			root.GetComponentsInChildren<Transform>(includeInactiveTransforms, transformCache);
			recordingAnimatedTransforms.Clear();

			foreach (var tr in transformCache)
			{
				if (!allowRecordingTransform(tr)) continue;
				recordingAnimatedTransforms.Add(tr, new AnimationData(animationSamplers, tr, lastRecordedTime));
			}
			transformCache.Clear();

			isRecording = true;
			hasRecording = true;
		}

		private static readonly ProfilerMarker updateRecordingSingleIterationMarker = new ProfilerMarker("Update Recording - Single Iteration");
		
		/// <summary>
		/// Update the recorded state that will be saved as gltf animations for all transforms under the root transform of the recording.
		/// </summary>
		/// <param name="time">time to record at</param>
		/// <exception cref="InvalidOperationException">thrown if the recorder is not recording when this is called</exception>
		public void UpdateRecording(float time)
		{
			Profiler.BeginSample("Get Transforms");
			root.GetComponentsInChildren(true, transformCache);
			Profiler.EndSample();
			updateRecording(time, transformCache);
			Profiler.BeginSample("Clear Transform Cache");
			transformCache.Clear();
			Profiler.EndSample();
		}
		
		/// <summary>
		/// Very similar to <see cref="UpdateRecording"/>, but only updates the recorded state for the transforms passed in
		/// </summary>
		/// <param name="time">time to record at</param>
		/// <param name="transforms">the transforms for which to update the recorded state</param>
		/// <exception cref="InvalidOperationException">thrown if the recorder is not recording when this is called or if any of the transforms passed
		/// in is not parented directly or indirectly to the root</exception>
		public void UpdateRecordingFor(float time, IReadOnlyList<Transform> transforms) {
			Profiler.BeginSample("Check transforms are parented properly");
			foreach (var transform in transforms) {
				if (transform && !transform.IsChildOf(root))
					throw new InvalidOperationException(
						$"Transform {transform.name} passed in for recording is not parented to the recording root transform. This is not allowed"
					);
			}
			Profiler.EndSample();

			updateRecording(time, transforms);
		}

		private void updateRecording(float time, IReadOnlyList<Transform> transforms) {
			if (!isRecording)
			{
				throw new InvalidOperationException($"{nameof(GLTFRecorder)} isn't recording, but {nameof(UpdateRecording)} was called. This is invalid.");
			}

			if (time <= lastRecordedTime)
			{
				Debug.LogWarning($"Can't record backwards in time, please avoid this (Tried to record at {time}, but it is already {lastRecordedTime}).");
				return;
			}

			var timeSinceStart = time - startTime;
			foreach (var tr in transforms) {
				using var _ = updateRecordingSingleIterationMarker.Auto();
				
				if (!allowRecordingTransform(tr)) continue;
				if (!recordingAnimatedTransforms.ContainsKey(tr))
				{
					Profiler.BeginSample("Update Recording - Add New Transform");
					// because lastRecordedTime > 0, this will insert an "empty" frame with scale=0,0,0 at time = 0
					// because this object just appeared in this frame
					var emptyData = new AnimationData(animationSamplers, tr, lastRecordedTime);
					recordingAnimatedTransforms.Add(tr, emptyData);
					Profiler.EndSample();
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
				+ "Total Keyframes: " + recordingAnimatedTransforms.Sum(x => x.Value.tracks.Sum(y => y.ValuesUntyped.Count())));
			#endif

			// release any excess memory of the cache as fast as we can
			transformCache.Clear();
			transformCache.TrimExcess();
			return true;
		}
		
		public GLTFSceneExporter CreateSceneExporterAfterRecording(GLTFSettings? settings = null, IEnumerable<Transform>? ignoredTransforms = null, ILogger? logger = null) 
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
				new ExportContext(settings, ignoredTransforms ?? Enumerable.Empty<Transform>()) { AfterSceneExport = PostExport, logger = logger };

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
			var exporter = CreateSceneExporterAfterRecording(settings, logger: new Logger(logHandler));
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

		public void CollectAndProcessAnimation(AnimationDataCollector gltfSceneExporter, GLTFAnimation anim, bool calculateTranslationBounds, out Bounds translationBounds)
		{
			var gotFirstValue = false;
			translationBounds = new Bounds();

			foreach (var kvp in recordingAnimatedTransforms) {
				
				using var _ = processAnimationMarker.Auto();
				
				var weHadAScaleTrack = false;
				
				var visibilityTrack = kvp.Value.visibilityTrack;
				
				foreach (var track in kvp.Value.tracks) {
					collectAndProcessSingleTrack(
						gltfSceneExporter,
						anim,
						track,
						visibilityTrack,
						calculateTranslationBounds,
						ref gotFirstValue,
						ref translationBounds,
						ref weHadAScaleTrack
					);
				}

				if (visibilityTrack != null && !weHadAScaleTrack) {
					if (visibilityTrack.Values.Length <= 2) {
						// use nullable to be able to distinguish "no value" from "first value is invisible"
						bool? first = visibilityTrack.Values.Length > 0 ? visibilityTrack.Values[0] : null;
						if (first == null || visibilityTrack.Values.All(v => first == v)) 
							continue;
					}
					
					var (interpolation, times, scales) = visibilityTrackToScaleTrack(visibilityTrack);
					gltfSceneExporter.AddAnimationData(kvp.Key, "scale", anim, interpolation, times, scales.Cast<object>().ToArray());
				}
			}
		}

		private void collectAndProcessSingleTrack(
			AnimationDataCollector gltfSceneExporter,
			GLTFAnimation animation,
			AnimationTrack track,
			VisibilityTrack? visibilityTrack,
			bool calculateTranslationBounds,
			ref bool foundFirstTranslationTrack,
			ref Bounds translationBounds,
			ref bool foundScaleTrack
		) {
			if (track.Times.Length == 0) return;
			
			var animatedObject = track.AnimatedObjectUntyped;
			if(animatedObject == null) return;

			var trackName = track.PropertyName;
			var trackTimes = track.Times;
			var trackValues = track.ValuesUntyped;
					
			// AnimationData always has a visibility track, and if there is also a scale track, merge them
			if (track.PropertyName == "scale" && track is AnimationTrack<Transform, Vector3> scaleTrack) {
				// GLTF does not internally support a visibility state (animation).
				// So to simulate support for that, merge the visibility track with the scale track
				// forcing the scale to (0,0,0) whenever the model is invisible
				foundScaleTrack = true;
				var result = mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
				if (result == null) return;
				
				trackTimes = result!.Value.times;
				trackValues = result!.Value.mergedScales.Cast<object>().ToArray();
			}

			// tracks that contain only an initial entry or two entries that
			// are identical do not bring any benefit - they only bloat the file
			if (trackValues.Length <= 2 && 
				trackValues.All(v => track.InitialValueUntyped?.Equals(v) ?? false))
				return;
			
			OnBeforeAddAnimationData?.Invoke(
				new PostAnimationData(animatedObject, trackName, trackTimes, trackValues)
			);

			if (calculateTranslationBounds && track.PropertyName == "translation") {
						
				foreach (var t in trackValues) {
					var vec = (Vector3) t;
					if (!foundFirstTranslationTrack)
					{
						translationBounds = new Bounds(vec, Vector3.zero);
						foundFirstTranslationTrack = true;
					}
					else
					{
						translationBounds.Encapsulate(vec);
					}
				}
			}
			
			// TODO FIlter tracks that only have one or two samples that dont change the default value
			//GLTFSceneExporter.RemoveUnneededKeyframes(ref trackTimes, ref trackValues);
			gltfSceneExporter.AddAnimationData(track.AnimatedObjectUntyped, track.PropertyName, animation, track.InterpolationType, trackTimes, trackValues);
		}

		/// use this only if you only have a visibility track, no scale, otherwise use <see cref="mergeVisibilityAndScaleTracks"/> instead to merge the two 
		internal static (InterpolationType interpolation, float[] times, Vector3[] mergedScales)
			visibilityTrackToScaleTrack(AnimationTrack<GameObject, bool> visibilityTrack) {
			var visTimes = visibilityTrack.Times;
			var visValues = visibilityTrack.Values;
			var visScaleValues = visValues.Select(vis => vis ? Vector3.one : Vector3.zero).ToArray();
			return (InterpolationType.STEP, visTimes, visScaleValues);
		}

		internal static (InterpolationType interpolation, float[] times, Vector3[] mergedScales)?
			mergeVisibilityAndScaleTracks(
				AnimationTrack<GameObject, bool>? visibilityTrack,
				AnimationTrack<Transform, Vector3>? scaleTrack
			) {
			if (visibilityTrack == null && scaleTrack == null) return null;
			if (visibilityTrack == null) return (scaleTrack!.InterpolationType, scaleTrack.Times, scaleTrack.Values);

			if (scaleTrack == null) return visibilityTrackToScaleTrack(visibilityTrack);
			// both tracks are present, need to merge, but visibility always takes precedence

			var currentState = new MergeVisibilityAndScaleTrackMerger(
				visibilityTrack.Times,
				visibilityTrack.Values,
				scaleTrack.Times,
				scaleTrack.Values
			);
			var merged = currentState.Merge().ToArray();
			
			// process both
			return (scaleTrack.InterpolationType, merged.Select(t => t.Time).ToArray(),
				merged.Select(t => t.mergedScale).ToArray());
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
	
	internal static class FloatExtensions {
		internal static bool nearlyEqual(this float a, float b, float epsilon = float.Epsilon) => Math.Abs(a - b) < epsilon;

		// one microsecond
		private const float desiredTimeDelta = 0.100f;
		
		// works for positive d only. PreviousD must be smaller than d
		internal static float nextSmaller(this float d, float previousD) {
			// if the value is so large that subtracting the smallest
			// delta does not change the value, return the next smallest possible value
			if (d - desiredTimeDelta < d) {
				var candidate = d - desiredTimeDelta;
				if (candidate > previousD)
					return candidate;
				// we would create discontinuities in the animation if we returned a smaller value compared to the
				// previous value, so do additional stuff to avoid that.
				// This will fail if d and previousD have no representable value between them -
				// we cant really do anything about that though
				return previousD + 0.5f * (d - previousD);
			}
			else {
				// d is so large that subtracting the smallest delta did not
				// change the value because it lies between representable values,
				// instead return the next smaller, representable value
				if (!double.IsFinite(d))
					return -float.Epsilon;
				var bits = BitConverter.SingleToInt32Bits(d);
				// we can directly return this without checking previous here because there are only two possibilities:
				// (1) The candidate is smaller or equal to previous => since candidate is the next smaller possible value below d and we require previousD < d => previousD is also the first possible smaller value below d,
				//     so we dont have any value to choose in between them anyway
				// (2) The candidate is larger than previous => we can return the candidate because
				//     it is larger than previous
				return BitConverter.Int32BitsToSingle(bits - 1);
			}
		}
	}
}
