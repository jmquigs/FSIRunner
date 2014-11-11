#load "FSIRunner.fsx"

let r = new XFSIRunner.Runner()
r.Watch "." [ "TestPlugin.fsx" ]

