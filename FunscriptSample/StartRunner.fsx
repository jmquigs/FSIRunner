// your IDE may not be able to find the reference to the compiler service here; should be safe to ignore, since FSI respects 
// relative paths to loaded scripts (Runner.fsx in this case)
#load "../FSIRunner/Runner.fsx"

let r = new FSIRunner.Runner()

open FSIRunner.Types

let genProjectPlugin = 
    { BasePluginConfiguration with 
        ScriptPath = "../FSIRunner/GenProjectPlugin.fsx" 
        WatchExtensions = [".fsproj"]
        Options = Map.ofSeq 
            [ "ProjectFile", box "FunscriptSample.fsproj";
              "ReferenceOrder", box [ "FunScript.Interop" ] 
            ]
    }

let funscriptCombined = 
    { BasePluginConfiguration with 
        ScriptPath = "FunscriptPlugins.fsx" 
    }

r.Watch [ genProjectPlugin; funscriptCombined ];