//#r "../packages/FSharp.Compiler.Service.0.0.73/lib/net45/FSharp.Compiler.Service.dll"
//#r "bin/Debug/FSIRunner.dll"

#load "Types.fs"
open FSIRunner.Types

type RunnerPlugin() =
    let beforeReload:BeforeReloadFn = (fun rs -> printfn "before reload!" )

    let afterReload:AfterReloadFn = (fun rs ->
        printfn "after reload!"
    )

    member x.InitPlugin (rs:RunnerState) = 
        { BeforeReload = beforeReload; AfterReload = afterReload }

    interface IRunnerPlugin with
        member x.Init() = beforeReload,afterReload
