// your IDE may not be able to find the reference to the compiler service here; should be safe to ignore, since FSI respects 
// relative paths to loaded scripts (Runner.fsx in this case)
#load "../FSIRunner/Runner.fsx"

let r = new FSIRunner.Runner()
r.Watch "." [ "WebPlugin.fsx" ]

