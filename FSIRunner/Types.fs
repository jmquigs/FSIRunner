namespace FSIRunner

module Types = 
    // Logging interface.  The runner logs all of its messages to an instance of this type.  
    // The intent is that you can replace that with your own logging system if you want.
    type Logger = 
        abstract Info : string -> unit
        abstract Error : string -> unit
        abstract Warn : string -> unit
        abstract Trace : string -> unit
    
    // The runner state is passed in and out of plugins.  There is one of these objects shared by all running plugins.  Plugins
    // can store whatever they want in this dictionary, which is important, because they are not able to contain state.  This is a dictionary
    // rather than some more complex type, so that it can easily be passed between the "outer" FSI session (which contains the runner)
    // and the "inner" one.  Using a custom type instead would cause a cast exception, because the compiled Types type is not actually
    // the same in the two FSI sessions, even though it "looks" the same from a duck-typing perspective.  
    // If this is a problem for you, you can reference the runner assembly
    // rather than #load-ing its files directly.  This causes the runner types to be defined just once and thus they are sharable, and 
    // enables the use of more complex types both for the RunnerState and PluginDefinition.
    type RunnerState = System.Collections.Generic.Dictionary<string, obj>
    
    // Plugin callback invoked before files are reloaded.  Callbacks are invoked in the order the plugins are specified to the runner.
    // Note that plugins defined earlier in the list may tear down state (for example, a web server), so later plugins cannot use that state.
    type BeforeReloadFn = RunnerState -> unit
    
    // Plugin callback invoked after all files are reload.  Execution order is the same as for BeforeReload.  A similar state condition 
    // holds: if an early plugin sets up state, later plugins can safely use it.
    type AfterReloadFn = RunnerState -> unit
    
    // Structure returned by the init interface to describe a plugin.  
    type PluginDefinition = 
        { BeforeReload : BeforeReloadFn
          AfterReload : AfterReloadFn }
    
    // All plugins should implement this at least once in their definition files.
    type IRunnerPlugin = 
        abstract Init : RunnerState -> PluginDefinition
    
    // Default logger used by the runner, which logs using printfn 
    let private log level s = printfn "%s: %s" level s
    
    let DefaultLogger = 
        { new Logger with
              member x.Info s = log "Runner" s
              member x.Warn s = log "Runner[warn]" s
              member x.Trace s = log "Runner[trace]" s
              member x.Error s = log "Runner[error]" s }
    
    // Used internally by the Runner and watcher
    type WatchInfo = 
        { WatchPath : string
          IncludeSubdirectories : bool }

// Contains the standard state keys that will be available in the runner state
module StateKeys = 
    // This will be set in the state when AfterReload is called.  Its value is a list of the most recent types that were defined
    // by a plugin reload.  One of these types is the plugin itself, but that is usually only of interest to the Runner.  
    // Other types may be interesting to the plugin, such as unit test types.
    let NewTypes = "__newtypes"
