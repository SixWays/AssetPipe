#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Sigtrap.AssetPipe.Examples {
	public class APE_ListScripts : EditorWindow {
		[MenuItem("AssetPipe/Examples/ListScripts")]
		public static void Launch(){
			EditorWindow.GetWindow(typeof(APE_ListScripts)).Show();
		}

		Helpers.EditorGUIProgressBarDrawer _guiProgress = new Helpers.EditorGUIProgressBarDrawer("Listing Scripts", false);
		Helpers.EditorGUIBlocker _blocker = new Helpers.EditorGUIBlocker();
		string _scriptNameContains;
		string _abortOnScript;

		bool __allowCancel;
		bool _allowCancel {
			get {return __allowCancel;}
			set {
				__allowCancel = value;
				_guiProgress.allowCancel = value;
			}
		}
		ProcessType _processType;
		float _tickTime = 0.01f;

		void OnGUI(){
			_blocker.OnGUIStart();

			if (GUILayout.Button("Find Scripts")){
				foreach (var a in Pipeline.GetAssets("t:Script")){
					Debug.Log("Script: "+a.name);
				}
			}

			EditorGUILayout.LabelField("Process Settings");
			_scriptNameContains = EditorGUILayout.TextField("Script name contains: ", _scriptNameContains);
			_abortOnScript = EditorGUILayout.TextField("Abort on this script: ", _abortOnScript);
			_processType = (ProcessType)EditorGUILayout.EnumPopup("Process type:", _processType);
			if (_processType == ProcessType.ASYNC){
				_allowCancel = EditorGUILayout.Toggle("Allow async cancel?", _allowCancel);
				_tickTime = EditorGUILayout.Slider("Max time per operation:", _tickTime, 0.0001f, 0.1f);
			}

			if (GUILayout.Button("Process Scripts (dummy)")){
				StartProcess();
			}

			_blocker.OnGUIEnd();

			_guiProgress.Draw();
		}

		System.Guid _currentProcessId;
		void StartProcess(){
			_currentProcessId = Pipeline.ProcessAssets(
				"t:Script", ProcessAsset,
				// Asset matching lambda
				(asset, metadata)=>{
					return string.IsNullOrEmpty(_scriptNameContains) || asset.name.Contains(_scriptNameContains);
				},
				// Results callback
				(results)=>{
					Debug.LogFormat("Processed {0} assets.", results.Count);
				},
				// On Done callback lambda
				(data)=>{
					_blocker.OnProcessDoneCallback(data);
					_guiProgress.OnProcessDoneCallback(data);
					Repaint();
					switch (data.exitStatus){
						case ProcessExitStatus.SUCCESS:
							Debug.Log(data.ToString());
							break;
						case ProcessExitStatus.CANCELLED:
							Debug.LogWarning(data.ToString());
							break;
						case ProcessExitStatus.FAILED:
							Debug.LogError(data.ToString());
							break;
					}
				}, 
				_processType, _tickTime
			);

			_guiProgress.OnProcessStart(_currentProcessId);	// Hand process GUID to progress bar to allow cancelling
			_blocker.OnProcessStart();
		}

		void ProcessAsset(AssetProcessData<Object> data){
			if (!string.IsNullOrEmpty(_abortOnScript) && data.asset.name.Contains(_abortOnScript)){
				Pipeline.AbortProcess(_currentProcessId, "Aborted: script matches "+_abortOnScript);
			} else {
				Debug.LogFormat("Found script {0}/{1}: {2}", data.progress.index, data.progress.assetCount, data.metadata.path);
				_guiProgress.OnUpdateProgressCallback(data);
			}
			Repaint();
		}
	}
}
#endif