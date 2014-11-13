#load "Project.fsx"

#r "packages/Fuchu/lib/Fuchu.dll"
#load "Tests.fs"

#load "../FSIRunner/Types.fs"
open FSIRunner.Types

type RunnerPlugin() =
    let beforeReload:BeforeReloadFn = (fun rs -> () )

    let afterReload:AfterReloadFn = (fun rs ->
        let newTypes = rs.[FSIRunner.StateKeys.NewTypes] :?> System.Type list
        Tests.runTests newTypes
    )

    interface IRunnerPlugin with
        member x.Init(rs:RunnerState) = { BeforeReload = beforeReload; AfterReload = afterReload }


