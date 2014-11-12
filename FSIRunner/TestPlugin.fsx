//#r "packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"
//#r "bin/Debug/FSIRunner.dll"

#load "Types.fs"
open FSIRunner.Types

type RunnerPlugin() =
    let beforeReload:BeforeReloadFn = (fun rs -> printfn "before reload!" )

    let afterReload:AfterReloadFn = (fun rs ->
        printfn "after reload!"
    )

    interface IRunnerPlugin with
        member x.Init(rs:RunnerState) = { BeforeReload = beforeReload; AfterReload = afterReload }
