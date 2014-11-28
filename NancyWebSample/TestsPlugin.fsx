#load "Project.fsx"
#r "packages/Fuchu/lib/Fuchu.dll"
#load "Tests/Tests.fs"
#load "Tests/Tests2.fs"
#load "../FSIRunner/TestUtil.fs"
#load "../FSIRunner/Types.fs"

open FSIRunner
open FSIRunner.Types

type RunnerPlugin() = 
    let afterReload : AfterReloadFn = 
        (fun (rs,opts) -> 
        let newTypes = rs.[FSIRunner.StateKeys.NewTypes] :?> System.Type list
        let testTypes = 
            TestUtil.getTests newTypes typedefof<Fuchu.TestsAttribute> |> Seq.map (fun t -> t :?> Fuchu.Test)
        Fuchu.Test.Run testTypes |> ignore)
    
    interface IRunnerPlugin with
        member x.Create(rs : RunnerState) = 
            { BasePluginDefinition with 
                AfterReload = afterReload }
