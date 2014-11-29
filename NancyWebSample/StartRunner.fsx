// your IDE may not be able to find the reference to the compiler service here; should be safe to ignore, since FSI respects 
// relative paths to loaded scripts (Runner.fsx in this case)
#load "../FSIRunner/Runner.fsx"

let r = new FSIRunner.Runner()

open FSIRunner.Types

// Use the genProjectPlugin to re-export the Project.fsx file from the fsproj file.  This is optional (as with all plugins)
// But can eliminate some of the manual work required to keep Project.fsx up-to-date.
let genProjectPlugin = 
    { BasePluginConfiguration with 
        ScriptPath = "../FSIRunner/GenProjectPlugin.fsx" 
        WatchExtensions = [".fsproj"]
        Options = Map.ofSeq 
            [ "ProjectFile", box "NancyWebSample.fsproj"; 
              "ExcludedFilePaths", box [ "Tests" ] ]
    }

let webPlugin = { BasePluginConfiguration with ScriptPath = "WebPlugin.fsx" }

let testsPlugin = 
    { BasePluginConfiguration with 
        WatchDir = "./Tests" // TODO: include "../FSIRunner" for TestUtil.fs
        ScriptPath = "TestsPlugin.fsx" 
    }

r.Watch [ genProjectPlugin; webPlugin; testsPlugin ]

// The watcher can't reload itself; however, including FSIRunner in the list of watch directories allows iterative work on TestUtil.fs,
// which is loaded by TestsPlugin and therefore can be reloaded on changes.
//r.Watch([ "WebPlugin.fsx"; "TestsPlugin.fsx" ], [ "../FSIRunner"; "."; "./Tests" ])

