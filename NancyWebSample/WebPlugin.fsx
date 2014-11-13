#load "Project.fsx"

open Nancy.Hosting.Self

#load "../FSIRunner/Types.fs"
open FSIRunner.Types

type RunnerPlugin() =
    let hostKey = "prev_nancy_host"

    let beforeReload:BeforeReloadFn = (fun rs -> 
                let ok, host = rs.TryGetValue hostKey
                if ok then
                    rs.Remove hostKey |> ignore
                    let host = host :?> NancyHost
                    printfn "Stopping previous host" 
                    Main.stopNancy host
            )

    let afterReload:AfterReloadFn = (fun rs ->
                let host = Main.startNancy()
                rs.Add(hostKey, host)
            )

    interface IRunnerPlugin with
        member x.Init(rs:RunnerState) = { BeforeReload = beforeReload; AfterReload = afterReload }

