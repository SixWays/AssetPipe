#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Sigtrap.AssetPipe.Data;

namespace Sigtrap.AssetPipe {
	#region Enums
	public enum SceneSaveMode {
		/// <summary>Don't save scenes after processing</summary>
		NONE,
		/// <summary>Ask whether to save each scene after processing</summary>
		PROMPT,
		/// <summary>Save all scenes after processing</summary>
		AUTO
	}
	public enum ComponentSearchType {
		/// <summary>Only find components on root object</summary>
		ROOT_ONLY,
		/// <summary>Find components on root and active children</summary>
		CHILDREN_ACTIVE,
		/// <summary>Find components on root, active children and inactive children</summary>
		CHILDREN_ALL
	}
	public enum ProcessType {
		/// <summary>Blocks editor with progress bar.</summary>
		BLOCKING,
		/// <summary>Blocks editor with progress bar and cancel button.</summary>
		BLOCKING_CANCELABLE,
		/// <summary>Doesn't block editor while processing. Can be cancelled manually with Pipeline.CancelProcess.</summary>
		ASYNC,
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

	#region Match Delegates
	public delegate bool AssetMatcher<T>(AssetMetadata<T> metadata) where T:Object;
	public delegate bool PrefabMatcher(AssetMetadata<GameObject> metadata);
	public delegate bool ComponentMatcher<T>(ComponentData<T> data) where T:Component;
	public delegate bool SceneObjectMatcher(SceneObjectMetadata metadata);
	public delegate bool SceneComponentMatcher<T>(SceneComponentData<T> data) where T:Component;
	#endregion

	#region Process Specifiers
	public abstract class ProcessInfoBase {
		/// <summary></summary>
		public System.Action<ProcessDoneData> onDone;
		/// <summary></summary>
		public ProcessType processType = ProcessType.BLOCKING;
		/// <summary></summary>
		public double tickTime = 0.1f;

		/// <summary></summary>
		public abstract System.Guid StartProcess();
	}
	public abstract class ProcessObjectInfoBase : ProcessInfoBase {
		/// <summary></summary>
		public GameObject[] selection;

		public bool hasSelection {get {return selection != null && selection.Length > 0;}}
	}

	#region Asset Processing
	public sealed class ProcessAssetsInfo<T> : ProcessInfoBase where T:Object {
		/// <summary>Asset filter string. Uses Unity project window search syntax.</summary>
		public string filter;
		/// <summary></summary>
		public System.Action<AssetProcessData<T>> onProcessAsset;
		/// <summary></summary>
		public AssetMatcher<T> match;
		/// <summary></summary>
		public System.Action<List<AssetMetadata<T>>> onResults;
		/// <summary></summary>
		public T[] selection;

		public bool hasSelection {get {return selection != null && selection.Length > 0;}}

		public ProcessAssetsInfo(string filter, System.Action<AssetProcessData<T>> onProcessAsset){
			this.filter = filter;
			this.onProcessAsset = onProcessAsset;
		}

		public override System.Guid StartProcess(){
			return Pipeline.ProcessAssets(this);
		}
	}
	public sealed class ProcessPrefabsInfo<T> : ProcessObjectInfoBase where T:Component {
		/// <summary>Asset filter string. Uses Unity project window search syntax.</summary>
		public string filter = Pipeline.DEFAULT_FILTER;
		/// <summary></summary>
		public System.Action<PrefabProcessData> onProcessPrefab;
		/// <summary></summary>
		public PrefabMatcher matchPrefab;
		/// <summary></summary>
		public ComponentMatcher<T> matchComponent;
		/// <summary></summary>
		public System.Action<List<AssetMetadata<GameObject>>> onResults;
		

		public ProcessPrefabsInfo(System.Action<PrefabProcessData> onProcessPrefab){
			this.onProcessPrefab = onProcessPrefab;
		}

		public override System.Guid StartProcess(){
			return Pipeline.ProcessPrefabs(this);
		}
	}
	public sealed class ProcessComponentsInfo<T> : ProcessObjectInfoBase where T:Component {
		/// <summary>Asset filter string. Uses Unity project window search syntax.</summary>
		public string filter = Pipeline.DEFAULT_FILTER;
		/// <summary></summary>
		public System.Action<ComponentProcessData<T>> onProcessComponent;
		/// <summary></summary>
		public ComponentMatcher<T> matchComponent;
		/// <summary></summary>
		public ComponentSearchType componentSearchType=ComponentSearchType.ROOT_ONLY;
		/// <summary></summary>
		public PrefabMatcher matchPrefab;
		/// <summary></summary>
		public System.Action<Dictionary<AssetMetadata<GameObject>, List<T>>> onResults;

		public ProcessComponentsInfo(System.Action<ComponentProcessData<T>> onProcessComponent){
			this.onProcessComponent = onProcessComponent;
		}

		public override System.Guid StartProcess(){
			return Pipeline.ProcessComponents(this);
		}
	}
	#endregion

	#region Scene Processing
	public struct StringMatcher {
		public enum StringMatchMode {
			/// <summary>Case-insensitive match</summary>
			SIMPLE,
			/// <summary>Case-sensitive match</summary>
			CASED,
			/// <summary>Pattern is a regular expression</summary>
			REGEX
		}
		public StringMatchMode mode;
		public string pattern;
		/// <summary>
		/// If true, entire string must match. Otherwise just use Contains. 
		/// Ignored for REGEX mode.
		/// </summary>
		public bool matchWholeWord;
		
		public bool Match(string input){
			if (string.IsNullOrEmpty(pattern)) return true;

			if (mode == StringMatchMode.REGEX){
				var r = new System.Text.RegularExpressions.Regex(pattern);
				return r.IsMatch(input);
			} else {
				bool useCase = (mode == StringMatchMode.CASED);
				string p = useCase ? pattern : pattern.ToLower();
				string m = useCase ? input : input.ToLower();
				
				if (matchWholeWord){
					return p == m;
				} else {
					return m.Contains(p);
				}
			}
		}
	}

	public abstract class ProcessSceneInfoBase<T> : ProcessObjectInfoBase where T:Component {
		/// <summary></summary>
		public GameObject[] roots;
		/// <summary></summary>
		public SceneSaveMode saveMode;
		/// <summary></summary>
		public string[] scenePaths;
		/// <summary></summary>
		public StringMatcher nameMatch;
		/// <summary></summary>
		public SceneComponentMatcher<T> matchComponent;
		/// <summary></summary>
		public SceneObjectMatcher matchObject;

		public bool hasRoots {get {return roots != null && roots.Length > 0;}}
		public bool hasScenes {get {return scenePaths != null && scenePaths.Length > 0;}}

		public ProcessSceneInfoBase(SceneSaveMode saveMode){
			this.saveMode = saveMode;
		}
	}

	public sealed class ProcessSceneObjectsInfo<T> : ProcessSceneInfoBase<T> where T:Component {
		/// <summary></summary>
		public System.Action<SceneObjectProcessData> onProcessObject;
		/// <summary></summary>
		public System.Action<List<SceneObjectMetadata>> onResults;

		public ProcessSceneObjectsInfo(System.Action<SceneObjectProcessData> onProcessObject, SceneSaveMode saveMode) : base(saveMode){
			this.onProcessObject = onProcessObject;
		}

		public override System.Guid StartProcess(){
			return Pipeline.ProcessSceneObjects(this);
		}
	}

	public sealed class ProcessSceneComponentsInfo<T> : ProcessSceneInfoBase<T> where T:Component {
		/// <summary></summary>
		public ComponentSearchType componentSearchType;
		/// <summary></summary>
		public System.Action<SceneComponentProcessData<T>> onProcessComponent;
		/// <summary></summary>
		public System.Action<Dictionary<SceneObjectMetadata, List<T>>> onResults;

		public ProcessSceneComponentsInfo(System.Action<SceneComponentProcessData<T>> onProcessComponent, SceneSaveMode saveMode) : base(saveMode){
			this.onProcessComponent = onProcessComponent;
		}

		public override System.Guid StartProcess(){
			return Pipeline.ProcessSceneComponents(this);
		}
	}
	#endregion
	#endregion

	public static class Pipeline {
		public const string DEFAULT_FILTER = "t:Prefab";

		#region API
		#region Get Assets
		/// <summary>
		/// Gets assets from the database as UnityEngine.Object.
		/// </summary>
		/// <returns>Selected assets. Null if cancelled.</returns>
		/// <param name="match">Called for each asset to decide whether to include in results.</param>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		public static List<Object> GetAssets(string filter, AssetMatcher<Object> match=null) {
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
			string filter, AssetMatcher<T> match=null
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
			PrefabMatcher match=null, string filter=DEFAULT_FILTER
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
			PrefabMatcher matchPrefab=null, ComponentMatcher<T> matchComponent=null, string filter=DEFAULT_FILTER
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
			PrefabMatcher matchPrefab=null, ComponentMatcher<T> matchComponent=null,
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

		/// <summary>Start asset processing for assets of type T.</summary>
		/// <param name="filter">Asset filter string. Uses Unity project window search syntax.</param>
		/// <param name="onProcessAsset">Called for each matching asset. Implement your process here (including saving!).</param>
		/// <param name="match">Called on each asset to decide whether to process. Null matches all.</param>
		/// <param name="onResults">Callback to receive list of processed assets when done. If null, assets not stored.</param>
		/// <param name="onDone">Callback when processing finished. Bool is TRUE if cancelled.</param>
		/// <typeparam name="T">Search for assets of this type.</typeparam>
		public static System.Guid ProcessAssets<T>(ProcessAssetsInfo<T> info) where T:Object {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(ProcessAssetsAsync<T>(pid, info), info.onDone, pid);
			return pid;
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
		public static System.Guid ProcessPrefabs<T>(ProcessPrefabsInfo<T> info) where T:Component {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(ProcessPrefabsAsync<T>(pid, info), info.onDone, pid);
			return pid;
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
		public static System.Guid ProcessComponents<T>(ProcessComponentsInfo<T> info) where T:Component {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(ProcessComponentsAsync<T>(pid, info), info.onDone, pid);
			return pid;
		}
		#endregion

		#region Process Scene Objects
		public static System.Guid ProcessSceneObjects<T>(ProcessSceneObjectsInfo<T> info) where T:Component {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(ProcessSceneAsync<T>(pid, info), info.onDone, pid);
			return pid;
		}
		public static System.Guid ProcessSceneComponents<T>(ProcessSceneComponentsInfo<T> info) where T:Component {
			var pid = System.Guid.NewGuid();
			AssetPipeProcessManager.StartProcess(ProcessSceneAsync<T>(pid, info), info.onDone, pid);
			return pid;
		}
		#endregion
		#endregion

		#region Internal
		#region Asset Coroutines
		static IEnumerator ProcessAssetsAsync<T>(System.Guid processId, ProcessAssetsInfo<T> info) where T:Object {
			var sw = new System.Diagnostics.Stopwatch();
			double lastTick = 0;
			sw.Start();

			List<AssetMetadata<T>> results = null;
			if (info.onResults != null) results = new List<AssetMetadata<T>>();
			
			var guids = AssetDatabase.FindAssets(info.filter);
			int count = guids.Length;
			if (count == 0){
				Debug.LogWarningFormat("No assets found in database for filter [{0}].", info.filter);
			}
			bool cancelled = false;

			for (int i=0; i<count; ++i){
				AssetMetadata<T> meta = new AssetMetadata<T>(guids[i]);
				T asset = meta.asset;

				if (MatchAsset(meta, info.match)){					// If asset found and meets requirements, process
					if (info.onProcessAsset != null){
						info.onProcessAsset(new AssetProcessData<T>(asset, meta, i, count));
					}
					if (results != null) results.Add(meta);	// Add to results
				}

				if (info.processType == ProcessType.ASYNC){
					if (CheckTime(sw, ref lastTick, info.tickTime)){
						yield return null;						// Wait for next editor update
					}
				} else {
					if (DisplayProgressBar("Processing Assets", i, count, info.processType)){
						ClearProgressBar();
						cancelled = true;
						break;
					}
				}
			}

			if (info.onResults != null) info.onResults(results);
			if (info.onDone != null) info.onDone(
				new ProcessDoneData(
					processId, null, sw,
					cancelled ? ProcessExitStatus.CANCELLED : ProcessExitStatus.SUCCESS					
				) 
			);
			
			if (info.processType != ProcessType.ASYNC){
				ClearProgressBar();
			}
		}

		static IEnumerator ProcessPrefabsAsync<T>(System.Guid processId, ProcessPrefabsInfo<T> info) where T:Component {
			var sw = new System.Diagnostics.Stopwatch();
			double lastTick = 0;
			sw.Start();

			List<AssetMetadata<GameObject>> results = null;
			if (info.onResults != null) results = new List<AssetMetadata<GameObject>>();

			var guids = AssetDatabase.FindAssets(info.filter);
			int count = guids.Length;
			if (count == 0){
				Debug.LogWarningFormat("No assets found in database for filter [{0}].", info.filter);
			}
			bool cancelled = false;

			for (int i=0; i<count; ++i){
				var meta = new AssetMetadata<GameObject>(guids[i]);
				GameObject prefab = meta.asset;

				if (MatchPrefab(meta, info.matchPrefab)){					// Does prefab meet requirements?
					if (MatchComponent(prefab, meta, info.matchComponent)){	// If component found and meets requirements, process
						if (info.onProcessPrefab != null){
							info.onProcessPrefab(new PrefabProcessData(prefab, meta, i, count));
						}
						if (results != null) results.Add(meta);		// Add to results
					}
				}
				
				if (info.processType == ProcessType.ASYNC){
					if (CheckTime(sw, ref lastTick, info.tickTime)){
						yield return null;								// Wait for next editor update
					}
				} else {
					if (DisplayProgressBar("Processing Assets", i, count, info.processType)){
						ClearProgressBar();
						cancelled = true;
						break;
					}
				}
			}
			
			if (info.onResults != null) info.onResults(results);
			if (info.onDone != null) info.onDone(
				new ProcessDoneData(
					processId, null, sw,
					cancelled ? ProcessExitStatus.CANCELLED : ProcessExitStatus.SUCCESS					
				) 
			);
			
			if (info.processType != ProcessType.ASYNC){
				ClearProgressBar();
			}
		}

		static IEnumerator ProcessComponentsAsync<T>(System.Guid processId, ProcessComponentsInfo<T> info) where T:Component {
			var sw = new System.Diagnostics.Stopwatch();
			double lastTick = 0;
			sw.Start();

			Dictionary<AssetMetadata<GameObject>, List<T>> results = null;				// Only store results if callback given
			if (info.onResults != null) results = new Dictionary<AssetMetadata<GameObject>, List<T>>();

			var guids = AssetDatabase.FindAssets(info.filter);
			int count = guids.Length;
			if (count == 0){
				Debug.LogWarningFormat("No assets found in database for filter [{0}].", info.filter);
			}
			bool cancelled = false;

			for (int i=0; i<count; ++i){
				AssetMetadata<GameObject> meta = new AssetMetadata<GameObject>(guids[i]);
				GameObject prefab = meta.asset;
				
				if (MatchPrefab(meta, info.matchPrefab)){					// Does prefab meet requirements?
					T[] ts = GetComponents<T>(prefab, info.componentSearchType);
					foreach (T t in ts){
						if (MatchComponent(t, meta, info.matchComponent)){	// If component meets requirements, process
							if (info.onProcessComponent != null){
								info.onProcessComponent(new ComponentProcessData<T>(t, meta, i, count));
							}

							if (results != null){						// Add to results
								List<T> l = null;
								if (!results.TryGetValue(meta, out l)){
									l = new List<T>();
									results.Add(meta, l);
								}
								l.Add(t);
							}

							if (info.processType == ProcessType.ASYNC){
								if (CheckTime(sw, ref lastTick, info.tickTime)){
									yield return null;					// Wait for next editor update
								}
							} else {
								if (DisplayProgressBar("Processing Components", i, count, info.processType, "Prefab {0}/{1}")){
									ClearProgressBar();
									cancelled = true;
									break;
								}
							}
						}
					}
				}

				if (info.processType == ProcessType.ASYNC){
					if (CheckTime(sw, ref lastTick, info.tickTime)){
						yield return null;								// Wait for next editor update
					}
				} else if (cancelled){
					break;												// Break both loops if cancelled
				} else {
					if (DisplayProgressBar("Processing Components", i, count, info.processType, "Prefab {0}/{1}")){
						ClearProgressBar();
						cancelled = true;
						break;
					}
				}
			}

			if (info.onResults != null) info.onResults(results);
			if (info.onDone != null) info.onDone(
				new ProcessDoneData(
					processId, null, sw,
					cancelled ? ProcessExitStatus.CANCELLED : ProcessExitStatus.SUCCESS					
				) 
			);

			if (info.processType != ProcessType.ASYNC){
				ClearProgressBar();
			}
		}
		#endregion

		#region Scene Coroutines
		class ValRef<T> where T:struct {
			public T value;
		}

		#region Scene Objects
		static IEnumerator ProcessSceneAsync<T>(System.Guid processId, ProcessSceneInfoBase<T> info) where T:Component {
			var sw = new System.Diagnostics.Stopwatch();
			ValRef<double> lastTick = new ValRef<double>();
			sw.Start();

			int sceneCount = info.hasScenes ? info.scenePaths.Length : 1;
			if (info.hasRoots && info.hasScenes){
				AbortProcess(processId, "Cannot have multiple scenes and specified root objects");
			}
			if (info.hasSelection && info.hasScenes){
				AbortProcess(processId, "Cannot have multiple scenes and specified object selection");
			}
			if (info.hasSelection && info.hasRoots){
				AbortProcess(processId, "Cannot have specified root objects and specified object selection");
			}

			#region Casts and Outputs
			var infoObj = info as ProcessSceneObjectsInfo<T>;
			var infoCmp = info as ProcessSceneComponentsInfo<T>;

			List<SceneObjectMetadata> resultsObj = null;
			if (infoObj != null && infoObj.onResults != null){
				resultsObj = new List<SceneObjectMetadata>();
			}
			Dictionary<SceneObjectMetadata, List<T>> resultsCmp = null;
			if (infoCmp != null && infoCmp.onResults != null){
				resultsCmp = new Dictionary<SceneObjectMetadata, List<T>>();
			}
			#endregion

			ValRef<bool> cancelled = new ValRef<bool>();
			ValRef<int> k = new ValRef<int>();
			ValRef<float> progress = new ValRef<float>();

			for (int i=0; i<sceneCount; ++i){
				if (info.hasScenes){
					Scene s = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(info.scenePaths[i]);
					UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(s);
				}

				Scene scene = SceneManager.GetActiveScene();

				if (info.hasSelection){
					#region Selection Mode
					for (int j=0; j<info.selection.Length; ++j){
						var meta = new SceneObjectMetadata(scene, info.selection[j], null);
						var loop = LoopSceneProcess<T>(
							ProcessSceneIterative(
								processId, meta, info, infoObj, infoCmp, SelectionType.OBJECTS, j, 0,
								i, sceneCount, j, info.selection.Length, resultsObj, null
							), info, sw, lastTick, progress, cancelled
						);

						while (loop.MoveNext()){
							yield return null;
						}
						if (cancelled.value) break;
					}
					#endregion
				} else {
					#region Roots Mode
					GameObject[] roots = null;
					if (info.hasRoots){
						roots = info.roots;
					} else {
						roots = scene.GetRootGameObjects();
					}

					for (int j=0; j<roots.Length; ++j){
						if (roots[i] == null){
							Debug.LogWarningFormat("Root object {0} is null - skippng", j);
						}
						
						var meta = new SceneObjectMetadata(scene, roots[j], roots[j]);
						k.value = 0;
						var loop = LoopSceneProcess<T>(
							ProcessSceneRecursive(
								processId, meta, info, infoObj, infoCmp, j, roots.Length, 
								i, sceneCount, !info.hasRoots, k, resultsObj, null
							), info, sw, lastTick, progress, cancelled
						);

						while (loop.MoveNext()){
							yield return null;
						}
						if (cancelled.value) break;
					}
					#endregion
				}

				if (cancelled.value) break;

				switch (info.saveMode){
					case SceneSaveMode.AUTO:
						UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
						break;
					case SceneSaveMode.PROMPT:
						UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
						break;
					case SceneSaveMode.NONE:
						if (info.hasScenes){
							UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
						}
						break;
				}
			}
						
			if (resultsObj != null) infoObj.onResults(resultsObj);
			if (resultsCmp != null) infoCmp.onResults(resultsCmp);
			if (info.onDone != null) info.onDone(
				new ProcessDoneData(
					processId, null, sw,
					cancelled.value ? ProcessExitStatus.CANCELLED : ProcessExitStatus.SUCCESS					
				) 
			);
			
			if (info.processType != ProcessType.ASYNC){
				ClearProgressBar();
			}
		}
		static IEnumerator LoopSceneProcess<T>(
			IEnumerator loop, ProcessSceneInfoBase<T> info, System.Diagnostics.Stopwatch sw,
			ValRef<double> lastTick, ValRef<float> progress, ValRef<bool> cancelled
		) where T:Component {
			while (loop.MoveNext()){
				if (info.processType == ProcessType.ASYNC){
					if (CheckTime(sw, lastTick, info.tickTime)){
						yield return null;								// Wait for next editor update
					}
				} else {
					float p = (float)loop.Current;
					if (p > progress.value) progress.value = p;
					if (DisplayProgressBar("Processing Scene Objects", progress.value, info.processType)){
						ClearProgressBar();
						cancelled.value = true;
						break;
					}
				}
			}
		}
		static IEnumerator ProcessSceneRecursive<T>(
			System.Guid processId, SceneObjectMetadata data, ProcessSceneInfoBase<T> infoBase,
			ProcessSceneObjectsInfo<T> infoAsObj, ProcessSceneComponentsInfo<T> infoAsComp, int currentRoot, 
			int rootCount, int currentScene, int sceneCount, bool autoRoots, ValRef<int> currentObject,
			List<SceneObjectMetadata> outObjResults, Dictionary<SceneObjectMetadata, List<T>> outCmpResults
		) where T:Component {
			SelectionType st = autoRoots ? SelectionType.ROOTS_AUTO : SelectionType.ROOTS_MANUAL;
			var process = ProcessSceneIterative<T>(
				processId, data, infoBase, infoAsObj, infoAsComp, st, currentRoot, rootCount, 
				currentScene, sceneCount, currentObject.value, -1, outObjResults, outCmpResults
			);

			float progress = 0;
			while (process.MoveNext()){
				if (process.Current != null){
					progress = (float)process.Current;
				}
				yield return progress;
			}

			++currentObject.value;
			yield return progress;
			
			foreach (Transform child in data.gameObject.transform){
				var d = new SceneObjectMetadata(data.scene, child.gameObject, data.root);
				var loop = ProcessSceneRecursive(
					processId, d, infoBase, infoAsObj, infoAsComp, currentRoot, rootCount, 
					currentScene, sceneCount, autoRoots, currentObject, outObjResults, outCmpResults
				);
				while (loop.MoveNext()){
					yield return loop.Current;
				}
			}
		}
		static IEnumerator ProcessSceneIterative<T>(
			System.Guid processId, SceneObjectMetadata data, ProcessSceneInfoBase<T> infoBase,
			ProcessSceneObjectsInfo<T> infoAsObj, ProcessSceneComponentsInfo<T> infoAsComp, 
			SelectionType selectionType, int currentRoot, int rootCount,
			int currentScene, int sceneCount, int currentObject, int objectCount,
			List<SceneObjectMetadata> outObjResults, Dictionary<SceneObjectMetadata, List<T>> outCmpResults
		) where T:Component {
			if (infoAsComp == null && infoAsObj == null){
				Pipeline.AbortProcess(processId, "Must provide either ProcessSceneObjectsInfo or ProcessSceneComponentsInfo to ProcessSceneObjectsIterative");
			}
			if (selectionType == SelectionType.OBJECTS && objectCount <= 0){
				Pipeline.AbortProcess(processId, "In object selection mode, must pass total object count to ProcessSceneObjectsIterative");
			}
			float progress = -1;
			if (MatchSceneObject(data, infoBase.matchObject)){
				if (infoAsObj != null){
					#region Object Mode
					if (MatchSceneComponent(data, infoBase.matchComponent)){
						SceneObjectProcessData p;
						if (selectionType != SelectionType.OBJECTS){
							p = new SceneObjectProcessData(
								data.scene, data.gameObject, data.root, currentObject,
								currentRoot, rootCount, currentScene, sceneCount, selectionType
							);
						} else {
							p = new SceneObjectProcessData(
								data.scene, data.gameObject, currentObject, objectCount
							);
						}
						if (infoAsObj.onProcessObject != null){
							infoAsObj.onProcessObject(p);
						}
						progress = p.progressTotal;
						if (outObjResults != null) outObjResults.Add(data);
					}
					#endregion
				} else {
					#region Component Mode
					var ts = GetComponents<T>(data.gameObject, infoAsComp.componentSearchType);
					for (int i=0; i<ts.Length; ++i){
						T t = ts[i];
						if (MatchSceneComponent(data, infoBase.matchComponent)){
							SceneComponentProcessData<T> p;
							if (selectionType != SelectionType.OBJECTS){
								p = new SceneComponentProcessData<T>(
									data.scene, t, data.root, currentObject, i, ts.Length,
									currentRoot, rootCount, currentScene, sceneCount, selectionType
								);
							} else {
								p = new SceneComponentProcessData<T>(
									data.scene, t, currentObject, i, ts.Length, objectCount
								);
							}
							if (infoAsComp.onProcessComponent != null){
								infoAsComp.onProcessComponent(p);
							}
							progress = p.progressTotal;
							if (outCmpResults != null){
								List<T> l = null;
								if (!outCmpResults.TryGetValue(data, out l)){
									l = new List<T>();
									outCmpResults.Add(data, l);
								}
								l.Add(t);
							}
							yield return progress;
						}
					}
					#endregion
				}
			}
		}
		#endregion

		#region Scene Components
		// TODO
		#endregion
		#endregion		

		#region Coroutine implementation
		private class AssetPipeProcessManager {
			struct ProcessData {
				public System.Guid processId {get; private set;}
				public IEnumerator process {get; private set;}
				System.Action<ProcessDoneData> onCancelled;
				System.Diagnostics.Stopwatch _stopwatch;
				public System.Diagnostics.Stopwatch stopwatch {
					get {
						_stopwatch.Stop();
						return _stopwatch;
					}
				}

				public ProcessData(IEnumerator process, System.Action<ProcessDoneData> onCancelled, System.Guid processId){
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

			public static void StartProcess(IEnumerator coroutine, System.Action<ProcessDoneData> onCancelled, System.Guid processId){
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
		static bool MatchAsset<T>(AssetMetadata<T> data, AssetMatcher<T> match) where T:Object {
			if (!data.isValid) return false;
			if (match == null) return true;
			return match(data);
		}
		static bool MatchPrefab(AssetMetadata<GameObject> data, PrefabMatcher match){
			if (!data.isValid) return false;
			if (match == null) return true;
			return match(data);
		}
		static bool MatchComponent<T>(GameObject prefab, AssetMetadata<GameObject> metadata, ComponentMatcher<T> match) where T:Component {
			return MatchComponent(prefab.GetComponent<T>(), metadata, match);
		}
		static bool MatchComponent<T>(T component, AssetMetadata<GameObject> metadata, ComponentMatcher<T> match) where T:Component {
			if (!metadata.isValid || component == null) return false;
			if (match == null) return true;
			return match(new ComponentData<T>(component, metadata));
		}
		static bool MatchSceneObject(SceneObjectMetadata data, SceneObjectMatcher match){
			if (!data.isValid) return false;
			if (match == null) return true;
			return match(data);
		}
		static bool MatchSceneComponent<T>(SceneObjectMetadata metadata, SceneComponentMatcher<T> match) where T:Component{
			return MatchSceneComponent(metadata.gameObject.GetComponent<T>(), metadata, match);
		}
		static bool MatchSceneComponent<T>(T component, SceneObjectMetadata metadata, SceneComponentMatcher<T> match) where T:Component{
			if (!metadata.isValid || component == null) return false;
			if (match == null) return true;
			return match(new SceneComponentData<T>(component, metadata));
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

		#region Blocking Progress Bar
		static bool DisplayProgressBar(string title, string info, bool cancelable, float progress){
			if (cancelable){
				return EditorUtility.DisplayCancelableProgressBar(title, info, progress);
			}
			EditorUtility.DisplayProgressBar(title, info, progress);
			return false;
		}
		static bool DisplayProgressBar(string title, int i, int count, bool cancelable, string infoFormat="{0}/{1}"){
			float progress = ((float)i+1)/((float)count);
			string info = string.Format(infoFormat, i, count);
			return DisplayProgressBar(title, info, cancelable, progress);
		}
		static bool DisplayProgressBar(string title, int i, int count, ProcessType pt, string infoFormat="{0}/{1}"){
			return DisplayProgressBar(title, i, count, pt == ProcessType.BLOCKING_CANCELABLE, infoFormat);
		}
		static bool DisplayProgressBar(string title, float progress, ProcessType pt, string infoFormat="{0}%"){
			return DisplayProgressBar(title, string.Format(infoFormat, Mathf.Floor(progress*100f)), pt == ProcessType.BLOCKING_CANCELABLE, progress);
		}
		static void ClearProgressBar(){
			EditorUtility.ClearProgressBar();
		}
		#endregion

		static bool CheckTime(System.Diagnostics.Stopwatch sw, ref double lastTick, double tickTime){
			if (sw.Elapsed.TotalSeconds - lastTick > tickTime){
				lastTick = sw.Elapsed.TotalSeconds;
				return true;
			}
			return false;
		}
		static bool CheckTime(System.Diagnostics.Stopwatch sw, ValRef<double> lastTick, double tickTime){
			return CheckTime(sw, ref lastTick.value, tickTime);
		}
		#endregion
		#endregion
	}
}

namespace Sigtrap.AssetPipe.Data {
	#region Assets
	public struct ProcessProgress {
		public int index {get; private set;}
		public int assetCount {get; private set;}
		public bool isValid {get {return assetCount > 0;}}
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
	public struct ComponentData<T> where T:Component {
		/// <summary>The component to match.</summary>
		public T component {get; private set;}
		/// <summary>Metadata for the prefab asset this component exists on.</summary>
		public AssetMetadata<GameObject> metadata {get; private set;}

		public ComponentData(T component, AssetMetadata<GameObject> metadata){
			this.component = component;
			this.metadata = metadata;
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
	#endregion

	#region Scene Objects
	public struct SceneObjectMetadata {
		public bool isValid {get {return gameObject != null;}}
		public Scene scene {get; private set;}
		public GameObject gameObject {get; private set;}
		public GameObject root {get; private set;}

		public SceneObjectMetadata(Scene scene, GameObject gameObject, GameObject root){
			this.scene = scene;
			this.gameObject = gameObject;
			this.root = root;
		}
	}
	public enum SelectionType {
		/// <summary>Scene root objects automatically recursed within one or more scenes</summary>
		ROOTS_AUTO,
		/// <summary>User-specified root objects recursed within a single scene</summary>
		ROOTS_MANUAL,
		/// <summary>User-specified objects iterated within a single scene</summary>
		OBJECTS
	}
	public struct SceneObjectProcessData {	
		public SelectionType selectionType {get; private set;}
		public GameObject gameObject {get; private set;}
		public SceneObjectMetadata metadata {get; private set;}
		public int currentObject {get; private set;}
		public ProcessProgress progressObjects {get; private set;}
		public ProcessProgress progressRoots {get; private set;}
		public ProcessProgress progressScenes {get; private set;}
		public float progressTotal {get; private set;}

		private SceneObjectProcessData(
			Scene scene, GameObject gameObject, GameObject root, int currentObject
		) : this(){
			this.gameObject = gameObject;
			metadata = new SceneObjectMetadata(scene, gameObject, root);
			this.currentObject = currentObject;
		}

		/// <summary>
		/// Create a new SceneObjectProcessData using root objects for recursion
		/// </summary>
		public SceneObjectProcessData(
			Scene scene, GameObject gameObject, GameObject root, 
			int currentObject, int currentRoot, int rootCount, 
			int currentScene, int sceneCount, SelectionType selectionType
		) : this(scene, gameObject, root, currentObject){
			if (selectionType == SelectionType.OBJECTS){
				throw new System.Exception("Cannot use OBJECTS mode for recursive process data");
			}
			this.selectionType = selectionType;
			progressRoots = new ProcessProgress(currentRoot, rootCount);
			progressScenes = new ProcessProgress(currentScene, sceneCount);
			progressTotal = 
				(float)(currentRoot + (currentScene * rootCount)) / 
				(float)(rootCount * sceneCount);
		}

		/// <summary>
		/// Create a new SceneObjectProcessData using object selection for iteration
		/// </summary>
		public SceneObjectProcessData(
			Scene scene, GameObject gameObject, int currentObject, int objectCount
		) : this(scene, gameObject, null, currentObject){
			this.selectionType = SelectionType.OBJECTS;
			progressObjects = new ProcessProgress(currentObject, objectCount);
		}
	}
	public struct SceneComponentData<T> where T:Component {
		public T component {get; private set;}
		public SceneObjectMetadata metadata {get; private set;}

		public SceneComponentData(T component, SceneObjectMetadata metadata){
			this.component = component;
			this.metadata = metadata;
		}
	}
	public struct SceneComponentProcessData<T> where T:Component {
		public SelectionType selectionType {get; private set;}
		public T component {get; private set;}
		public SceneObjectMetadata metadata {get; private set;}
		public ProcessProgress progressComponents {get; private set;}
		public int currentObject {get; private set;}
		public ProcessProgress progressObjects {get; private set;}
		public ProcessProgress progressRoots {get; private set;}
		public ProcessProgress progressScenes {get; private set;}
		public float progressTotal {get; private set;}

		private SceneComponentProcessData(
			Scene scene, T component, GameObject root, int currentObject, 
			int currentComponent, int componentCount
		) : this(){
			this.component = component;
			metadata = new SceneObjectMetadata(scene, component.gameObject, root);
			this.currentObject = currentObject;
			progressComponents = new ProcessProgress(currentComponent, componentCount);
		}

		/// <summary>
		/// Create a new SceneObjectProcessData using root objects for recursion
		/// </summary>
		public SceneComponentProcessData(
			Scene scene, T component, GameObject root, int currentObject, 
			int currentComponent, int componentCount, int currentRoot, 
			int rootCount, int currentScene, int sceneCount, SelectionType selectionType
		) : this(scene, component, root, currentObject, currentComponent, componentCount){
			if (selectionType == SelectionType.OBJECTS){
				throw new System.Exception("Cannot use OBJECTS mode for recursive process data");
			}
			this.selectionType = selectionType;
			progressRoots = new ProcessProgress(currentRoot, rootCount);
			progressScenes = new ProcessProgress(currentScene, sceneCount);
			progressTotal = 
				(float)(currentRoot + (currentScene * rootCount)) / 
				(float)(rootCount * sceneCount);
		}

		/// <summary>
		/// Create a new SceneObjectProcessData using object selection for iteration
		/// </summary>
		public SceneComponentProcessData(
			Scene scene, T component, int currentObject,
			int currentComponent, int componentCount, int objectCount
		) : this(scene, component, null, currentObject, currentComponent, componentCount){
			this.selectionType = SelectionType.OBJECTS;
			progressObjects = new ProcessProgress(currentObject, objectCount);
			progressTotal = 
				(float)(currentComponent + (currentObject * componentCount)) / 
				(float)(componentCount * componentCount);
		}
	}
	#endregion

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
}
#endif