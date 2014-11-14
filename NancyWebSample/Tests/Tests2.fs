module Tests2

open Fuchu 
open System

// This is here just to show that a project can contain more than one file that defines tests.
[<Tests>]
let tests =
    testCase "hopefullyWontFail"  <| 
        fun _ -> 
            Assert.Equal("3+3", 6, 3+3)
