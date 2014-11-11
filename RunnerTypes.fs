﻿namespace XFSIRunner

module RunnerTypes =
    type Logger =
        abstract member Info: string -> unit
        abstract member Error: string -> unit
        abstract member Warn: string -> unit
        abstract member Trace: string-> unit

    type RunnerState = System.Collections.Generic.Dictionary<string,obj>

    type BeforeReloadFn = RunnerState -> unit
    type AfterReloadFn = RunnerState -> unit

    type InitResult = {BeforeReload: BeforeReloadFn; AfterReload: AfterReloadFn}

    type IRunnerPlugin =
        abstract member Init: unit -> BeforeReloadFn * AfterReloadFn

    let private log level s = printfn "%s: %s" level s
    let DefaultLogger = { new Logger with 
        member x.Info s = log "info" s
        member x.Warn s = log "warn" s
        member x.Trace s = log "trace" s
        member x.Error s = log "error" s
    }
