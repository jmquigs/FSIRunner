#load "Runner.fsx"

let r = new FSIRunner.Runner()
r.Watch ["."] [ "TestPlugin.fsx" ]

