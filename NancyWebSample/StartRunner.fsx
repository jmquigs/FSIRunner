// your IDE may not be able to find the reference to the compiler service here; should be safe to ignore, since FSI respects 
// relative paths to loaded scripts (Runner.fsx in this case)
#load "../FSIRunner/Runner.fsx"

let r = new FSIRunner.Runner()
// The watcher can't reload itself; however, including FSIRunner in the list of watch directories allows iterative work on TestUtil.fs,
// which is loaded by TestsPlugin and therefore can be reloaded on changes.
r.Watch ["."; "../FSIRunner"] [ "WebPlugin.fsx"; "TestsPlugin.fsx" ]

