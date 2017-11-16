namespace ProtoBuf.FSharpHelpers.Unit

open Expecto
open FsCheck
open System.IO
open ProtoBuf.FSharpHelpers
open ProtoBuf
open ProtoBuf.Meta
open Expecto.Expect
open System
open System.Collections.Generic
open System.Linq

[<TestName("Standard record with a simple C# list")>]
type TestRecordOne = {
    One: string
    Two: int
    Three: string[]
}

module TestRecordRoundtrip = 

    type DataGenerator =
        static member Generate() : Arbitrary<string[]> = 
            Gen.oneof ([ "One"; "Two"; "" ] |> List.map Gen.constant) 
            |> Gen.listOf
            |> Gen.map List.toArray
            |> Arb.fromGen
            
        static member GenerateNonNullString() : Arbitrary<string> = Arb.Default.StringWithoutNullChars().Generator |> Gen.map (fun x -> x.Get) |> Arb.fromGen

    let propertyToTest<'t when 't : equality> cleanupFunc (typeToTest: 't)  = 
        let model = 
            RuntimeTypeModel.Create() 
           // |> FSharpCollectionRegistration.registerListIntoModel<string>
            |> ProtobufNetSerialiser.registerRecordIntoModel<'t>

        model.CompileInPlace()
        let cloned = model.DeepClone(typeToTest)
        equal (unbox cloned |> cleanupFunc) (cleanupFunc typeToTest) "Protobuf deep clone"
        use ms = new MemoryStream()
        ProtobufNetSerialiser.serialise model ms typeToTest
        ms.Seek(0L, SeekOrigin.Begin) |> ignore
        let rtData = ProtobufNetSerialiser.deserialise<'t> model ms
        equal (cleanupFunc rtData) (cleanupFunc typeToTest) "Type not equal"
    
    let config = { Config.QuickThrowOnFailure with Arbitrary = [ typeof<DataGenerator> ] }

    let emptySingletonListForEqualityComparison = new List<string>() :> IList<string>

    let cleanupNullForLists (t: TestRecordOne) = { t with Three = match t.Three with | null -> [||] | _ -> t.Three }

    let buildTest<'t when 't : equality> cleanupNullForLists = 
        let testNameAttribute = typeof<'t>.GetCustomAttributes(typeof<TestNameAttribute>, true) |> Seq.head :?> TestNameAttribute
        testCase testNameAttribute.Name <| fun () -> Check.One(config, (propertyToTest<'t> cleanupNullForLists))

    [<Tests>]
    let test() = 
        testList 
            "Record Test Cases" 
            [ buildTest<TestRecordOne> cleanupNullForLists ]