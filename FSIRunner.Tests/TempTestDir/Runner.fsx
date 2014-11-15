#load "../../FSIRunner/Runner.fsx"

let r = new FSIRunner.Runner()
let pluginList = [ "Plugin1.fsx"; "Plugin2.fsx"] 
let watchDirs = ["Dir1";"Dir2"]
r.Watch pluginList watchDirs


