module TestUtil

open System.Reflection

let getTests types testAttrType =
    let scanForTests (t:System.Type) =
        //printfn "searching type: %A" t.FullName
        let methods = t.GetMembers()
        let methodsWithAttr = methods |> Seq.filter (fun m -> 
                let attrs = m.GetCustomAttributes(true)
                let testsAttr = attrs |> Seq.tryFind (fun a -> a.GetType() = testAttrType) 
                match testsAttr with
                | None -> false
                | Some a -> true
            )
        (t, methodsWithAttr)

    let testsR = Seq.map scanForTests types |> Seq.filter (fun (t,methods) -> Seq.length methods > 0 ) |> List.ofSeq 
    match testsR with 
    | [] -> 
        printfn "no tests found!"

        Seq.empty
    | x::rest -> 
        let t, methods = x
        let tests = methods |> Seq.choose (fun m -> 
            match m.MemberType with
            | MemberTypes.Property ->
                let prop = t.GetProperty(m.Name)
                let pval = prop.GetValue(null, null)
                Some pval
            | MemberTypes.Method ->
                let flags = BindingFlags.DeclaredOnly ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance  ||| BindingFlags.InvokeMethod
                let mval = t.InvokeMember(m.Name, flags, null, null, null)
                Some mval
            | _ -> 
                printfn "getTests: Unsupported member type: %A" m.MemberType
                None
        )
        tests


