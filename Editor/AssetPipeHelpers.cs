#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Sigtrap.AssetPipe.Data;

namespace Sigtrap.AssetPipe.Helpers {
	#region Progress Bars
	public abstract class ProgressDrawerBase {
		const string DEFAULT_LABEL_PROGRESS = "Asset Pipe Processing";

		public bool allowCancel;
		public bool canCancel {get {return allowCancel && processId != System.Guid.Empty;}}
		public System.Guid processId {get; private set;}
		protected ProcessProgress _progress {get; private set;}
		protected bool _enabled {get; private set;}
		string __label = DEFAULT_LABEL_PROGRESS;
		protected string _label {get {return string.IsNullOrEmpty(__label) ? DEFAULT_LABEL_PROGRESS : __label;}}

		public ProgressDrawerBase(string label, bool allowCancel){
			this.__label = label;
			this.allowCancel = allowCancel;
		}

		#region Lifecycle
		public virtual void OnProcessStart(System.Guid processId){
			this.processId = processId;
			_enabled = true;
		}
		public virtual void OnProcessDoneCallback(ProcessDoneData data){
			this.processId = System.Guid.Empty;
			_enabled = false;
		}
		protected void Cancel(){
			Pipeline.CancelProcess(processId);
		}
		#endregion
		
		#region Process Callbacks
		public virtual void OnUpdateProgressCallback<T>(AssetProcessData<T> data) where T:Object {
			UpdateProgress(data.progress);
		}
		public virtual void OnUpdateProgressCallback(PrefabProcessData data){
			UpdateProgress(data.progress);
		}
		public virtual void OnUpdateProgressCallback<T>(ComponentProcessData<T> data) where T:Component {
			UpdateProgress(data.progress);
		}
		protected virtual void UpdateProgress(ProcessProgress p){
			_progress = p;
		}
		#endregion
	}

	public class ModalProgressBar : ProgressDrawerBase {
		public ModalProgressBar(string label, bool allowCancel) : base(label, allowCancel) {}

		protected override void UpdateProgress(ProcessProgress p){
			base.UpdateProgress(p);
			if (!_enabled) return;
			if (canCancel){
				if (EditorUtility.DisplayCancelableProgressBar(_label, string.Format("{0}/{1}", _progress.index, _progress.assetCount), _progress.progress)){
					Cancel();
				}
			} else {
				EditorUtility.DisplayProgressBar(_label, string.Format("{0}/{1}", _progress.index, _progress.assetCount), _progress.progress);
			}
		}

		public override void OnProcessDoneCallback(ProcessDoneData data){
			EditorUtility.ClearProgressBar();
		}
	}

	public class EditorGUIProgressBarDrawer : ProgressDrawerBase {
		public EditorGUIProgressBarDrawer(string label, bool allowCancel) : base(label, allowCancel) {}

		public void Draw(){
			if (!_enabled) return;
			EditorGUILayout.BeginHorizontal();
			if (canCancel && GUILayout.Button("CANCEL")){
				Cancel();
			}
			Rect r = EditorGUILayout.GetControlRect();
			EditorGUI.ProgressBar(r, _progress.progress, _label);
			EditorGUILayout.EndHorizontal();
		}
	}
	#endregion

	#region GUI blocker
	public class EditorGUIBlocker {
		bool _enabled = true;
		bool _guiWasEnabled;
		public void OnGUIStart(){
			_guiWasEnabled = GUI.enabled;
			GUI.enabled = _enabled;
		}
		public void OnGUIEnd(){
			GUI.enabled = _guiWasEnabled;
		}
		public void OnProcessStart(){
			_enabled = false;
		}
		public void OnProcessDoneCallback(ProcessDoneData data){
			_enabled = true;
		}
	}
	#endregion
}
#endif