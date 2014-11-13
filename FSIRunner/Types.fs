namespace FSIRunner

module Types =
    type Logger =
        abstract member Info: string -> unit
        abstract member Error: string -> unit
        abstract member Warn: string -> unit
        abstract member Trace: string-> unit

    type RunnerState = System.Collections.Generic.Dictionary<string,obj>

    type BeforeReloadFn = RunnerState -> unit
    type AfterReloadFn = RunnerState -> unit

    type PluginDefinition = {BeforeReload: BeforeReloadFn; AfterReload: AfterReloadFn}

    type IRunnerPlugin =
        abstract member Init: RunnerState -> PluginDefinition

    let private log level s = printfn "%s: %s" level s
    let DefaultLogger = { new Logger with 
        member x.Info s = log "Runner" s
        member x.Warn s = log "Runner[warn]" s
        member x.Trace s = log "Runner[trace]" s
        member x.Error s = log "Runner[error]" s
    }

module StateKeys =
    let NewTypes = "__newtypes"
