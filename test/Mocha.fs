module Mocha 

open Fable.Core.Testing
open Fable.Core 

type TestCase = 
    | SyncTest of string * (unit -> unit) 
    | AsyncTest of string * (unit -> Async<unit>)

type TestModule = TestModule of string * TestCase list

let areEqual expected actual : unit =
    if expected = actual 
    then Assert.AreEqual(expected, actual)
    else Assert.AreEqual(expected, actual, sprintf "Expected %A but got %A" expected actual)

let isTrue cond = areEqual true cond 
let isFalse cond = areEqual false cond 
let isZero number = areEqual 0 number 
let failTest msg = Assert.AreEqual(true, false, msg)
    
let testCase name body = SyncTest(name, body)
let testCaseAsync name body = AsyncTest(name, body)
let testList name tests = TestModule(name, tests)
let pass() = areEqual true true
let [<Global>] private describe (name: string) (f: unit->unit) = jsNative
let [<Global>] private it (msg: string) (f: unit->unit) = jsNative

let [<Emit("it($0, $1)")>] private itAsync msg (f: (unit -> unit) -> unit) = jsNative 
 
let runModules modules = 
    for TestModule(name, testCases) in modules do 
        describe name <| fun () ->
            testCases
            |> List.iter (function 
                | SyncTest(msg, test) -> it msg test
                | AsyncTest(msg, test) -> 
                    itAsync msg (fun finished -> 
                        async {
                            match! Async.Catch(test()) with 
                            | Choice1Of2 () -> do finished()
                            | Choice2Of2 err -> do finished(unbox err)
                        } |> Async.StartImmediate)) 