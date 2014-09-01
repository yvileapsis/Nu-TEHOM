﻿// Prime - A PRIMitivEs code library.
// Copyright (C) Bryan Edds, 2012-2014.

namespace Prime

[<AutoOpen>]
module EitherModule =

    /// Haskell-style Either type.
    /// TODO: more nice operators definitions.
    type Either<'a, 'b> =
        | Right of 'b
        | Left of 'a
        override this.ToString () =
            match this with
            | Left a -> "Left(" + string a + ")"
            | Right b -> "Right(" + string b + ")"

    /// Monadic bind.
    let inline (>>=) either fn =
        match either with
        | Right value -> Right <| fn value
        | Left _ -> either

    /// Bind that allows indication of failure.
    let inline (>>=?) either fn =
        match either with
        | Right value -> fn value
        | Left _ -> either

    /// Bind that allows handling of failure.
    let inline (>>=??) (either : Either<_, _>) fn : Either<_, _> =
        fn either

module Either =

    /// Monadic return.
    let inline returnM value =
        Right value

    /// Monadic returnFrom.
    /// TODO: ensure this is defined correctly!
    let inline returnFrom value =
        value

    /// Builds an either monad.
    type EitherBuilder () =
        member this.Bind (either, fn) = either >>= fn
        member this.Return value = returnM value
        member this.ReturnFrom value = returnFrom value

    /// The either monad.
    let either = EitherBuilder ()

    /// Queries if the either is a Left value.
    let isLeft either =
        match either with
        | Right _ -> false
        | Left _ -> true
    
    /// Queries if the either is a Right value.
    let isRight either =
        match either with
        | Right _ -> true
        | Left _ -> false

    /// Get the Left value of an either, failing if not available.
    let getLeftValue either =
        match either with
        | Right _ -> failwith "Could not get Left value from a Right value."
        | Left a -> a

    /// Get the Right value of an either, failing if not available.
    let getRightValue either =
        match either with
        | Right b -> b
        | Left _ -> failwith "Could not get Left value from a Right value."

    /// Get only the Left values of a sequence of either.
    let getLeftValues eithers =
        Seq.fold
            (fun lefts either -> match either with Right _ -> lefts | Left left -> left :: lefts)
            eithers

    /// Get only the Right values of a sequence of either.
    let getRightValues eithers =
        Seq.fold
            (fun rights either -> match either with Right _ -> rights | Left right -> right :: rights)
            eithers

    /// Split a sequences of eithers into a pair of left and right value lists.
    let split eithers =
        Seq.fold
            (fun (ls, rs) either ->
                match either with
                | Right r -> (ls, r :: rs)
                | Left l -> (l :: ls, rs))
            ([], [])
            eithers