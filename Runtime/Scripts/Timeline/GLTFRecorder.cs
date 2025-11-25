#nullable enable
#define USE_ANIMATION_POINTER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GLTF.Schema;
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
				TimeSpan animationTimeStep,
				TimeSpan animationStartOffset,
				bool recordBlendShapes = true,
				bool recordAnimationPointer = false,
				bool recordVisibility = false
			) => new(
				root,
				recordTransformInWorldSpace,
				animationTimeStep,
				animationStartOffset,
				recordBlendShapes,
				recordAnimationPointer,
				recordVisibility,
				customAnimationSamplers
			);
		}
	
		
		
		
		internal GLTFRecorder(
			Transform root,
			Func<Transform, bool> recordTransformInWorldSpace,
			TimeSpan animationTimeStep,
			TimeSpan animationStartOffset,
			bool recordBlendShapes = true,
			bool recordAnimationPointer = false,
			bool recordVisibility = false,
			IEnumerable<AnimationSampler>? additionalSamplers = null
		) {
			if (!root)
				throw new ArgumentNullException(nameof(root), "Please provide a root transform to record.");

			var animatedMaterialColorCount = 0;
			if (recordAnimationPointer) {
				// Idea: At this point we know all renderers that are exported,
				// so we can determine the maximum count of materials per mesh we need to support for animation.
				// This should have little performance impact, since it only happens once, but just in case we profile it.
				Profiler.BeginSample("GLTF Recorder: Count Animated Material Colors");
				var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
				animatedMaterialColorCount = renderers.Length == 0 ? 0 : renderers.Max(r => r.sharedMaterials.Length);
				Debug.Log("GLTF Recorder: Found a maximum of " + animatedMaterialColorCount + " materials per mesh to record.");
				Profiler.EndSample();
			}
			
			this.recorderData = new RecorderData(
				animationTimeStep,
				animationStartOffset,
				recordBlendShapes,
				recordAnimationPointer,
				root,
				AnimationSamplers.From(
					recordTransformInWorldSpace,
					recordVisibility,
					recordBlendShapes,
					animatedMaterialColorCount,
					additionalSamplers
				)
			);
			this._recorderState = new RecorderState.NotRecording();
		}


		private sealed record RecorderData(
			TimeSpan AnimationTimeStep,
			TimeSpan AnimationStartOffset,
			bool RecordBlendShapes,
			bool RecordAnimationPointer,
			Transform Root,
			AnimationSamplers AnimationSamplers
		);
		
		internal abstract class RecorderState
		{
			private RecorderState() { }

			public sealed class NotRecording : RecorderState { }

			public sealed class CurrentlyRecording : RecorderState
			{
				public CurrentlyRecording(
					ulong lastRecordedSampleNumber,
					Dictionary<Transform, AnimationData> currentlyRecordingTransforms,
					List<Transform> transformCache
				) {
					this.LastRecordedSampleNumber = lastRecordedSampleNumber;
					this.CurrentlyRecordingTransforms = currentlyRecordingTransforms;
					this.TransformCache = transformCache;
				}
				
				public ulong LastRecordedSampleNumber { get; set; }
				public Dictionary<Transform, AnimationData> CurrentlyRecordingTransforms { get; }
				public List<Transform> TransformCache { get; init; }
			}

			public sealed class RecordingFinished : RecorderState
			{
				public RecordingFinished(ulong lastRecordedSampleNumber,
					Dictionary<Transform, AnimationData> recordedTransforms
				) {
					this.LastRecordedSampleNumber = lastRecordedSampleNumber;
					this.RecordedTransforms = recordedTransforms;
				}
				public ulong LastRecordedSampleNumber { get; init; }
				public Dictionary<Transform, AnimationData> RecordedTransforms { get; init; }
			}

		}
		
		private readonly RecorderData recorderData;
		private RecorderState _recorderState;
		
		public bool IsRecording => _recorderState is RecorderState.CurrentlyRecording;


		/// <summary>
		/// Application Time when the most recent sample was recorded
		/// </summary>;
		public TimeSpan? LastRecordedTime => _recorderState is RecorderState.CurrentlyRecording rec
			? sampleIndexToTimeOffset(rec.LastRecordedSampleNumber)
			: null;

		public TimeSpan RecordingStartTime => recorderData.AnimationStartOffset;
		

		public string AnimationName = "Recording";

		public delegate void OnBeforeAddAnimationDataDelegate(PostAnimationData animationData);
		public delegate void OnPostExportDelegate(PostExportArgs animationData);
		
		/// <summary>
		/// Callback to modify the animation data before it is added to the animation.
		/// Is called once for each track after the recording has ended. This is a non destructive callback,
		/// so the original recorded data is not modified. Every time you call EndRecording to the save the gltf/glb,
		/// you can modify the data again. 
		/// </summary>
		public OnBeforeAddAnimationDataDelegate? OnBeforeAddAnimationData;
		
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
			public ulong[] Times;
			public object[] Values;
			
			public Object AnimatedObject { get; }
			public string PropertyName { get; }
			
			internal PostAnimationData(Object animatedObject, string propertyName, ulong[] times, object[] values) {
				this.AnimatedObject = animatedObject;
				this.PropertyName = propertyName;
				this.Times = times;
				this.Values = values;
			}
		}
		
		public void StartRecording(bool includeInactiveTransforms = true) {
			
			if(_recorderState is not RecorderState.NotRecording)
				throw new Exception("Cannot start recording because the recorder is already recording or has finished recording.");
			
			var recordingTransforms = new Dictionary<Transform, AnimationData>(64);
			var transformCache = new List<Transform>(64);
			
			recorderData.Root.GetComponentsInChildren<Transform>(includeInactiveTransforms, transformCache);
			
			foreach (var tr in transformCache) {
				var emptyData = new AnimationData(recorderData.AnimationSamplers, tr, 0);
				recordingTransforms.Add(tr, emptyData);
			}
			
			var recordingState = new RecorderState.CurrentlyRecording(
				lastRecordedSampleNumber: 0,
				currentlyRecordingTransforms: recordingTransforms,
				transformCache: transformCache
			);
			transformCache.Clear();
			
			_recorderState = recordingState;
		}
		
		private static readonly ProfilerMarker updateRecordingSingleIterationMarker = new ProfilerMarker("Update Recording - Single Iteration");
		
		/// <summary>
		/// Update the recorded state that will be saved as gltf animations for all transforms under the root transform of the recording.
		/// </summary>
		/// <param name="time">time to record at</param>
		/// <exception cref="InvalidOperationException">thrown if the recorder is not recording when this is called</exception>
		public void UpdateRecording(TimeSpan currentTime)
		{
			if(_recorderState is not RecorderState.CurrentlyRecording recording)
				throw new InvalidOperationException("Cannot update recording because the recorder is not currently recording.");
			
			Profiler.BeginSample("Get Transforms");
			recorderData.Root.GetComponentsInChildren(true, recording.TransformCache);
			Profiler.EndSample();
			updateRecording(currentTime, recording.TransformCache);
			Profiler.BeginSample("Clear Transform Cache");
			recording.TransformCache.Clear();
			Profiler.EndSample();
		}
		
		/// <summary>
		/// Very similar to <see cref="UpdateRecording"/>, but only updates the recorded state for the transforms passed in
		/// </summary>
		/// <param name="time">time to record at</param>
		/// <param name="transforms">the transforms for which to update the recorded state</param>
		/// <exception cref="InvalidOperationException">thrown if the recorder is not recording when this is called or if any of the transforms passed
		/// in is not parented directly or indirectly to the root</exception>
		public void UpdateRecordingFor(TimeSpan currentTime, IReadOnlyCollection<Transform> transforms) {
			Profiler.BeginSample("Check transforms are parented properly");
			foreach (var transform in transforms) {
				if (transform && !transform.IsChildOf(recorderData.Root))
					throw new InvalidOperationException(
						$"Transform {transform.name} passed in for recording is not parented to the recording root transform. This is not allowed"
					);
			}
			Profiler.EndSample();

			updateRecording(currentTime, transforms);
		}
		
		private void updateRecording(TimeSpan currentTime, IReadOnlyCollection<Transform> transforms) {
			if(_recorderState is not RecorderState.CurrentlyRecording recording)
				throw new InvalidOperationException("Cannot update recording because the recorder is not currently recording.");

			if(currentTime < recorderData.AnimationStartOffset)
				throw new InvalidOperationException($"Cannot sample the animation at {currentTime} because it is before the animation starts at {recorderData.AnimationStartOffset}");
			
			var sampleIndex = (ulong) Math.Floor((currentTime - RecordingStartTime) / recorderData.AnimationTimeStep);
			var lastSampleNumber = recording.LastRecordedSampleNumber;
			if (sampleIndex <= lastSampleNumber)
			{
				Debug.LogWarning($"Can't record backwards in time, please avoid this (Tried to record at {sampleIndex}, but it is already {lastSampleNumber}).");
				return;
			}
			foreach (var tr in transforms) {
				using var _ = updateRecordingSingleIterationMarker.Auto();
				if (!recording.CurrentlyRecordingTransforms.TryGetValue(tr, out var recordingData))
				{
					Profiler.BeginSample("Update Recording - Add New Transform");
					Debug.Log("Found previously unknown transform during recording.");
					// because lastRecordedTime > 0, this will insert an "empty" frame with scale=0,0,0 at time = 0
					// because this object just appeared in this frame
					
					recordingData = new AnimationData(recorderData.AnimationSamplers, tr, lastSampleNumber);
					
					recording.CurrentlyRecordingTransforms.Add(tr, recordingData);
					Profiler.EndSample();
				} else {
					recordingData.Update(sampleIndex);	
				}
				
			}
			recording.LastRecordedSampleNumber = sampleIndex;
		}

		private TimeSpan sampleIndexToTimeOffset(ulong ind) => recorderData.AnimationStartOffset + ind * recorderData.AnimationTimeStep;

		public bool EndRecording()
		{
			if(_recorderState is not RecorderState.CurrentlyRecording recording)
				throw new Exception($"Cannot end recording because the recorder is not currently recording. It is {_recorderState.GetType().Name}.");
			
			#if UNITY_EDITOR
			Debug.Log("Gltf Recording saved. "
				+ "Tracks: " + recording.CurrentlyRecordingTransforms.Count + ", "
				+ "Total Keyframes: " + recording.CurrentlyRecordingTransforms.Sum(x => x.Value.tracks.Sum(y => y.ValuesUntyped.Count())));
			#endif
			
			_recorderState = new RecorderState.RecordingFinished(
				recording.LastRecordedSampleNumber,
				recording.CurrentlyRecordingTransforms
			);
			
			return true;
		}
		
		internal IReadOnlyDictionary<Transform, AnimationData>? endRecordingAndGetAnimationTracks() {
			EndRecording();
			
			if (_recorderState is not RecorderState.RecordingFinished recording) {
				return null;
			}
			return recording.RecordedTransforms;
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
			if (!recorderData.RecordAnimationPointer)
				settings.ExportPlugins.RemoveAll(x => x is AnimationPointerExport);
			else if (!settings.ExportPlugins.Any(x => x is AnimationPointerExport))
				settings.ExportPlugins.Add(ScriptableObject.CreateInstance<AnimationPointerExport>());

			if (!recorderData.RecordBlendShapes)
				settings.BlendShapeExportProperties = GLTFSettings.BlendShapeExportPropertyFlags.None;
			
			var exportContext =
				new ExportContext(settings, ignoredTransforms ?? Enumerable.Empty<Transform>()) { AfterSceneExport = PostExport, logger = logger };

			return new GLTFSceneExporter(new Transform[] { recorderData.Root }, exportContext);
		}

		public void EndRecordingAndSaveToFile(string filepath, string sceneName = "scene", GLTFSettings? settings = null)
		{
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
			if(_recorderState is not RecorderState.RecordingFinished recording)
				throw new Exception("Cannot collect animation because the recorder has not finished recording yet.");
			
			var gotFirstValue = false;
			translationBounds = new Bounds();

			foreach (var kvp in recording.RecordedTransforms) {
				
				using var _ = processAnimationMarker.Auto();
				
				var weHadAScaleTrack = false;
				
				var visibilityTrack = kvp.Value.visibilityTrack;
				
				foreach (var track in kvp.Value.tracks) {
					collectAndProcessSingleTrack(
						gltfSceneExporter,
						anim,
						kvp.Key,
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
					
					var (interpolation, sampleIds, scales) = visibilityTrackToScaleTrack(visibilityTrack);
					var times = sampleIds.Select(sid => (float) sampleIndexToTimeOffset(sid).TotalSeconds).ToArray();
					gltfSceneExporter.AddAnimationData(kvp.Key, kvp.Key, "scale", anim, interpolation, times, scales.Cast<object>().ToArray());
				}
			}
		}

		private void collectAndProcessSingleTrack(
			AnimationDataCollector gltfSceneExporter,
			GLTFAnimation animation,
			// This is not guaranteed to be the same as track.AnimatedObjectUntyped (for example if the track is exporting a material)
			Transform trackTargetTransform,
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
			var trackSampleNumbers = track.Times;
			var trackValues = track.ValuesUntyped;
					
			// AnimationData always has a visibility track, and if there is also a scale track, merge them
			if (track.PropertyName == "scale" && track is AnimationTrack<Transform, Vector3> scaleTrack) {
				// GLTF does not internally support a visibility state (animation).
				// So to simulate support for that, merge the visibility track with the scale track
				// forcing the scale to (0,0,0) whenever the model is invisible
				foundScaleTrack = true;
				var result = mergeVisibilityAndScaleTracks(visibilityTrack, scaleTrack);
				if (result == null) return;
				
				trackSampleNumbers = result!.Value.times.ToArray();
				trackValues = result!.Value.mergedScales.Cast<object>().ToArray();
			}

			// tracks that contain only an initial entry or two entries that
			// are identical do not bring any benefit - they only bloat the file
			if (trackValues.Length <= 2 && 
				trackValues.All(v => track.LastValueUntyped?.Equals(v) ?? false))
				return;

			
			OnBeforeAddAnimationData?.Invoke(
				new PostAnimationData(animatedObject, trackName, trackSampleNumbers, trackValues)
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
			
			var (filteredTimes, filteredValues) = AnimationFilteringUtils.RemoveUnneededKeyframes(trackSampleNumbers, trackValues);
			(trackSampleNumbers, trackValues) = (filteredTimes.ToArray(), filteredValues.ToArray());
			
			var trackTimes = trackSampleNumbers.Select(sid => (float) sampleIndexToTimeOffset(sid).TotalSeconds).ToArray();
			gltfSceneExporter.AddAnimationData(trackTargetTransform, track.AnimatedObjectUntyped, track.PropertyName, animation, track.InterpolationType, trackTimes, trackValues);
		}

		/// use this only if you only have a visibility track, no scale, otherwise use <see cref="mergeVisibilityAndScaleTracks"/> instead to merge the two 
		internal static (AnimationInterpolationType interpolation, IEnumerable<ulong> times, IEnumerable<Vector3> mergedScales)
			visibilityTrackToScaleTrack(AnimationTrack<GameObject, bool> visibilityTrack) {

			
			var inTimes = visibilityTrack.Times;
			var inValues = visibilityTrack.Values;
			
			var outTimes = new List<ulong>();
			var outScale = new List<Vector3>();
			
			for (var vi = 0; vi < inTimes.Length; vi++) {
				var time = inTimes[vi];
				var value = visibilityTrack.Values[vi];

				if (vi > 0 && inTimes[vi - 1] < time - 1) {
					outScale.Add(inValues[vi-1] ? Vector3.one : Vector3.zero);
					outTimes.Add(time - 1);
				}
				
				outScale.Add(value ? Vector3.one : Vector3.zero);
				outTimes.Add(time);
			}
			return (AnimationInterpolationType.LINEAR, outTimes, outScale);
		}

		internal static (AnimationInterpolationType interpolation, IEnumerable<ulong> times, IEnumerable<Vector3> mergedScales)?
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
			return (scaleTrack.InterpolationType, merged.Select(t => t.Time),
				merged.Select(t => t.mergedScale));
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

		// This Delta time is used to offset additional samples needed for combining scale and visibility.
		// It is set to 100 ms, which should result in a large enough margin of error
		private const float desiredTimeDelta = 0.100f;
		
		// works for positive d only. PreviousD must be smaller than d
		internal static float nextSmaller(this float time, float previousTime = 0.0f) {
			// if the value is so large that subtracting the smallest
			// delta does not change the value, return the next smallest possible value
			var candidate = time - desiredTimeDelta;
			return candidate > previousTime && candidate < time ? candidate :
				// we cant use the desired time delta because that would be smaller or equal to the last sampled time
				// or the next representable value is the previous sampled time.
				// In this case we instead use the next smallest representable value from time.
				// In rare edge cases this might still be equal to the previous time,
				// but we have no correct solution in that case anyway.
				time.nextSmallerRepresentable();
		}

		internal static float nextSmallerRepresentable(this float f) {
			if (!float.IsFinite(f))
				return -float.Epsilon;
			var bits = BitConverter.SingleToInt32Bits(f);
			// We can directly return this without checking previous here because there are only two possibilities:
			// (1) The candidate is smaller or equal to previous => since candidate is the next smaller possible
			//     value below time, and we require previousTime < time => previousTime is also the first possible
			//     smaller value below time, so we don't have any value to choose in between them anyway
			// (2) The candidate is larger than previous => we can return the candidate because
			//     it is larger than previous
			return BitConverter.Int32BitsToSingle(bits - 1);
		}
	}
}
