#load "Project.fsx"

#r "packages/Fuchu/lib/Fuchu.dll"
#load "Tests/Tests.fs"
#load "Tests/Tests2.fs"

#load "../FSIRunner/TestUtil.fs"
#load "../FSIRunner/Types.fs"
open FSIRunner
open FSIRunner.Types

type RunnerPlugin() =
    let beforeReload:BeforeReloadFn = (fun rs -> () )

    let afterReload:AfterReloadFn = (fun rs ->
        let newTypes = rs.[FSIRunner.StateKeys.NewTypes] :?> System.Type list
        let testTypes = TestUtil.getTests newTypes typedefof<Fuchu.TestsAttribute> |> Seq.map (fun t -> t :?> Fuchu.Test)

        Fuchu.Test.Run testTypes |> ignore
    )

    interface IRunnerPlugin with
        member x.Init(rs:RunnerState) = { BeforeReload = beforeReload; AfterReload = afterReload }


