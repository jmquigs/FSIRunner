namespace FSIRunner

open FSIRunner
open FSIRunner.Types

open System.Reflection

module XTypeScan =
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
