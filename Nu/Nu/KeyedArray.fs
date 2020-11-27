﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open Prime
open System.Collections.Generic

[<RequireQualifiedAccess>]
module KeyedArray =

    /// A garbage-collected keyed array.
    /// TODO: once this is well-tested, let's consider moving into Prime.
    type [<NoEquality; NoComparison>] KeyedArray<'k, 'v when 'k : comparison> =
        private
            { Keys_ : SortedDictionary<int, 'k> // sorted so that compacting does not change order
              Indices_ : Dictionary<'k, int>
              mutable Values_ : struct (bool * 'k * 'v) array
              mutable Current_ : int
              mutable Removed_ : single // single to elide unecessary conversions
              mutable Threshold_ : single }

        interface struct (bool * 'k * 'v) IEnumerable with
            member this.GetEnumerator () = this.Values_.GetEnumerator ()
            member this.GetEnumerator () = (this.Values_ :> _ IEnumerable).GetEnumerator ()

        /// The keyed array values.
        member this.Values =
            this.Values_

        /// The threshold for compaction.
        member this.Threshold =
            this.Threshold_

        /// The current number of values (some of which may be empty).
        member this.Length =
            this.Current_

        /// Index a keyed value.
        member this.Item key =
            &this.Values_.[this.Indices_.[key]]

    let private overflowing karr =
        karr.Current_ = karr.Values.Length

    let private underflowing karr =
        karr.Removed_ / single karr.Keys_.Count > karr.Threshold_

    let private expand karr =

        // check overflow
        if overflowing karr then

            // grow
            let values = Array.zeroCreate (karr.Current_ * int (1.0f / karr.Threshold_))
            Array.blit karr.Values_ 0 values 0 karr.Current_
            karr.Values_ <- values

    let private compact karr =
        
        // check underflow
        if underflowing karr then

            // reorg
            let mutable current = 0
            for kvp in karr.Keys_ do
                let key = kvp.Key
                let index = karr.Indices_.[key]
                let value = karr.Values_.[index]
                karr.Keys_.[current] <- key
                karr.Indices_.[key] <- current
                karr.Values_.[current] <- value
                current <- inc current

            // shrink
            let values = Array.zeroCreate (karr.Current_ * int karr.Threshold_)
            Array.blit karr.Values_ 0 values 0 values.Length
            karr.Values_ <- values

            // clean up
            karr.Current_ <- current
            karr.Removed_ <- 0.0f

    /// Get the keyed array values.
    let getValues karr =
        karr.Values_

    /// Get the threshold for compaction.
    let getThreshold karr =
        karr.Threshold_
    
    /// Get the current number of values (some of which may be empty).
    let getLength karr =
        karr.Current_

    /// Add a keyed value, or update one if it already exists.
    let add key value karr =
        match karr.Indices_.TryGetValue key with
        | (false, _) ->
            expand karr
            let index = karr.Current_
            karr.Keys_.Add (index, key) |> ignore
            karr.Indices_.Add (key, index)
            karr.Values_.[index] <- struct (true, key, value)
            karr.Current_ <- inc karr.Current_
        | (true, index) ->
            karr.Values_.[index] <- struct (true, key, value)

    /// Remove a keyed value if it exists.
    let remove key karr =
        match karr.Indices_.TryGetValue key with
        | (true, index) ->
            karr.Keys_.Remove index |> ignore
            karr.Indices_.Remove key |> ignore
            karr.Values_.[index] <- (false, key, Unchecked.defaultof<'v>)
            karr.Removed_ <- inc karr.Removed_
            compact karr
        | (false, _) -> ()

    /// Query that a keyed value exists.
    let containsKey key karr =
        karr.Indices_.ContainsKey key

    /// Attempt to find a keyed value.
    let tryFind key karr =
        match karr.Indices_.TryGetValue key with
        | (true, index) -> Some karr.Values_.[index]
        | (false, _) -> None

    /// Find a keyed value.
    let find key (karr : KeyedArray<_, _>) =
        &karr.[key]

    /// Make a KeyedArray with the given compaction threshold and initial capaticy.
    let make<'k, 'v when 'k : comparison> threshold capacity =
        { Keys_ = SortedDictionary<int, 'k> ()
          Indices_ = Dictionary<'k, int> ()
          Values_ = Array.zeroCreate capacity
          Current_ = 0
          Removed_ = 0.0f
          Threshold_ = threshold }

/// A garbage-collected keyed array.
/// TODO: once this is well-tested, let's consider moving into Prime.
type KeyedArray<'k, 'v when 'k : comparison> = KeyedArray.KeyedArray<'k, 'v>