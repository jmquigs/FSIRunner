#r "../packages/FSharp.Compiler.Service.0.0.73/lib/net45/FSharp.Compiler.Service.dll"

#load "Types.fs"
#load "Watcher.fs"
#load "Runner.fs"
#load "TypeScan.fs"

FSIRunner.RunnerConfig.SourceDirectory <- __SOURCE_DIRECTORY__
