namespace XFSIRunner

open XFSIRunner
open XFSIRunner.RunnerTypes

module XTypeScan =
    open RunnerTypes

    open System.Reflection

    let typesDefinedInSession = new System.Collections.Generic.HashSet<System.Type>()

    let scan(compiled) = 
        let asm = if compiled then Assembly.GetCallingAssembly() else Assembly.GetExecutingAssembly()

        let types = asm.GetTypes() 
        let types = types |> Seq.filter (fun t -> 
            let inSession = typesDefinedInSession.Contains(t)
            if not inSession then
                typesDefinedInSession.Add(t) |> ignore
            not inSession
            )

        let lst = List.ofSeq types
        lst
