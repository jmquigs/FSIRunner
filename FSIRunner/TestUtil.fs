module TestUtil

open System.Reflection

// Scans the specified type list for types that are attributed with testAttrType.  Returns the values of anything so-attributed.
// This has only been tested with Fuchu, but could be extended to work with other unit-test frameworks.  It assumes that tests are 
// defined as top level members of some module.
let getTests (types: System.Type list) (testAttrType:System.Type) =
    let scanForTests (t:System.Type) =
        //printfn "searching type: %A" t.FullName
        let members = t.GetMembers() // assume t is a module, and examine its members
        let membersWithAttr = members |> Seq.filter (fun m -> 
                let attrs = m.GetCustomAttributes(true)
                let testsAttr = attrs |> Seq.tryFind (fun a -> a.GetType() = testAttrType) 
                match testsAttr with
                | None -> false
                | Some a -> true
            )
        (t, membersWithAttr)

    let createTests ( (t:System.Type), (methods:seq<MemberInfo>)) = 
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

    let testsFound = Seq.map scanForTests types |> Seq.filter (fun (t,methods) -> Seq.length methods > 0 ) |> List.ofSeq |> List.rev
    //printfn "%A" testsFound
    match testsFound.Length with
    | 0 -> 
        printfn "no tests found!"
        Seq.empty
    | n -> 
        testsFound |> Seq.collect createTests
