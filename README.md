# AssetPipe
#### A flexible framework for batch-processing assets in Unity

AssetPipe is a lightweight unified framework designed to facilitate arbitrary batch-processing of assets in a Unity project from the editor. Users write delegates to query assets, process them, receive results and handle cancellations and errors. 

AssetPipe allows the resulting process to be run either in blocking or asynchronous mode, and allows GUI-driven and programmatic cancellation in either case, so you'll never be staring at an editor locked for god-knows-how-long again.

Disclaimers - AssetPipe is pre-pre-alpha. API is in flux. It has *not* yet been heavily tested. It does *not* have exception handling built-in (yet?).

#### Example GUI built around AssetPipe
![Example GUI built around AssetPipe](https://i.imgur.com/orZXsJ1.png)

## How it works
The core of the framework is a set of static methods - `ProcessAssets()`, `ProcessPrefabs()` and `ProcessComponents()`. While all support totally arbitrary processing they're designed to do some of the heavy lifting depending on use case.

Each one searches all assets in AssetDatabase, finds matching assets depending on specified constraints, and calls back into user code on each match to allow processing.

AssetPipe provides a rich API with all the data you could need to process assets. Because of this it may also be a bit overwhelming to begin with! Note that the API is also designed to facilitate LINQ-like lambdas - most callbacks pass a single struct so you don't have to remember the argument list every time.

### Example
```C#
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Sigtrap.AssetPipe;

public class BatchProcessorExample {
  System.Guid processId;

  public void Go(){
	// Create a ProcessAssetsInfo instance to specify processing
	// Constructor takes the minimum required arguments
	ProcessAssetsInfo<Material> p = new ProcessAssetsInfo<Material>(
		"my search filter", 			// Filter string for searching AssetDatabase
		ProcessAsset					// Each matching asset is passed to this callback for processing
	);
	p.match = MatchAsset;				// Assets passed into this callback - return true for matching assets (optional)
	p.onResults = ResultsCallback;		// After process completes, results passed to this callback (optional)
	p.onDone = OnDone;					// Called when process ends whether success, cancel or failure (optional)
	p.processType = ProcessType.ASYNC;	// Should process be ASYNC, BLOCKING or BLOCKING_CANCELABLE? (defaults to BLOCKING)
	p.tickTime = 0.1f;					// Max time per tick for ASYNC processes before yielding
	
	// Any and all callbacks can be lambdas.
	// All callbacks pass a single object for simple lambda declaration.
	
	processId = StartProcess();			// Start processing. Returns GUID for Pipeline.CancelProcess(guid)
  }
  
  #region Process Callbacks
  void ProcessAsset(AssetProcessData<Object> data){
    // Data contains the asset, database metadata, and global process data (e.g. percent complete)
    // This method could also call back into GUI code to update a progress bar (helpers provided for this)
  }
  bool MatchAsset(AssetMetadata<Object> data){
    // Passes almost all the same data as AssetProcessData
    // Perform arbitrary tests on asset here to see whether you want it processed
    return true;
  }
  void ResultsCallback(List<AssetMetadata<T>> results){
    // Final post-processing? Debug message noting how many assets were actually processed?
  }
  void OnDone(ProcessDoneData data){
    // Data gives exit status (SUCCESS, CANCELLED, FAILED), allowing user to clean up if necessary
    // Also gives optional error message provided upon failure, and total process time
  }
  #endregion
  
  public void CancelProcess(){
    // Safely cancel process at any time, including within callbacks
    Pipeline.CancelProcess(processId);  
  }
  public void AbortProcess(string errorMessage){
    // Safely abort process with error message at any time, including within callbacks
    Pipeline.AbortProcess(processId, errorMessage);
  }
}
```

All `Process` methods provide generic overloads to further specify the types of assets to process, and `ProcessPrefabs` and `ProcessComponents` treat the filter string as optional.

## GetAssets, GetPrefabs and GetComponents
Another set of methods - `GetAssets()`, `GetPrefabs()` and `GetComponents()` work along similar lines but run synchronously and simply return a collection of assets with no processing. All draw an editor progress bar with a cancel button.