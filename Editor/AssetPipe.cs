#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Sigtrap.AssetPipe {
	#region Internal Types
	#region Structs
	public struct ProcessProgress {
		public int index {get; private set;}
		public int assetCount {get; private set;}
		public float progress {
			get {
				return ((float)(index+1))/((float)assetCount);
			}
		}

		public ProcessProgress(int index, int assetCount){
			this.index = index;
			this.assetCount = assetCount;
		}
	}
	public struct AssetMetadata<T> where T:Object {
		public string name {get; private set;}
		public string guid {get; private set;}
		public string path {get; private set;}
		public T asset {get; private set;}
		public bool isValid {get; private set;}

		public AssetMetadata(string guid){
			this.guid = guid;
			path = AssetDatabase.GUIDToAssetPath(guid);
			asset = AssetDatabase.LoadAssetAtPath<T>(path);
			isValid = asset != null;
			name = isValid ? asset.name : "NO ASSET";
		}
	}

	public struct AssetProcessData<T> where T:Object {
		/// <summary>The asset to process.</summary>
		public T asset {get; private set;}
		/// <summary>Metadata for the asset.</summary>
		public AssetMetadata<T> metadata {get; private set;}
		/// <summary>Current progress of the entire process.</summary>
		public ProcessProgress progress {get; private set;}

		public AssetProcessData(T asset, AssetMetadata<T> metadata, int currentAsset, int assetCount){
			this.asset = asset;
			this.metadata = metadata;
			progress = new ProcessProgress(currentAsset, assetCount);
		}
	}
	public struct PrefabProcessData {
		/// <summary>The prefab to process.</summary>
		public GameObject prefab {get; private set;}
		/// <summary>Metadata for the prefab asset.</summary>
		public AssetMetadata<GameObject> metadata {get; private set;}
		/// <summary>Current progress of the entire process.</summary>
		public ProcessProgress progress {get; private set;}

		public PrefabProcessData(GameObject prefab, AssetMetadata<GameObject> metadata, int currentAsset, int assetCount){
			this.prefab = prefab;
			this.metadata = metadata;
			progress = new ProcessProgress(currentAsset, assetCount);
		}
	}
	public struct ComponentProcessData<T> where T:Component {
		/// <summary>The component to process.</summary>
		public T component {get; private set;}
		/// <summary>Metadata for the prefab asset this component exists on.</summary>
		public AssetMetadata<GameObject> metadata {get; private set;}
		/// <summary>Current progress of the entire process.</summary>
		public ProcessProgress progress {get; private set;}

		public ComponentProcessData(T component, AssetMetadata<GameObject> metadata, int currentAsset, int assetCount){
			this.component = component;
			this.metadata = metadata;
			progress = new ProcessProgress(currentAsset, assetCount);
		}
	}

	public struct ProcessDoneData {
		public System.Guid processId {get; private set;}
		public ProcessExitStatus exitStatus {get; private set;}
		public string exitMessage {get; private set;}
		public double elapsedSeconds {get; private set;}

		public ProcessDoneData(System.Guid processId, string exitMessage, System.Diagnostics.Stopwatch stopwatch, ProcessExitStatus exitStatus){
			this.processId = processId;
			this.exitMessage = exitMessage;
			this.elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
			this.exitStatus = exitStatus;
		}

		public override string ToString(){
			return string.Format("Process {0} {1} in {2} seconds [Message: \"{3}\"]", processId, exitStatus, elapsedSeconds, exitMessage);
		}
	}
	#endregion

	#region Delegates
	public delegate void AssetProcessCallback<T>(AssetProcessData<T> data) where T:Object;
	public delegate bool AssetMatchCallback<T>(T asset, AssetMetadata<T> metadata) where T:Object;
	public delegate void AssetsResultsCallback<T>(List<T> results) where T:Object;

	public delegate void PrefabProcessCallback(PrefabProcessData data);
	public delegate bool PrefabMatchCallback(GameObject prefab, AssetMetadata<GameObject> metadata);
	public delegate void PrefabsResultsCallback(List<GameObject> results);

	public delegate void ComponentProcessCallback<T>(ComponentProcessData<T> data) where T:Component;
	public delegate bool ComponentMatchCallback<T>(T component, AssetMetadata<GameObject> metadata) where T:Component;
	public delegate void ComponentsResultsCallback<T>(Dictionary<GameObject, List<T>> results) where T:Component;

	public delegate void ProcessDoneCallback(ProcessDoneData data);
	#endregion

	public enum ComponentSearchType {
		/// <summary>Only find components on root object</summary>
		ROOT_ONLY,
		/// <summary>Find components on root and active children</summary>
		CHILDREN_ACTIVE,
		/// <summary>Find components on root, active children and inactive children</summary>
		CHILDREN_ALL
	}

	public enum ProcessType {
		/// <summary>Doesn't block editor while processing. Can be cancelled manually with Pipeline.CancelProcess.</summary>
		ASYNC,
		/// <summary>Blocks editor with progress bar.</summary>
		BLOCKING,
		/// <summary>Blocks editor with progress bar and cancel button.</summary>
		BLOCKING_CANCELABLE
	}

	public enum ProcessExitStatus {
		/// <summary>Process completed successfully.</summary>
		SUCCESS,
		/// <summary>Process was manually cancelled before completion.</summary>
		CANCELLED,
		/// <summary>Process failed.</summary>
		FAILED
	}
	#endregion

	public static class Pipeline {
		const string DEFAULT_FILTER = "t:Prefab";

		#region API
		#region Get Assets
		/// <summary>
		/// Gets assets from the database as UnityEngine.Object.
		/// </summary>
		/// <returns>Selected assets. Null if cancelled.</returns>
		/// <param name="match">Called for each asset to decide whether to include in results.</param>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		public static List<Object> GetAssets(string filter, AssetMatchCallback<Object> match=null) {
			List<Object> results = new List<Object>();
			
			var guids = AssetDatabase.FindAssets(filter);
			int count = guids.Length;

			for (int i=0; i<count; ++i){
				AssetMetadata<Object> meta = new AssetMetadata<Object>(guids[i]);
				Object asset = meta.asset;

				if (MatchAsset(meta, match)){
					results.Add(asset);
				}

				if (DisplayProgressBar("Getting Assets", i, count, true)){
					ClearProgressBar();
					return null;
				}
			}

			ClearProgressBar();
			return results;
		}

		/// <summary>
		/// Gets assets from the database of type T.
		/// </summary>
		/// <returns>Selected assets. Null if cancelled.</returns>
		/// <param name="match">Called for each asset to decide whether to include in results.</param>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <typeparam name="T">Asset type to search for.</typeparam>
		public static List<T> GetAssets<T>(
			string filter, AssetMatchCallback<T> match=null
		) where T:Object {
			List<T> results = new List<T>();
			
			var guids = AssetDatabase.FindAssets(filter);
			int count = guids.Length;

			for (int i=0; i<count; ++i){
				AssetMetadata<T> meta = new AssetMetadata<T>(guids[i]);
				T asset = meta.asset;

				if (MatchAsset(meta, match)){
					results.Add(asset);
				}

				if (DisplayProgressBar("Getting Assets", i, count, true)){
					ClearProgressBar();
					return null;
				}
			}

			ClearProgressBar();
			return results;
		}

		/// <summary>
		/// Gets prefabs from the database.
		/// </summary>
		/// <returns>Selected prefabs. Null if cancelled.</returns>
		/// <param name="match">Called for each prefab to decide whether to include in results. Null matches all.</param>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		public static List<GameObject> GetPrefabs(
			PrefabMatchCallback match=null, string filter=DEFAULT_FILTER
		) {
			return GetPrefabs<Component>(match, null, filter);
		}

		/// <summary>
		/// Gets prefabs from the database with the specific component.
		/// </summary>
		/// <returns>Selected prefabs. Null if cancelled.</returns>
		/// <param name="matchPrefab">Called for each prefab to decide whether to include in results. Null matches all.</param>
		/// <param name="matchComponent">Called for the component found on the prefab to decide whether to include prefab in results. Null matches all.</param>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <typeparam name="T">Only find prefabs with this component on root object.</typeparam>
		public static List<GameObject> GetPrefabs<T>(
			PrefabMatchCallback matchPrefab=null, ComponentMatchCallback<T> matchComponent=null, string filter=DEFAULT_FILTER
		) where T:Component {
			List<GameObject> results = new List<GameObject>();

			var guids = AssetDatabase.FindAssets(filter);
			int count = guids.Length;

			for (int i=0; i<count; ++i){
				AssetMetadata<GameObject> meta = new AssetMetadata<GameObject>(guids[i]);
				GameObject prefab = meta.asset;

				if (MatchPrefab(meta, matchPrefab)){					// Match prefab
					if (MatchComponent(prefab, meta, matchComponent)){	// Match component
						results.Add(prefab);
					}
				}

				if (DisplayProgressBar("Getting Prefabs", i, count, true)){
					ClearProgressBar();
					return null;
				}
			}

			ClearProgressBar();
			return results;
		}

		/// <summary>
		/// Gets components on assets from the database.
		/// </summary>
		/// <returns>Selected components. Null if cancelled.</returns>
		/// <param name="componentSearchType">Search root objects only, active child objects or all children.</param>
		/// <param name="matchPrefab">Called for each prefab to decide whether to check components. Null matches all.</param>
		/// <param name="matchComponent">Called for each component to decide whether to include in results. Null matches all.</param>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <typeparam name="T">Component type to search for.</typeparam>
		public static Dictionary<GameObject, List<T>> GetComponents<T>(
			ComponentSearchType componentSearchType=ComponentSearchType.ROOT_ONLY,
			PrefabMatchCallback matchPrefab=null, ComponentMatchCallback<T> matchComponent=null,
			string filter=DEFAULT_FILTER
		) where T:Component {
			var results = new Dictionary<GameObject, List<T>>();

			var guids = AssetDatabase.FindAssets(filter);
			int count = guids.Length;

			for (int i=0; i<count; ++i){
				AssetMetadata<GameObject> meta = new AssetMetadata<GameObject>(guids[i]);
				GameObject prefab = meta.asset;
				
				if (MatchPrefab(meta, matchPrefab)){				// Not null and match prefab
					T[] ts = GetComponents<T>(prefab, componentSearchType);
					if (ts.Length != 0){
						if (matchComponent == null){					// If no callback, add all
							results.Add(prefab, new List<T>(ts));
						} else {
							List<T> comps = new List<T>();
							foreach (var t in ts){						// Match each component individually
								if (MatchComponent(t, meta, matchComponent)){
									comps.Add(t);
								}
							}
							if (comps.Count != 0){
								results.Add(prefab, comps);
							}
						}
					}
				}

				if (DisplayProgressBar("Getting Components", i, count, true)){
					ClearProgressBar();
					return null;
				}
			}

			ClearProgressBar();
			return results;
		}
		#endregion

		#region Process Assets
		/// <summary>Safely cancel a process at any time. Intended for manual use.</summary>
		public static bool CancelProcess(System.Guid processId){
			return AssetPipeProcessManager.CancelProcess(processId);
		}
		/// <summary>Safely abort a process at any time. Intended for programmatic use.</summary>
		public static bool AbortProcess(System.Guid processId, string errorMessage){
			return AssetPipeProcessManager.AbortProcess(processId, errorMessage);
		}

		/// <summary>Start async asset processing. Loads assets as UnityEngine.Object.</summary>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <param name="onProcessAsset">Called for each matching asset. Implement your process here (including saving!).</param>
		/// <param name="match">Called on each asset to decide whether to process. Null matches all.</param>
		/// <param name="onResults">Callback to receive list of processed assets when done. If null, assets not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		public static System.Guid ProcessAssets(
			string filter, AssetProcessCallback<Object> onProcessAsset, 
			AssetMatchCallback<Object> match=null,
			AssetsResultsCallback<Object> onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) {
			return ProcessAssets<Object>(filter, onProcessAsset, match, onResults, onDone, processType, tickTime);
		}

		/// <summary>Start asset processing for assets of type T.</summary>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <param name="onProcessAsset">Called for each matching asset. Implement your process here (including saving!).</param>
		/// <param name="match">Called on each asset to decide whether to process. Null matches all.</param>
		/// <param name="onResults">Callback to receive list of processed assets when done. If null, assets not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <typeparam name="T">Search for assets of this type.</typeparam>
		public static System.Guid ProcessAssets<T>(
			string filter, AssetProcessCallback<T> onProcessAsset, 
			AssetMatchCallback<T> match=null,
			AssetsResultsCallback<T> onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) where T:Object {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(
				ProcessAssetsAsync<T>(pid, filter, onProcessAsset, match, onResults, onDone, processType, tickTime),
				onDone, pid
			);
			return pid;
		}

		/// <summary>Start prefab processing. Uses default search filter "t:Prefab".</summary>
		/// <param name="onProcessPrefab">Called for each matching prefab. Implement your process here (including saving!).</param>
		/// <param name="match">Called for each prefab to decide whether to process. Null matches all.</param>
		/// <param name="onResults">Callback to receive list of processed prefabs when done. If null, prefabs not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <param name="processType">Asynchronous, blocking or blocking cancellable. Async can be cancelled manually with StopProcess(guid).</param>
		/// <param name="tickTime">Maximum operation time before waiting for next update.</param>
		public static System.Guid ProcessPrefabs (
			PrefabProcessCallback onProcessPrefab,
			PrefabMatchCallback match=null,
			PrefabsResultsCallback onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) {
			return ProcessPrefabs<Component>(DEFAULT_FILTER, onProcessPrefab, match, null, onResults, onDone, processType, tickTime);
		}

		/// <summary>Start prefab processing.</summary>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <param name="onProcessPrefab">Called for each matching prefab. Implement your process here (including saving!).</param>
		/// <param name="match">Called for each prefab to decide whether to process. Null matches all.</param>
		/// <param name="onResults">Callback to receive list of processed prefabs when done. If null, prefabs not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <param name="processType">Asynchronous, blocking or blocking cancellable. Async can be cancelled manually with StopProcess(guid).</param>
		/// <param name="tickTime">Maximum operation time before waiting for next update.</param>
		public static System.Guid ProcessPrefabs (
			string filter, PrefabProcessCallback onProcessPrefab, 
			PrefabMatchCallback match=null,
			PrefabsResultsCallback onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) {
			return ProcessPrefabs<Component>(filter, onProcessPrefab, match, null, onResults, onDone, processType, tickTime);
		}

		/// <summary>Start prefab processing for prefabs with component type T. Uses default search filter "t:Prefab".</summary>
		/// <param name="onProcessPrefab">Called for each matching prefab. Implement your process here (including saving!).</param>
		/// <param name="matchPrefab">Called for each prefab to decide whether to process. Null matches all.</param>
		/// <param name="matchComponent">Called for the component found on the prefab to decide whether to process prefab. Null matches all.</param>
		/// <param name="onResults">Callback to receive list of processed prefabs when done. If null, prefabs not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <param name="processType">Asynchronous, blocking or blocking cancellable. Async can be cancelled manually with StopProcess(guid).</param>
		/// <param name="tickTime">Maximum operation time before waiting for next update.</param>
		/// <typeparam name="T">Search for prefabs with a root component of this type.</typeparam>
		public static System.Guid ProcessPrefabs<T>(
			PrefabProcessCallback onProcessPrefab,
			PrefabMatchCallback matchPrefab=null, ComponentMatchCallback<T> matchComponent=null,
			PrefabsResultsCallback onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) where T:Component {
			return ProcessPrefabs<T>(DEFAULT_FILTER, onProcessPrefab, matchPrefab, matchComponent, onResults, onDone, processType, tickTime);
		}

		/// <summary>Start prefab processing for prefabs with component type T.</summary>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <param name="onProcessPrefab">Called for each matching prefab. Implement your process here (including saving!).</param>
		/// <param name="matchPrefab">Called for each prefab to decide whether to process. Null matches all.</param>
		/// <param name="matchComponent">Called for the component found on the prefab to decide whether to process prefab. Null matches all.</param>
		/// <param name="onResults">Callback to receive list of processed prefabs when done. If null, prefabs not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <param name="processType">Asynchronous, blocking or blocking cancellable. Async can be cancelled manually with StopProcess(guid).</param>
		/// <param name="tickTime">Maximum operation time before waiting for next update.</param>
		/// <typeparam name="T">Search for prefabs with a root component of this type.</typeparam>
		public static System.Guid ProcessPrefabs<T>(
			string filter, PrefabProcessCallback onProcessPrefab,
			PrefabMatchCallback matchPrefab=null, ComponentMatchCallback<T> matchComponent=null,
			PrefabsResultsCallback onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) where T:Component {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(
				ProcessPrefabsAsync<T>(
					pid, filter, onProcessPrefab, matchPrefab, 
					matchComponent, onResults, onDone, processType, tickTime
				),
				onDone, pid
			);
			return pid;
		}

		/// <summary>Start processing of components of type T on prefabs. Uses default search filter "t:Prefab".</summary>
		/// <param name="onProcessComponent">Called for each matching component. Implement your process here (including saving!).</param>
		/// <param name="componentSearchType">Search root objects only, active child objects or all children.</param>
		/// <param name="matchComponent">Called for each component to decide whether to process prefab. Null matches all.</param>
		/// <param name="matchPrefab">Called for each prefab to decide whether to process. Null matches all.</param>
		/// <param name="onResults">Callback to receive dictionary of processed components (by prefab) when done. If null, components not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <param name="processType">Asynchronous, blocking or blocking cancellable. Async can be cancelled manually with StopProcess(guid).</param>
		/// <param name="tickTime">Maximum operation time before waiting for next update.</param>
		/// <typeparam name="T">Component type to search for.</typeparam>
		public static System.Guid ProcessComponents<T>(
			ComponentProcessCallback<T> onProcessComponent, 
			ComponentMatchCallback<T> matchComponent=null, ComponentSearchType componentSearchType=ComponentSearchType.ROOT_ONLY, PrefabMatchCallback matchPrefab=null,
			ComponentsResultsCallback<T> onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) where T:Component {
			return ProcessComponents<T>(
				DEFAULT_FILTER, onProcessComponent, matchComponent, componentSearchType, matchPrefab,
				onResults, onDone, processType, tickTime
			);
		}

		/// <summary>Start processing of components of type T on prefabs.</summary>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <param name="onProcessComponent">Called for each matching component. Implement your process here (including saving!).</param>
		/// <param name="componentSearchType">Search root objects only, active child objects or all children.</param>
		/// <param name="matchComponent">Called for each component to decide whether to process prefab. Null matches all.</param>
		/// <param name="matchPrefab">Called for each prefab to decide whether to process. Null matches all.</param>
		/// <param name="onResults">Callback to receive dictionary of processed components (by prefab) when done. If null, components not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <param name="processType">Asynchronous, blocking or blocking cancellable. Async can be cancelled manually with StopProcess(guid).</param>
		/// <param name="tickTime">Maximum operation time before waiting for next update.</param>
		/// <typeparam name="T">Component type to search for.</typeparam>
		public static System.Guid ProcessComponents<T>(
			string filter, ComponentProcessCallback<T> onProcessComponent, 
			ComponentMatchCallback<T> matchComponent=null, ComponentSearchType componentSearchType=ComponentSearchType.ROOT_ONLY, PrefabMatchCallback matchPrefab=null,
			ComponentsResultsCallback<T> onResults=null, ProcessDoneCallback onDone=null,
			ProcessType processType=ProcessType.BLOCKING, double tickTime=0.1
		) where T:Component {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(
				ProcessComponentsAsync<T>(
					pid, filter, onProcessComponent, matchComponent, componentSearchType, 
					matchPrefab, onResults, onDone, processType, tickTime
				),
				onDone, pid
			);
			return pid;
		}
		#endregion
		#endregion

		#region Internal
		#region Coroutines
		static IEnumerator ProcessAssetsAsync<T>(
			System.Guid processId, string filter, AssetProcessCallback<T> onProcessAsset,
			AssetMatchCallback<T> match,
			AssetsResultsCallback<T> onResults, ProcessDoneCallback onDone,
			ProcessType pt, double tickTime
		) where T:Object {
			var sw = new System.Diagnostics.Stopwatch();
			double lastTick = 0;
			sw.Start();

			List<T> results = null;
			if (onResults != null) results = new List<T>();
			
			var guids = AssetDatabase.FindAssets(filter);
			int count = guids.Length;
			if (count == 0){
				Debug.LogWarningFormat("No assets found in database for filter [{0}].", filter);
			}
			bool cancelled = false;

			for (int i=0; i<count; ++i){
				AssetMetadata<T> meta = new AssetMetadata<T>(guids[i]);
				T asset = meta.asset;

				if (MatchAsset(meta, match)){					// If asset found and meets requirements, process
					if (onProcessAsset != null){
						onProcessAsset(new AssetProcessData<T>(asset, meta, i, count));
					}
					if (results != null) results.Add(asset);	// Add to results
				}

				if (pt == ProcessType.ASYNC){
					if (CheckTime(sw, ref lastTick, tickTime)){
						yield return null;						// Wait for next editor update
					}
				} else {
					if (DisplayProgressBar("Processing Assets", i, count, pt)){
						ClearProgressBar();
						cancelled = true;
						break;
					}
				}
			}

			if (onResults != null) onResults(results);
			if (onDone != null) onDone(
				new ProcessDoneData(
					processId, null, sw,
					cancelled ? ProcessExitStatus.CANCELLED : ProcessExitStatus.SUCCESS					
				) 
			);
			
			if (pt != ProcessType.ASYNC){
				ClearProgressBar();
			}
		}

		static IEnumerator ProcessPrefabsAsync<T>(
			System.Guid processId, string filter, PrefabProcessCallback onProcessPrefab, 
			PrefabMatchCallback matchPrefab, ComponentMatchCallback<T> matchComponent,
			PrefabsResultsCallback onResults, ProcessDoneCallback onDone,
			ProcessType pt, double tickTime
		) where T:Component {
			var sw = new System.Diagnostics.Stopwatch();
			double lastTick = 0;
			sw.Start();

			List<GameObject> results = null;
			if (onResults != null) results = new List<GameObject>();

			var guids = AssetDatabase.FindAssets(filter);
			int count = guids.Length;
			if (count == 0){
				Debug.LogWarningFormat("No assets found in database for filter [{0}].", filter);
			}
			bool cancelled = false;

			for (int i=0; i<count; ++i){
				var meta = new AssetMetadata<GameObject>(guids[i]);
				GameObject prefab = meta.asset;

				if (MatchPrefab(meta, matchPrefab)){					// Does prefab meet requirements?
					if (MatchComponent(prefab, meta, matchComponent)){	// If component found and meets requirements, process
						if (onProcessPrefab != null){
							onProcessPrefab(new PrefabProcessData(prefab, meta, i, count));
						}
						if (results != null) results.Add(prefab);		// Add to results
					}
				}
				
				if (pt == ProcessType.ASYNC){
					if (CheckTime(sw, ref lastTick, tickTime)){
						yield return null;								// Wait for next editor update
					}
				} else {
					if (DisplayProgressBar("Processing Assets", i, count, pt)){
						ClearProgressBar();
						cancelled = true;
						break;
					}
				}
			}
			
			if (onResults != null) onResults(results);
			if (onDone != null) onDone(
				new ProcessDoneData(
					processId, null, sw,
					cancelled ? ProcessExitStatus.CANCELLED : ProcessExitStatus.SUCCESS					
				) 
			);
			
			if (pt != ProcessType.ASYNC){
				ClearProgressBar();
			}
		}

		static IEnumerator ProcessComponentsAsync<T>(
			System.Guid processId, string filter, ComponentProcessCallback<T> onProcessComponent,
			ComponentMatchCallback<T> matchComponent, ComponentSearchType componentSearchType, PrefabMatchCallback matchPrefab, 
			ComponentsResultsCallback<T> onResults, ProcessDoneCallback onDone,
			ProcessType pt, double tickTime
		) where T:Component {
			var sw = new System.Diagnostics.Stopwatch();
			double lastTick = 0;
			sw.Start();

			Dictionary<GameObject, List<T>> results = null;				// Only store results if callback given
			if (onResults != null) results = new Dictionary<GameObject, List<T>>();

			var guids = AssetDatabase.FindAssets(filter);
			int count = guids.Length;
			if (count == 0){
				Debug.LogWarningFormat("No assets found in database for filter [{0}].", filter);
			}
			bool cancelled = false;

			for (int i=0; i<count; ++i){
				AssetMetadata<GameObject> meta = new AssetMetadata<GameObject>(guids[i]);
				GameObject prefab = meta.asset;
				
				if (MatchPrefab(meta, matchPrefab)){					// Does prefab meet requirements?
					T[] ts = GetComponents<T>(prefab, componentSearchType);
					foreach (T t in ts){
						if (MatchComponent(t, meta, matchComponent)){	// If component meets requirements, process
							if (onProcessComponent != null){
								onProcessComponent(new ComponentProcessData<T>(t, meta, i, count));
							}

							if (results != null){						// Add to results
								List<T> l = null;
								if (!results.TryGetValue(prefab, out l)){
									l = new List<T>();
									results.Add(prefab, l);
								}
								l.Add(t);
							}

							if (pt == ProcessType.ASYNC){
								if (CheckTime(sw, ref lastTick, tickTime)){
									yield return null;					// Wait for next editor update
								}
							} else {
								if (DisplayProgressBar("Processing Components", i, count, pt, "Prefab {0}/{1}")){
									ClearProgressBar();
									cancelled = true;
									break;
								}
							}
						}
					}
				}

				if (pt == ProcessType.ASYNC){
					if (CheckTime(sw, ref lastTick, tickTime)){
						yield return null;								// Wait for next editor update
					}
				} else if (cancelled){
					break;												// Break both loops if cancelled
				} else {
					if (DisplayProgressBar("Processing Components", i, count, pt, "Prefab {0}/{1}")){
						ClearProgressBar();
						cancelled = true;
						break;
					}
				}
			}

			if (onResults != null) onResults(results);
			if (onDone != null) onDone(
				new ProcessDoneData(
					processId, null, sw,
					cancelled ? ProcessExitStatus.CANCELLED : ProcessExitStatus.SUCCESS					
				) 
			);

			if (pt != ProcessType.ASYNC){
				ClearProgressBar();
			}
		}
		#endregion		

		#region Coroutine implementation
		private class AssetPipeProcessManager {
			struct ProcessData {
				public System.Guid processId {get; private set;}
				public IEnumerator process {get; private set;}
				ProcessDoneCallback onCancelled;
				System.Diagnostics.Stopwatch _stopwatch;
				public System.Diagnostics.Stopwatch stopwatch {
					get {
						_stopwatch.Stop();
						return _stopwatch;
					}
				}

				public ProcessData(IEnumerator process, ProcessDoneCallback onCancelled, System.Guid processId){
					this.processId = processId;
					this.process = process;
					this.onCancelled = onCancelled;
					_stopwatch = new System.Diagnostics.Stopwatch();
					_stopwatch.Start();
				}
				public void OnCancelled(ProcessDoneData data){
					if (onCancelled != null) onCancelled(data);
				}
			}

			#region Static
			static AssetPipeProcessManager _i;
			static Dictionary<System.Guid, ProcessData> _runningProcesses = new Dictionary<System.Guid, ProcessData>();
			static List<System.Guid> _cancelledProcesses = new List<System.Guid>();
			static Dictionary<System.Guid, string> _abortedProcesses = new Dictionary<System.Guid, string>();

			static void Init(){
				if (_i == null){
					_i = new AssetPipeProcessManager();
					EditorApplication.update += _i.Update;
				}
			}

			public static void StartProcess(IEnumerator coroutine, ProcessDoneCallback onCancelled, System.Guid processId){
				Init();
				ProcessData process = new ProcessData(coroutine, onCancelled, processId);
				_runningProcesses.Add(processId, process);
			}
			public static bool CancelProcess(System.Guid processId){
				return EndProcess(processId, ProcessExitStatus.CANCELLED, null);
			}
			public static bool AbortProcess(System.Guid processId, string exitMessage){
				return EndProcess(processId, ProcessExitStatus.FAILED, exitMessage);
			}
			static bool EndProcess(System.Guid processId, ProcessExitStatus result, string exitMessage){
				Init();
				if (!_runningProcesses.ContainsKey(processId)) return false;
				switch (result){
					case ProcessExitStatus.CANCELLED:
						_cancelledProcesses.Add(processId);
						return true;
					case ProcessExitStatus.FAILED:
						if (_abortedProcesses.ContainsKey(processId)) return false;
						_abortedProcesses.Add(processId, exitMessage);
						return true;
				}
				return false;
			}
			#endregion

			#region Instance
			List<System.Guid> _endedProcesses = new List<System.Guid>();
			
			private AssetPipeProcessManager(){}

			void Update(){
				Cleanup();										// Do cancels and aborts before and after execution, not during

				foreach (var a in _runningProcesses){
					bool running = a.Value.process.MoveNext();	// Execute next step of coroutine
					if (!running){								// If finished, flag for removal
						_endedProcesses.Add(a.Key);
					}
				}

				Cleanup();
			}
			void Cleanup(){
				foreach (var aborted in _abortedProcesses){		// Abort coroutines
					ProcessData process = _runningProcesses[aborted.Key];
					process.OnCancelled(new ProcessDoneData(aborted.Key, aborted.Value, process.stopwatch, ProcessExitStatus.FAILED));
					_runningProcesses.Remove(aborted.Key);
				}
				foreach (var cancelled in _cancelledProcesses){	// Cancel coroutines
					ProcessData process = _runningProcesses[cancelled];
					process.OnCancelled(new ProcessDoneData(cancelled, null, process.stopwatch, ProcessExitStatus.CANCELLED));
					_runningProcesses.Remove(cancelled);
				}
				foreach (var ended in _endedProcesses){			// Remove finished coroutines
					_runningProcesses.Remove(ended);
				}

				_endedProcesses.Clear();
				_cancelledProcesses.Clear();
				_abortedProcesses.Clear();
			}
			#endregion
		}
		#endregion

		#region Helpers
		static bool MatchAsset<T>(AssetMetadata<T> data, AssetMatchCallback<T> match) where T:Object {
			if (!data.isValid) return false;
			if (match == null) return true;
			return match(data.asset, data);
		}
		static bool MatchPrefab(AssetMetadata<GameObject> data, PrefabMatchCallback match){
			if (!data.isValid) return false;
			if (match == null) return true;
			return match(data.asset, data);
		}
		static bool MatchComponent<T>(GameObject prefab, AssetMetadata<GameObject> metadata, ComponentMatchCallback<T> match) where T:Component {
			return MatchComponent<T>(prefab.GetComponent<T>(), metadata, match);
		}
		static bool MatchComponent<T>(T component, AssetMetadata<GameObject> metadata, ComponentMatchCallback<T> match) where T:Component {
			if (!metadata.isValid || component == null) return false;
			if (match == null) return true;
			return match(component, metadata);
		}

		static T[] GetComponents<T>(GameObject prefab, ComponentSearchType componentSearchType){
			bool includeChildren = (componentSearchType != ComponentSearchType.ROOT_ONLY);
			bool includeInactive = (componentSearchType == ComponentSearchType.CHILDREN_ALL);

			T[] ts = null;
			if (includeChildren){
				ts = prefab.GetComponentsInChildren<T>(includeInactive);
			} else {
				ts = prefab.GetComponents<T>();
			}

			return ts;
		}

		static bool DisplayProgressBar(string title, int i, int count, bool cancelable, string infoFormat="{0}/{1}"){
			float progress = ((float)i+1)/((float)count);
			if (cancelable){
				return EditorUtility.DisplayCancelableProgressBar(title, string.Format(infoFormat, i, count), progress);
			}
			EditorUtility.DisplayProgressBar(title, string.Format("{0}/{1}", i, count), progress);
			return false;
		}
		static bool DisplayProgressBar(string title, int i, int count, ProcessType pt, string infoFormat="{0}/{1}"){
			return DisplayProgressBar(title, i, count, pt == ProcessType.BLOCKING_CANCELABLE, infoFormat);
		}
		static void ClearProgressBar(){
			EditorUtility.ClearProgressBar();
		}

		static bool CheckTime(System.Diagnostics.Stopwatch sw, ref double lastTick, double tickTime){
			if (sw.Elapsed.TotalSeconds - lastTick > tickTime){
				lastTick = sw.Elapsed.TotalSeconds;
				return true;
			}
			return false;
		}
		#endregion
		#endregion
	}
}
#endif