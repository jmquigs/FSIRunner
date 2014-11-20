// This is an alternate way to run the FSIRunner tests, by using FSI instead of the compiled assembly.
// Note, this is currently the only way to run the tests on windows.
#load "../FSIRunner/Runner.fsx"
#r "./packages/Fuchu/lib/Fuchu.dll"
#load "../FSIRunner/TestUtil.fs"
#load "TestTestUtil.fs"
#load "TestTypeScan.fs"
#load "TestRunner.fs"

// Fuchu doesn't expose an interface for running tests defined with attributes in an FSI session assembly.  Must use the
// FSIRunner typescan & test helpers to do this; therefore keeping those operational is a prerequisite to running any tests :)
let types = FSIRunner.XTypeScan.scan (true)
let testTypes = FSIRunner.TestUtil.getTests types typedefof<Fuchu.TestsAttribute> |> Seq.map (fun t -> t :?> Fuchu.Test)

FSIRunner.XTypeScan.forgetDefinedTypes()
Fuchu.Test.Run testTypes |> ignore
