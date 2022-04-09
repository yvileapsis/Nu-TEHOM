﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System
open System.ComponentModel
open System.Numerics
open Prime
open Nu

/// Masks for Transform flags.
module TransformMasks =

    // OPTIMIZATION: Transform flag bit-masks for performance.
    let [<Literal>] ActiveMask =                    0b000000000000000000001u
    let [<Literal>] DirtyMask =                     0b000000000000000000010u
    let [<Literal>] InvalidatedMask =               0b000000000000000000100u
    let [<Literal>] OmnipresentMask =               0b000000000000000001000u
    let [<Literal>] AbsoluteMask =                  0b000000000000000010000u
    let [<Literal>] ImperativeMask =                0b000000000000000100000u
    let [<Literal>] PublishChangeBindingsMask =     0b000000000000001000000u
    let [<Literal>] PublishChangeEventsMask =       0b000000000000010000000u
    let [<Literal>] EnabledMask =                   0b000000000000100000000u
    let [<Literal>] VisibleMask =                   0b000000000001000000000u
    let [<Literal>] AlwaysUpdateMask =              0b000000000010000000000u
    let [<Literal>] PublishUpdatesMask =            0b000000000100000000000u
    let [<Literal>] PublishPostUpdatesMask =        0b000000001000000000000u
    let [<Literal>] PersistentMask =                0b000000010000000000000u
    let [<Literal>] IgnorePropertyBindingsMask =    0b000000100000000000000u
    let [<Literal>] MountedMask =                   0b000001000000000000000u
    let [<Literal>] EnabledLocalMask =              0b000010000000000000000u
    let [<Literal>] VisibleLocalMask =              0b000100000000000000000u
    let [<Literal>] RotationMatrixDirtyMask =       0b001000000000000000000u
    let [<Literal>] AffineMatrixDirtyMask =         0b010000000000000000000u
    let [<Literal>] DefaultFlags =                  0b000110010001100100001u

// NOTE: opening this in order to make the Transform property implementations reasonably succinct.
open TransformMasks

/// Carries transformation data specific to an Entity.
type [<NoEquality; NoComparison>] Transform =
    struct
        // cache line 1
        val mutable private Flags_ : uint
        val mutable private Position_ : Vector3
        val mutable private Rotation_ : Quaternion
        // cache line 2
        val mutable private Scale_ : Vector3
        val mutable private Offset_ : Vector3
        val mutable private RotationMatrixOpt_ : Matrix4x4 ref
        val mutable private AffineMatrixOpt_ : Matrix4x4 ref
        // cache line 3
        val mutable private Angles_ : Vector3
        val mutable private Size_ : Vector3
        val mutable private Elevation_ : single
        // 4 cache line bytes remaining
        end

    member inline this.Active with get () = this.Flags_ &&& ActiveMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| ActiveMask else this.Flags_ &&& ~~~ActiveMask
    member inline this.Dirty with get () = this.Flags_ &&& DirtyMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| DirtyMask else this.Flags_ &&& ~~~DirtyMask
    member inline this.Invalidated with get () = this.Flags_ &&& InvalidatedMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| InvalidatedMask else this.Flags_ &&& ~~~InvalidatedMask
    member inline this.Omnipresent with get () = this.Flags_ &&& OmnipresentMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| OmnipresentMask else this.Flags_ &&& ~~~OmnipresentMask
    member inline this.Absolute with get () = this.Flags_ &&& AbsoluteMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| AbsoluteMask else this.Flags_ &&& ~~~AbsoluteMask
    member inline this.Imperative with get () = this.Flags_ &&& ImperativeMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| ImperativeMask else this.Flags_ &&& ~~~ImperativeMask
    member inline this.PublishChangeBindings with get () = this.Flags_ &&& PublishChangeBindingsMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| PublishChangeBindingsMask else this.Flags_ &&& ~~~PublishChangeBindingsMask
    member inline this.PublishChangeEvents with get () = this.Flags_ &&& PublishChangeEventsMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| PublishChangeEventsMask else this.Flags_ &&& ~~~PublishChangeEventsMask
    member inline this.Enabled with get () = this.Flags_ &&& EnabledMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| EnabledMask else this.Flags_ &&& ~~~EnabledMask
    member inline this.Visible with get () = this.Flags_ &&& VisibleMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| VisibleMask else this.Flags_ &&& ~~~VisibleMask
    member inline this.AlwaysUpdate with get () = this.Flags_ &&& AlwaysUpdateMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| AlwaysUpdateMask else this.Flags_ &&& ~~~AlwaysUpdateMask
    member inline this.PublishUpdates with get () = this.Flags_ &&& PublishUpdatesMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| PublishUpdatesMask else this.Flags_ &&& ~~~PublishUpdatesMask
    member inline this.PublishPostUpdates with get () = this.Flags_ &&& PublishPostUpdatesMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| PublishPostUpdatesMask else this.Flags_ &&& ~~~PublishPostUpdatesMask
    member inline this.Persistent with get () = this.Flags_ &&& PersistentMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| PersistentMask else this.Flags_ &&& ~~~PersistentMask
    member inline this.IgnorePropertyBindings with get () = this.Flags_ &&& IgnorePropertyBindingsMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| IgnorePropertyBindingsMask else this.Flags_ &&& ~~~IgnorePropertyBindingsMask
    member inline this.Mounted with get () = this.Flags_ &&& MountedMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| MountedMask else this.Flags_ &&& ~~~MountedMask
    member inline this.EnabledLocal with get () = this.Flags_ &&& EnabledLocalMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| EnabledLocalMask else this.Flags_ &&& ~~~EnabledLocalMask
    member inline this.VisibleLocal with get () = this.Flags_ &&& VisibleLocalMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| VisibleLocalMask else this.Flags_ &&& ~~~VisibleLocalMask
    member inline this.RotationMatrixDirty with get () = this.Flags_ &&& RotationMatrixDirtyMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| RotationMatrixDirtyMask else this.Flags_ &&& ~~~RotationMatrixDirtyMask
    member inline this.AffineMatrixDirty with get () = this.Flags_ &&& AffineMatrixDirtyMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| AffineMatrixDirtyMask else this.Flags_ &&& ~~~AffineMatrixDirtyMask
    member inline this.Optimized with get () = this.Imperative && this.Omnipresent && not this.PublishChangeBindings && not this.PublishChangeEvents // TODO: see if I can remove all conditionals from here.

    member inline this.Position with get () = this.Position_ and set value = this.Position_ <- value; this.AffineMatrixDirty <- true
    member inline this.Scale with get () = this.Scale_ and set value = this.Scale_ <- value; this.AffineMatrixDirty <- true
    member inline this.Offset with get () = this.Offset_ and set value = this.Offset_ <- value; this.AffineMatrixDirty <- true
    member inline this.Size with get () = this.Size_ and set value = this.Size_ <- value; this.AffineMatrixDirty <- true
    member inline this.Elevation with get () = this.Elevation_ and set value = this.Elevation_ <- value; this.AffineMatrixDirty <- true

    member inline this.Rotation
        with get () = this.Rotation_
        and set value =
            this.Rotation_ <- value
            let pitchYawRoll = value.PitchYawRoll ()
            this.Angles_.X <- pitchYawRoll.X
            this.Angles_.Y <- pitchYawRoll.Y
            this.Angles_.Z <- pitchYawRoll.Z
            this.RotationMatrixDirty <- true
            this.AffineMatrixDirty <- true

    member inline this.Angles
        with get () = this.Angles_
        and set value =
            this.Angles_ <- value
            this.Rotation_ <- Quaternion.CreateFromYawPitchRoll (value.Y, value.X, value.Z)
            this.RotationMatrixDirty <- true
            this.AffineMatrixDirty <- true

    member this.RotationMatrix =
        if notNull (this.RotationMatrixOpt_ :> obj) then
            if this.RotationMatrixDirty then this.RotationMatrixOpt_ := Matrix4x4.CreateFromQuaternion this.Rotation_
            this.RotationMatrixOpt_.Value
        else Matrix4x4.Identity

    member this.AffineMatrix =
        if notNull (this.AffineMatrixOpt_ :> obj) then
            if this.AffineMatrixDirty then
                // TODO: P1: optimize this hella!
                let positionMatrix = Matrix4x4.CreateTranslation this.Position_
                let rotationMatrix = this.RotationMatrix
                let scaleMatrix = Matrix4x4.CreateScale this.Scale_
                this.AffineMatrixOpt_ := positionMatrix * rotationMatrix * scaleMatrix
            this.AffineMatrixOpt_.Value
        else Matrix4x4.Identity

    member inline this.Extent = this.Size_ * 0.5f
    member inline this.Right = Vector3 (this.RotationMatrix.M11, this.RotationMatrix.M12, this.RotationMatrix.M13) // TODO: implement Row properties.
    member inline this.Up = Vector3 (this.RotationMatrix.M21, this.RotationMatrix.M22, this.RotationMatrix.M23)
    member inline this.Forward = -Vector3 (this.RotationMatrix.M31, this.RotationMatrix.M32, this.RotationMatrix.M33)
    member inline this.Left = -this.Right
    member inline this.Down = -this.Up
    member inline this.Backward = -this.Forward

    member inline this.PositionScaled = this.Position_ * this.Scale_
    member inline this.SizeScaled = this.Size_ * this.Scale_
    member inline this.ExtentScaled = this.Extent * this.Scale_

    member inline this.OffsetScaled =
        if not this.Rotation_.IsIdentity
        then Vector3.Transform (this.Offset_, this.Rotation_) * this.Scale_
        else this.Offset_ * this.Scale_

    member this.Dimensions =
        let scale = this.Scale_
        let sizeScaled = this.Size_ * scale
        let extentScaled = sizeScaled * 0.5f
        let positionScaled = this.Position_ - extentScaled
        let offsetScaled = this.OffsetScaled
        Box3 (positionScaled + offsetScaled, sizeScaled)

    member this.AABB =
        let dimensions = this.Dimensions
        dimensions.Orient this.Rotation_

    member this.Copy =
        let mutable copy = Transform.makeEmpty ()
        Transform.assignByRef (&this, &copy)
        copy

    member inline this.InvalidateFast () =
        this.Flags_ <- this.Flags_ ||| TransformMasks.InvalidatedMask

    /// Test transforms for equality.
    static member equalsByRef (left : Transform inref, right : Transform inref) =
        left.Flags_ = right.Flags_ &&
        left.Position_.Equals right.Position_ &&
        left.Rotation_.Equals right.Rotation_ &&
        left.Scale_.Equals right.Scale_ &&
        left.Offset_.Equals right.Offset_ &&
        left.Angles_.Equals right.Angles_ &&
        left.Size_.Equals right.Size_ &&
        left.Elevation_ = right.Elevation_

    /// Test transforms for equality.
    static member inline equals (left : Transform) (right : Transform) =
        Transform.equalsByRef (&left, &right)

    /// Assign the value of the left transform to the right.
    static member assignByRef (source : Transform inref, target : Transform byref) =
        target.Flags_ <- source.Flags_
        target.Position_ <- source.Position_
        target.Rotation_ <- source.Rotation_
        target.Scale_ <- source.Scale_
        target.Offset_ <- source.Offset_
        if notNull (source.RotationMatrixOpt_ :> obj) then target.RotationMatrixOpt_ := source.RotationMatrixOpt_.Value
        if notNull (source.AffineMatrixOpt_ :> obj) then target.AffineMatrixOpt_ := source.AffineMatrixOpt_.Value
        target.Elevation_ <- source.Elevation_
        target.Offset_ <- source.Offset_
        target.Size_ <- source.Size_

    /// Assign the value of the left transform to the right.
    static member inline assign (source : Transform, target : Transform byref) =
        Transform.assignByRef (&source, &target)

    /// Make an empty transform.
    static member inline makeEmpty () =
        Unchecked.defaultof<Transform>

    /// Make a transform with default values.
    static member makeDefault () =
        let mutable transform = Unchecked.defaultof<Transform>
        transform.Scale_ <- Vector3.One
        transform.RotationMatrixOpt_ <- ref Matrix4x4.Identity
        transform.AffineMatrixOpt_ <- ref Matrix4x4.Identity
        transform.Size_ <- Vector3.One
        transform.Flags_ <- DefaultFlags
        transform

    interface Transform Component with
        member this.TypeName = nameof Transform
        member this.Active with get () = this.Flags_ &&& ActiveMask <> 0u and set value = this.Flags_ <- if value then this.Flags_ ||| ActiveMask else this.Flags_ &&& ~~~ActiveMask

[<AutoOpen>]
module TransformOperators =

    /// Check two transforms for equality.
    let inline trEq (left : Transform) (right : Transform) =
        Transform.equals left right

    /// Check two transforms for inequality.
    let inline trNeq (left : Transform) (right : Transform) =
        not (Transform.equals left right)

[<AutoOpen>]
module Vector2 =

    type Vector2 with
        member this.XYZ = Vector3 (this.X, this.Y, 0.0f)
        member this.MapX mapper = Vector2 (mapper this.X, this.Y)
        member this.MapY mapper = Vector2 (this.X, mapper this.Y)
        member this.WithX x = Vector2 (x, this.Y)
        member this.WithY y = Vector2 (this.X, y)
        member this.Rotate r = Vector2 (cos r * this.X - sin r * this.Y, sin r * this.X + cos r * this.Y)

    let inline v2 x y = Vector2 (x, y)
    let inline v2Eq (x : Vector2) (y : Vector2) = x.X = y.X && x.Y = y.Y
    let inline v2Neq (x : Vector2) (y : Vector2) = x.X <> y.X || x.Y <> y.Y
    let inline v2Dup (a : single) = v2 a a
    let v2One = Vector2.One
    let v2Zero = Vector2.Zero
    let v2UnitX = Vector2.UnitX
    let v2UnitY = Vector2.UnitY
    let v2Up = v2 0.0f 1.0f
    let v2Right = v2 1.0f 0.0f
    let v2Down = v2 0.0f -1.0f
    let v2Left = v2 -1.0f 0.0f

/// The Vector2 value that can be plugged into the scripting language.
type [<CustomEquality; CustomComparison>] Vector2Pluggable =
    { Vector2 : Vector2 }

    static member equals left right =
        v2Eq left.Vector2 right.Vector2

    static member compare left right =
        compare
            struct (left.Vector2.X, left.Vector2.Y)
            struct (right.Vector2.X, right.Vector2.Y)

    override this.GetHashCode () =
        hash this.Vector2

    override this.Equals that =
        match that with
        | :? Vector2Pluggable as that -> Vector2Pluggable.equals this that
        | _ -> failwithumf ()

    interface Vector2Pluggable IComparable with
        member this.CompareTo that =
            Vector2Pluggable.compare this that

    interface Scripting.Pluggable with

        member this.CompareTo that =
            match that with
            | :? Vector2Pluggable as that -> (this :> Vector2Pluggable IComparable).CompareTo that
            | _ -> failwithumf ()

        member this.TypeName =
            "Vector2"

        member this.FSharpType =
            getType this.Vector2

        member this.ToSymbol () =
            let v2 = Symbol.Atom ("v2", None)
            let x = Symbol.Number (scstring this.Vector2.X, None)
            let y = Symbol.Number (scstring this.Vector2.Y, None)
            Symbol.Symbols ([v2; x; y], None)

[<AutoOpen>]
module Vector3 =

    type Vector3 with
        member this.XY = Vector2 (this.X, this.Y)
        member this.MapX mapper = Vector3 (mapper this.X, this.Y, this.Z)
        member this.MapY mapper = Vector3 (this.X, mapper this.Y, this.Z)
        member this.MapZ mapper = Vector3 (this.X, this.Y, mapper this.Z)
        member this.WithX x = Vector3 (x, this.Y, this.Z)
        member this.WithY y = Vector3 (this.X, y, this.Z)
        member this.WithZ z = Vector3 (this.X, this.Y, z)

    let inline v3 x y z = Vector3 (x, y, z)
    let inline v3Eq (x : Vector3) (y : Vector3) = x.X = y.X && x.Y = y.Y && x.Z = y.Z
    let inline v3Neq (x : Vector3) (y : Vector3) = x.X <> y.X || x.Y <> y.Y || x.Z <> y.Z
    let inline v3Dup (a : single) = v3 a a a
    let v3One = Vector3.One
    let v3Zero = Vector3.Zero
    let v3UnitX = Vector3.UnitX
    let v3UnitY = Vector3.UnitY
    let v3UnitZ = Vector3.UnitZ
    let v3Up = v3 0.0f 1.0f 0.0f
    let v3Right = v3 1.0f 0.0f 0.0f
    let v3Down = v3 0.0f -1.0f 0.0f
    let v3Left = v3 -1.0f 0.0f 0.0f
    let v3Forward = v3 -1.0f 0.0f 0.0f
    let v3Backward = v3 1.0f 0.0f 0.0f

/// Converts Vector2 types.
type Vector2Converter () =
    inherit TypeConverter ()

    override this.CanConvertTo (_, destType) =
        destType = typeof<Symbol> ||
        destType = typeof<Vector2>

    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<Symbol> then
            let v2 = source :?> Vector2
            Symbols
                ([Number (scstring v2.X, None)
                  Number (scstring v2.Y, None)], None) :> obj
        elif destType = typeof<Vector2> then source
        else failconv "Invalid Vector2Converter conversion to source." None

    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<Symbol> ||
        sourceType = typeof<Vector2>

    override this.ConvertFrom (_, _, source) =
        match source with
        | :? Symbol as symbol ->
            match symbol with
            | Symbols ([Number (x, _); Number (y, _)], _) -> Vector2 (scvalue x, scvalue y) :> obj
            | _ -> failconv "Invalid Vector2Converter conversion from source." (Some symbol)
        | :? Vector2 -> source
        | _ -> failconv "Invalid Vector2Converter conversion from source." None

/// The Vector3 value that can be plugged into the scripting language.
type [<CustomEquality; CustomComparison>] Vector3Pluggable =
    { Vector3 : Vector3 }

    static member equals left right =
        v3Eq left.Vector3 right.Vector3

    static member compare left right =
        compare
            struct (left.Vector3.X, left.Vector3.Y, left.Vector3.Z)
            struct (right.Vector3.X, right.Vector3.Y, right.Vector3.Z)

    override this.GetHashCode () =
        hash this.Vector3

    override this.Equals that =
        match that with
        | :? Vector3Pluggable as that -> Vector3Pluggable.equals this that
        | _ -> failwithumf ()

    interface Vector3Pluggable IComparable with
        member this.CompareTo that =
            Vector3Pluggable.compare this that

    interface Scripting.Pluggable with

        member this.CompareTo that =
            match that with
            | :? Vector3Pluggable as that -> (this :> Vector3Pluggable IComparable).CompareTo that
            | _ -> failwithumf ()

        member this.TypeName =
            "Vector3"

        member this.FSharpType =
            getType this.Vector3

        member this.ToSymbol () =
            let v3 = Symbol.Atom ("v3", None)
            let x = Symbol.Number (scstring this.Vector3.X, None)
            let y = Symbol.Number (scstring this.Vector3.Y, None)
            let z = Symbol.Number (scstring this.Vector3.Z, None)
            Symbol.Symbols ([v3; x; y; z], None)

/// Converts Vector3 types.
type Vector3Converter () =
    inherit TypeConverter ()

    override this.CanConvertTo (_, destType) =
        destType = typeof<Symbol> ||
        destType = typeof<Vector3>

    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<Symbol> then
            let v3 = source :?> Vector3
            Symbols
                ([Number (scstring v3.X, None)
                  Number (scstring v3.Y, None)
                  Number (scstring v3.Z, None)], None) :> obj
        elif destType = typeof<Vector3> then source
        else failconv "Invalid Vector3Converter conversion to source." None

    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<Symbol> ||
        sourceType = typeof<Vector3>

    override this.ConvertFrom (_, _, source) =
        match source with
        | :? Symbol as symbol ->
            match symbol with
            | Symbols ([Number (x, _); Number (y, _); Number (z, _)], _) ->
                Vector3 (scvalue x, scvalue y, scvalue z) :> obj
            | _ ->
                failconv "Invalid Vector3Converter conversion from source." (Some symbol)
        | :? Vector3 -> source
        | _ -> failconv "Invalid Vector3Converter conversion from source." None

[<AutoOpen>]
module Vector4 =

    type Vector4 with
        member this.MapX mapper = Vector4 (mapper this.X, this.Y, this.Z, this.W)
        member this.MapY mapper = Vector4 (this.X, mapper this.Y, this.Z, this.W)
        member this.MapZ mapper = Vector4 (this.X, this.Y, mapper this.Z, this.W)
        member this.MapW mapper = Vector4 (this.X, this.Y, this.Z, mapper this.W)
        member this.WithX x = Vector4 (x, this.Y, this.Z, this.W)
        member this.WithY y = Vector4 (this.X, y, this.Z, this.W)
        member this.WithZ z = Vector4 (this.X, this.Y, z, this.W)
        member this.WithW w = Vector4 (this.X, this.Y, this.Z, w)

    let inline v4 x y z w = Vector4 (x, y, z, w)
    let inline v4Eq (x : Vector4) (y : Vector4) = x.X = y.X && x.Y = y.Y && x.Z = y.Z && x.W = y.W
    let inline v4Neq (x : Vector4) (y : Vector4) = x.X <> y.X || x.Y <> y.Y || x.Z <> y.Z || x.W <> y.W
    let inline v4Dup (a : single) = v4 a a a a
    let v4One = Vector4.One
    let v4Zero = Vector4.Zero
    let v4UnitX = Vector4.UnitX
    let v4UnitY = Vector4.UnitY
    let v4UnitZ = Vector4.UnitZ
    let v4UnitW = Vector4.UnitW

/// The Vector4 value that can be plugged into the scripting language.
type [<CustomEquality; CustomComparison>] Vector4Pluggable =
    { Vector4 : Vector4 }

    static member equals left right =
        v4Eq left.Vector4 right.Vector4

    static member compare left right =
        compare
            struct (left.Vector4.X, left.Vector4.Y, left.Vector4.Z, left.Vector4.W)
            struct (right.Vector4.X, right.Vector4.Y, right.Vector4.Z, right.Vector4.W)

    override this.GetHashCode () =
        hash this.Vector4

    override this.Equals that =
        match that with
        | :? Vector4Pluggable as that -> Vector4Pluggable.equals this that
        | _ -> failwithumf ()

    interface Vector4Pluggable IComparable with
        member this.CompareTo that =
            Vector4Pluggable.compare this that

    interface Scripting.Pluggable with

        member this.CompareTo that =
            match that with
            | :? Vector4Pluggable as that -> (this :> Vector4Pluggable IComparable).CompareTo that
            | _ -> failwithumf ()

        member this.TypeName =
            "Vector4"

        member this.FSharpType =
            getType this.Vector4

        member this.ToSymbol () =
            let v4 = Symbol.Atom ("v4", None)
            let x = Symbol.Number (scstring this.Vector4.X, None)
            let y = Symbol.Number (scstring this.Vector4.Y, None)
            let z = Symbol.Number (scstring this.Vector4.Z, None)
            let w = Symbol.Number (scstring this.Vector4.W, None)
            Symbol.Symbols ([v4; x; y; z; w], None)

/// Converts Vector4 types.
type Vector4Converter () =
    inherit TypeConverter ()

    override this.CanConvertTo (_, destType) =
        destType = typeof<Symbol> ||
        destType = typeof<Vector4>

    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<Symbol> then
            let v4 = source :?> Vector4
            Symbols
                ([Number (scstring v4.X, None)
                  Number (scstring v4.Y, None)
                  Number (scstring v4.Z, None)
                  Number (scstring v4.W, None)], None) :> obj
        elif destType = typeof<Vector4> then source
        else failconv "Invalid Vector4Converter conversion to source." None

    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<Symbol> ||
        sourceType = typeof<Vector4>

    override this.ConvertFrom (_, _, source) =
        match source with
        | :? Symbol as symbol ->
            match symbol with
            | Symbols ([Number (x, _); Number (y, _); Number (z, _); Number (w, _)], _) ->
                Vector4 (scvalue x, scvalue y, scvalue z, scvalue w) :> obj
            | _ ->
                failconv "Invalid Vector4Converter conversion from source." (Some symbol)
        | :? Vector4 -> source
        | _ -> failconv "Invalid Vector4Converter conversion from source." None

[<AutoOpen>]
module Vector2i =

    type Vector2i with
        member this.XYZ = Vector3i (this.X, this.Y, 0)
        member this.MapX mapper = Vector2i (mapper this.X, this.Y)
        member this.MapY mapper = Vector2i (this.X, mapper this.Y)
        member this.WithX x = Vector2i (x, this.Y)
        member this.WithY y = Vector2i (this.X, y)

    let inline v2i x y = Vector2i (x, y)
    let inline v2iEq (x : Vector2i) (y : Vector2i) = x.X = y.X && x.Y = y.Y
    let inline v2iNeq (x : Vector2i) (y : Vector2i) = x.X <> y.X || x.Y <> y.Y
    let inline v2iDup (a : int) = v2i a a
    let v2iOne = Vector2i.One
    let v2iZero = Vector2i.Zero
    let v2iUnitX = Vector2i.UnitX
    let v2iUnitY = Vector2i.UnitY
    let v2iUp = Vector2i.Up
    let v2iRight = Vector2i.Right
    let v2iDown = Vector2i.Down
    let v2iLeft = Vector2i.Left

/// The Vector2i value that can be plugged into the scripting language.
type [<CustomEquality; CustomComparison>] Vector2iPluggable =
    { Vector2i : Vector2i }

    static member equals left right =
        v2iEq left.Vector2i right.Vector2i

    static member compare left right =
        compare
            struct (left.Vector2i.X, left.Vector2i.Y)
            struct (right.Vector2i.X, right.Vector2i.Y)

    override this.GetHashCode () =
        hash this.Vector2i

    override this.Equals that =
        match that with
        | :? Vector2iPluggable as that -> Vector2iPluggable.equals this that
        | _ -> failwithumf ()

    interface Vector2iPluggable IComparable with
        member this.CompareTo that =
            Vector2iPluggable.compare this that

    interface Scripting.Pluggable with

        member this.CompareTo that =
            match that with
            | :? Vector2iPluggable as that -> (this :> Vector2iPluggable IComparable).CompareTo that
            | _ -> failwithumf ()

        member this.TypeName =
            "Vector2i"

        member this.FSharpType =
            getType this.Vector2i

        member this.ToSymbol () =
            let v2i = Symbol.Atom ("v2i", None)
            let x = Symbol.Number (scstring this.Vector2i.X, None)
            let y = Symbol.Number (scstring this.Vector2i.Y, None)
            Symbol.Symbols ([v2i; x; y], None)

/// Converts Vector2i types.
type Vector2iConverter () =
    inherit TypeConverter ()

    override this.CanConvertTo (_, destType) =
        destType = typeof<Symbol> ||
        destType = typeof<Vector2i>

    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<Symbol> then
            let v2i = source :?> Vector2i
            Symbols ([Number (scstring v2i.X, None); Number (scstring v2i.Y, None)], None) :> obj
        elif destType = typeof<Vector2i> then source
        else failconv "Invalid Vector2iConverter conversion to source." None

    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<Symbol> ||
        sourceType = typeof<Vector2i>

    override this.ConvertFrom (_, _, source) =
        match source with
        | :? Symbol as symbol ->
            match symbol with
            | Symbols ([Number (x, _); Number (y, _)], _) -> Vector2i (scvalue x, scvalue y) :> obj
            | _ -> failconv "Invalid Vector2iConverter conversion from source." (Some symbol)
        | :? Vector2i -> source
        | _ -> failconv "Invalid Vector2iConverter conversion from source." None

[<AutoOpen>]
module Vector3i =

    type Vector3i with
        member this.XY = Vector2i (this.X, this.Y)
        member this.MapX mapper = Vector3i (mapper this.X, this.Y, this.Z)
        member this.MapY mapper = Vector3i (this.X, mapper this.Y, this.Z)
        member this.MapZ mapper = Vector3i (this.X, this.Y, mapper this.Z)
        member this.WithX x = Vector3i (x, this.Y, this.Z)
        member this.WithY y = Vector3i (this.X, y, this.Z)
        member this.WithZ z = Vector3i (this.X, this.Y, z)

    let inline v3i x y z = Vector3i (x, y, z)
    let inline v3iEq (x : Vector3i) (y : Vector3i) = x.X = y.X && x.Y = y.Y && x.Z = y.Z
    let inline v3iNeq (x : Vector3i) (y : Vector3i) = x.X <> y.X || x.Y <> y.Y || x.Z <> y.Z
    let inline v3iDup (a : int) = v3i a a a
    let v3iOne = Vector3i.One
    let v3iZero = Vector3i.Zero
    let v3iUnitX = Vector3i.UnitX
    let v3iUnitY = Vector3i.UnitY
    let v3iUnitZ = Vector3i.UnitZ

/// The Vector3 value that can be plugged into the scripting language.
type [<CustomEquality; CustomComparison>] Vector3iPluggable =
    { Vector3i : Vector3i }

    static member equals left right =
        v3iEq left.Vector3i right.Vector3i

    static member compare left right =
        compare
            struct (left.Vector3i.X, left.Vector3i.Y, left.Vector3i.Z)
            struct (right.Vector3i.X, right.Vector3i.Y, right.Vector3i.Z)

    override this.GetHashCode () =
        hash this.Vector3i

    override this.Equals that =
        match that with
        | :? Vector3iPluggable as that -> Vector3iPluggable.equals this that
        | _ -> failwithumf ()

    interface Vector3iPluggable IComparable with
        member this.CompareTo that =
            Vector3iPluggable.compare this that

    interface Scripting.Pluggable with

        member this.CompareTo that =
            match that with
            | :? Vector3iPluggable as that -> (this :> Vector3iPluggable IComparable).CompareTo that
            | _ -> failwithumf ()

        member this.TypeName =
            "Vector3i"

        member this.FSharpType =
            getType this.Vector3i

        member this.ToSymbol () =
            let v3i = Symbol.Atom ("v3i", None)
            let x = Symbol.Number (scstring this.Vector3i.X, None)
            let y = Symbol.Number (scstring this.Vector3i.Y, None)
            let z = Symbol.Number (scstring this.Vector3i.Z, None)
            Symbol.Symbols ([v3i; x; y; z], None)

/// Converts Vector3i types.
type Vector3iConverter () =
    inherit TypeConverter ()

    override this.CanConvertTo (_, destType) =
        destType = typeof<Symbol> ||
        destType = typeof<Vector3i>

    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<Symbol> then
            let v3i = source :?> Vector3i
            Symbols
                ([Number (scstring v3i.X, None)
                  Number (scstring v3i.Y, None)
                  Number (scstring v3i.Z, None)], None) :> obj
        elif destType = typeof<Vector3i> then source
        else failconv "Invalid Vector3iConverter conversion to source." None

    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<Symbol> ||
        sourceType = typeof<Vector3i>

    override this.ConvertFrom (_, _, source) =
        match source with
        | :? Symbol as symbol ->
            match symbol with
            | Symbols ([Number (x, _); Number (y, _); Number (z, _)], _) ->
                Vector3i (scvalue x, scvalue y, scvalue z) :> obj
            | _ ->
                failconv "Invalid Vector4Converter conversion from source." (Some symbol)
        | :? Vector3i -> source
        | _ -> failconv "Invalid Vector4Converter conversion from source." None

[<AutoOpen>]
module Vector4i =

    type Vector4i with
        member this.MapX mapper = Vector4i (mapper this.X, this.Y, this.Z, this.W)
        member this.MapY mapper = Vector4i (this.X, mapper this.Y, this.Z, this.W)
        member this.MapZ mapper = Vector4i (this.X, this.Y, mapper this.Z, this.W)
        member this.MapW mapper = Vector4i (this.X, this.Y, this.Z, mapper this.W)
        member this.WithX x = Vector4i (x, this.Y, this.Z, this.W)
        member this.WithY y = Vector4i (this.X, y, this.Z, this.W)
        member this.WithZ z = Vector4i (this.X, this.Y, z, this.W)
        member this.WithW w = Vector4i (this.X, this.Y, this.Z, w)

    let inline v4i x y z w = Vector4i (x, y, z, w)
    let inline v4iEq (x : Vector4i) (y : Vector4i) = x.X = y.X && x.Y = y.Y && x.Z = y.Z && x.W = y.W
    let inline v4iNeq (x : Vector4i) (y : Vector4i) = x.X <> y.X || x.Y <> y.Y || x.Z <> y.Z || x.W <> y.W
    let inline v4iDup (a : int) = v4i a a a a
    let v4iOne = Vector4i.One
    let v4iZero = Vector4i.Zero
    let v4iUnitX = Vector4i.UnitX
    let v4iUnitY = Vector4i.UnitY
    let v4iUnitZ = Vector4i.UnitZ
    let v4iUnitW = Vector4i.UnitW

/// The Vector4i value that can be plugged into the scripting language.
type [<CustomEquality; CustomComparison>] Vector4iPluggable =
    { Vector4i : Vector4i }

    static member equals left right =
        v4iEq left.Vector4i right.Vector4i

    static member compare left right =
        compare
            struct (left.Vector4i.X, left.Vector4i.Y, left.Vector4i.Z, left.Vector4i.W)
            struct (right.Vector4i.X, right.Vector4i.Y, right.Vector4i.Z, right.Vector4i.W)

    override this.GetHashCode () =
        hash this.Vector4i

    override this.Equals that =
        match that with
        | :? Vector4iPluggable as that -> Vector4iPluggable.equals this that
        | _ -> failwithumf ()

    interface Vector4iPluggable IComparable with
        member this.CompareTo that =
            Vector4iPluggable.compare this that

    interface Scripting.Pluggable with

        member this.CompareTo that =
            match that with
            | :? Vector4iPluggable as that -> (this :> Vector4iPluggable IComparable).CompareTo that
            | _ -> failwithumf ()

        member this.TypeName =
            "Vector4i"

        member this.FSharpType =
            getType this.Vector4i

        member this.ToSymbol () =
            let v4i = Symbol.Atom ("v4i", None)
            let x = Symbol.Number (scstring this.Vector4i.X, None)
            let y = Symbol.Number (scstring this.Vector4i.Y, None)
            let z = Symbol.Number (scstring this.Vector4i.Z, None)
            let w = Symbol.Number (scstring this.Vector4i.W, None)
            Symbol.Symbols ([v4i; x; y; z; w], None)

/// Converts Vector4i types.
type Vector4iConverter () =
    inherit TypeConverter ()

    override this.CanConvertTo (_, destType) =
        destType = typeof<Symbol> ||
        destType = typeof<Vector4i>

    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<Symbol> then
            let v4i = source :?> Vector4i
            Symbols
                ([Number (scstring v4i.X, None)
                  Number (scstring v4i.Y, None)
                  Number (scstring v4i.Z, None)
                  Number (scstring v4i.W, None)], None) :> obj
        elif destType = typeof<Vector4i> then source
        else failconv "Invalid Vector4iConverter conversion to source." None

    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<Symbol> ||
        sourceType = typeof<Vector4i>

    override this.ConvertFrom (_, _, source) =
        match source with
        | :? Symbol as symbol ->
            match symbol with
            | Symbols ([Number (x, _); Number (y, _); Number (z, _); Number (w, _)], _) ->
                Vector4i (scvalue x, scvalue y, scvalue z, scvalue w) :> obj
            | _ ->
                failconv "Invalid Vector4Converter conversion from source." (Some symbol)
        | :? Vector4i -> source
        | _ -> failconv "Invalid Vector4Converter conversion from source." None

[<AutoOpen>]
module Box2 =
    type Box2 with
        member this.XYZ =
            Box3 (v3 this.Position.X this.Position.Y 0.0f, v3 this.Size.X this.Size.Y 0.0f)

[<AutoOpen>]
module Box3 =
    type Box3 with
        member this.XY =
            Box2 (v2 this.Position.X this.Position.Y, v2 this.Size.X this.Size.Y)

[<AutoOpen>]
module Quaternion =
    type Quaternion with
        member this.PitchYawRoll =
            MathHelper.PitchYawRoll &this
    let inline quatEq (q : Quaternion) (q2 : Quaternion) = q.X = q2.X && q.Y = q2.Y && q.Z = q2.Z && q.W = q2.W
    let inline quatNeq (q : Quaternion) (q2 : Quaternion) = q.X <> q2.X || q.Y <> q2.Y || q.Z <> q2.Z || q.W <> q2.W

[<AutoOpen>]
module Color =

    type Color with
        member this.MapR mapper = Color (mapper this.R, this.G, this.B, this.A)
        member this.MapG mapper = Color (this.R, mapper this.G, this.B, this.A)
        member this.MapB mapper = Color (this.R, this.G, mapper this.B, this.A)
        member this.MapA mapper = Color (this.R, this.G, this.B, mapper this.A)
        member this.ScaleR scalar = Color (byte (single this.R * scalar), this.G, this.B, this.A)
        member this.ScaleG scalar = Color (this.R, byte (single this.G * scalar), this.B, this.A)
        member this.ScaleB scalar = Color (this.R, this.G, byte (single this.B * scalar), this.A)
        member this.ScaleA scalar = Color (this.R, this.G, this.B, byte (single this.A * scalar))
        member this.WithR r = Color (r, this.G, this.B, this.A)
        member this.WithG g = Color (this.R, g, this.B, this.A)
        member this.WithB b = Color (this.R, this.G, b, this.A)
        member this.WithA a = Color (this.R, this.G, this.B, a)

    let inline col r g b a = Color (r, g, b, a)
    let inline colEq (x : Color) (y : Color) = x.R = y.R && x.G = y.G && x.B = y.B && x.A = y.A
    let inline colNeq (x : Color) (y : Color) = x.R <> y.R || x.G <> y.G || x.B <> y.B || x.A <> y.A
    let inline colDup (a : byte) = col a a a a
    let colZero = Color.Zero
    let colWhite = Color.White
    let colBlack = Color.Black
    let colGray = Color.Gray

/// The Color value that can be plugged into the scripting language.
type [<CustomEquality; CustomComparison>] ColorPluggable =
    { Color : Color }

    static member equals left right =
        colEq left.Color right.Color

    static member compare left right =
        compare
            struct (left.Color.R, left.Color.G, left.Color.B, left.Color.A)
            struct (right.Color.R, right.Color.G, right.Color.B, right.Color.A)

    override this.GetHashCode () =
        hash this.Color

    override this.Equals that =
        match that with
        | :? ColorPluggable as that -> ColorPluggable.equals this that
        | _ -> failwithumf ()

    interface ColorPluggable IComparable with
        member this.CompareTo that =
            ColorPluggable.compare this that

    interface Scripting.Pluggable with

        member this.CompareTo that =
            match that with
            | :? ColorPluggable as that -> (this :> ColorPluggable IComparable).CompareTo that
            | _ -> failwithumf ()

        member this.TypeName =
            "Color"

        member this.FSharpType =
            getType this.Color

        member this.ToSymbol () =
            let col = Symbol.Atom ("col", None)
            let r = Symbol.Number (scstring this.Color.R, None)
            let g = Symbol.Number (scstring this.Color.G, None)
            let b = Symbol.Number (scstring this.Color.B, None)
            let a = Symbol.Number (scstring this.Color.A, None)
            Symbol.Symbols ([col; r; g; b; a], None)

/// Converts Color types.
type ColorConverter () =
    inherit TypeConverter ()

    override this.CanConvertTo (_, destType) =
        destType = typeof<Symbol> ||
        destType = typeof<Color>

    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<Symbol> then
            let col = source :?> Color
            Symbols
                ([Number (scstring col.R, None)
                  Number (scstring col.G, None)
                  Number (scstring col.B, None)
                  Number (scstring col.A, None)], None) :> obj
        elif destType = typeof<Color> then source
        else failconv "Invalid ColorConverter conversion to source." None

    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<Symbol> ||
        sourceType = typeof<Color>

    override this.ConvertFrom (_, _, source) =
        match source with
        | :? Symbol as symbol ->
            match symbol with
            | Symbols ([Number (r, _); Number (g, _); Number (b, _); Number (a, _)], _) ->
                Color (scvalue r, scvalue g, scvalue b, scvalue a) :> obj
            | _ ->
                failconv "Invalid ColorConverter conversion from source." (Some symbol)
        | :? Color -> source
        | _ -> failconv "Invalid ColorConverter conversion from source." None

[<AutoOpen>]
module Matrix3x3 =

    type Matrix3x3 with

        /// Gets the inverse view matrix with a terribly hacky method custom-designed to satisfy SDL2's
        /// SDL_RenderCopyEx requirement that all corrdinates be arbitrarily converted to ints.
        /// TODO: See if we can expose an SDL_RenderCopyEx from SDL2(#) that takes floats instead.
        member this.InvertedView () =
            let mutable m = this
            m.M13 <- -m.M13
            m.M23 <- -m.M23
            m.M11 <- 1.0f / m.M11
            m.M22 <- 1.0f / m.M22
            m

    let inline m3 r0 r1 r2 = Matrix3x3 (r0, r1, r2)
    let inline m3Eq (x : Matrix3x3) (y : Matrix3x3) = x = y // NOTE: didn't optimize away allocation here...
    let inline m3Neq (x : Matrix3x3) (y : Matrix3x3) = x <> y // NOTE: didn't optimize away allocation here...
    let m3Identity = Matrix3x3.Identity
    let m3Zero = Matrix3x3.Zero

/// The input for a 2D ray cast operation.
type [<StructuralEquality; NoComparison; Struct>] RayCast2Input =
    { RayBegin : Vector2
      RayEnd : Vector2 }
      
/// The output of a 2D ray cast operation.
type [<StructuralEquality; NoComparison; Struct>] RayCast2Output =
    { mutable Normal : Vector2
      mutable Fraction : single }
    static member inline defaultOutput =
        Unchecked.defaultof<RayCast2Output>

[<RequireQualifiedAccess>]
module Math =

    open tainicom.Aether.Physics2D // NOTE: for implementation of some ray-cast code in terms of Aether.

    let mutable private Initialized = false

    let Epsilon = 1.1920929E-07f

    /// Initializes the type converters found in NuMathModule.
    let init () =
        if not Initialized then
            assignTypeConverter<Vector2, Vector2Converter> ()
            assignTypeConverter<Vector3, Vector3Converter> ()
            assignTypeConverter<Vector4, Vector4Converter> ()
            assignTypeConverter<Vector2i, Vector2iConverter> ()
            assignTypeConverter<Vector3i, Vector3iConverter> ()
            assignTypeConverter<Vector4i, Vector4iConverter> ()
            assignTypeConverter<Color, ColorConverter> ()
            Initialized <- true

    /// Convert radians to degrees.
    let radiansToDegrees (radians : single) =
        MathHelper.RadiansToDegrees -radians

    /// Convert radians to degrees in 3D.
    let radiansToDegrees3d (radians : Vector3) =
        v3
            (radiansToDegrees radians.X)
            (radiansToDegrees radians.Y)
            (radiansToDegrees radians.Z)

    /// Convert degrees to radians.
    let degreesToRadians (degrees : single) =
        MathHelper.DegreesToRadians -degrees

    /// Convert degrees to radians in 3D.
    let degreesToRadians3d (degrees : Vector3) =
        v3
            (degreesToRadians degrees.X)
            (degreesToRadians degrees.Y)
            (degreesToRadians degrees.Z)

    /// Snap an int value to an offset.
    let snap offset value =
        if offset <> 0 then
            let (div, rem) = Math.DivRem (value, offset)
            let rem = if rem < offset / 2 then 0 else offset
            div * offset + rem
        else value

    /// Snap a radian value to an offset.
    let snapR offset value =
        radiansToDegrees value |>
        int |>
        snap offset |>
        single |>
        degreesToRadians

    /// Snap a Vector3 radian value to an offset.
    let snapR3d offset (v3 : Vector3) =
        Vector3 (snapR offset v3.X, snapR offset v3.Y, snapR offset v3.Z)

    /// Snap an single float value to an offset.
    let snapF offset (value : single) =
        single (snap offset (int value))

    /// Snap a Vector3 value to an offset.
    let snapF3d offset (v3 : Vector3) =
        Vector3 (snapF offset v3.X, snapF offset v3.Y, snapF offset v3.Z)

    /// Snap a Transform value to an offset.
    let snapTransform positionSnap rotationSnap (transform : Transform) =
        let mutable transform = transform
        transform.Position <- snapF3d positionSnap transform.Position
        transform.Angles <- snapR3d rotationSnap transform.Angles
        transform

    /// Check that a point is within the given bounds.
    /// TODO: move this into Box3 definition.
    let isPointInBounds (point : Vector3) (bounds : Box3) =
        point.X >= bounds.Position.X &&
        point.Y >= bounds.Position.Y &&
        point.Z >= bounds.Position.Z &&
        point.X <= bounds.Position.X + bounds.Size.X &&
        point.Y <= bounds.Position.Y + bounds.Size.Y &&
        point.Z <= bounds.Position.Z + bounds.Size.Z

    /// Check that a point is within the given bounds.
    /// TODO: move this into Box2 definition.
    let isPointInBounds2d (point : Vector2) (bounds : Box2) =
        point.X >= bounds.Position.X &&
        point.Y >= bounds.Position.Y &&
        point.X <= bounds.Position.X + bounds.Size.X &&
        point.Y <= bounds.Position.Y + bounds.Size.Y

    /// Check that a bounds is within the given bounds.
    /// TODO: move this into Box3 definition.
    let isBoundsInBounds (bounds : Box3) (bounds2 : Box3) =
        bounds.Position.X >= bounds2.Position.X &&
        bounds.Position.Y >= bounds2.Position.Y &&
        bounds.Position.Z >= bounds2.Position.Z &&
        bounds.Position.X + bounds.Size.X <= bounds2.Position.X + bounds2.Size.X &&
        bounds.Position.Y + bounds.Size.Y <= bounds2.Position.Y + bounds2.Size.Y &&
        bounds.Position.Z + bounds.Size.Z <= bounds2.Position.Z + bounds2.Size.Z

    /// Check that a bounds is within the given bounds.
    /// TODO: move this into Box2 definition.
    let isBoundsInBounds2d (bounds : Box2) (bounds2 : Box2) =
        bounds.Position.X >= bounds2.Position.X &&
        bounds.Position.Y >= bounds2.Position.Y &&
        bounds.Position.X + bounds.Size.X <= bounds2.Position.X + bounds2.Size.X &&
        bounds.Position.Y + bounds.Size.Y <= bounds2.Position.Y + bounds2.Size.Y

    /// Check that a bounds is intersecting the given bounds.
    /// TODO: move this into Box3 definition.
    let isBoundsIntersectingBounds (bounds : Box3) (bounds2 : Box3) =
        bounds.Position.X < bounds2.Position.X + bounds2.Size.X &&
        bounds.Position.Y < bounds2.Position.Y + bounds2.Size.Y &&
        bounds.Position.Z < bounds2.Position.Z + bounds2.Size.Z &&
        bounds.Position.X + bounds.Size.X > bounds2.Position.X &&
        bounds.Position.Y + bounds.Size.Y > bounds2.Position.Y &&
        bounds.Position.Z + bounds.Size.Z > bounds2.Position.Z

    /// Check that a bounds is intersecting the given bounds.
    /// TODO: move this into Box2 definition.
    let isBoundsIntersectingBounds2d (bounds : Box2) (bounds2 : Box2) =
        bounds.Position.X < bounds2.Position.X + bounds2.Size.X &&
        bounds.Position.Y < bounds2.Position.Y + bounds2.Size.Y &&
        bounds.Position.X + bounds.Size.X > bounds2.Position.X &&
        bounds.Position.Y + bounds.Size.Y > bounds2.Position.Y

    /// Get the 2D view of the eye in absolute terms (world space).
    let getViewAbsolute2d (_ : Vector2) (_ : Vector2) =
        Matrix3x3.Identity
        
    /// Get the 2D view of the eye in absolute terms (world space) with translation sliced on
    /// integers.
    let getViewAbsoluteI2d (_ : Vector2) (_ : Vector2) =
        Matrix3x3.Identity

    /// The relative 2D view of the eye with original single values. Due to the problems with
    /// SDL_RenderCopyEx as described in Math.fs, using this function to decide on sprite
    /// coordinates is very, very bad for rendering.
    let getViewRelative2d (eyePosition : Vector2) (_ : Vector2) =
        Matrix3x3.CreateTranslation eyePosition

    /// The relative 2D view of the eye with translation sliced on integers. Good for rendering.
    let getViewRelativeI2d (eyePosition : Vector2) (_ : Vector2) =
        let translation = eyePosition
        let translationI = Vector2 (single (int translation.X), single (int translation.Y))
        Matrix3x3.CreateTranslation translationI

    /// Perform a 2D ray cast on a circle.
    /// Code adapted from - https://github.com/tainicom/Aether.Physics2D/blob/aa8a6b45c63e26c2f408ffde40f03cbe78ecfa7c/Physics2D/Collision/Shapes/CircleShape.cs#L93-L134
    let rayCastCircle2d (position : Vector2) (radius : single) (input : RayCast2Input inref) (output : RayCast2Output outref) =
        let mutable s = input.RayBegin - position
        let b = Vector2.Dot (s, s) - 2.0f * radius
        let mutable r = input.RayEnd - input.RayBegin
        let c = Vector2.Dot (s, r)
        let rr = Vector2.Dot (r, r)
        let sigma = c * c - rr * b
        if sigma >= 0f && rr >= Epsilon then
            let a = 0f - (c + single (Math.Sqrt (float sigma)))
            if 0f <= a && a <= rr then
                output.Fraction <- a / rr
                output.Normal <- Vector2.Normalize (s + output.Fraction * r)
                true
            else false
        else false

    /// Perform a 2D ray cast on a rectangle.
    /// BUG: There's a bug in AABB.RayCast that produces invalid normals.
    let rayCastRectangle2d (rectangle : Vector4) (input : RayCast2Input inref) (output : RayCast2Output outref) =
        let point1 = Common.Vector2 (input.RayBegin.X, input.RayBegin.Y)
        let point2 = Common.Vector2 (input.RayEnd.X, input.RayEnd.Y)
        let mutable inputAether = Collision.RayCastInput (MaxFraction = 1.0f, Point1 = point1, Point2 = point2)
        let mutable outputAether = Unchecked.defaultof<Collision.RayCastOutput>
        let aabb  = Collision.AABB (Common.Vector2 (rectangle.X, rectangle.Y), Common.Vector2 (rectangle.X + rectangle.Z, rectangle.Y + rectangle.W))
        let result = aabb.RayCast (&outputAether, &inputAether)
        output.Normal <- Vector2 (outputAether.Normal.X, outputAether.Normal.Y)
        output.Fraction <- outputAether.Fraction
        result

    /// Perform a 2D ray-cast on a line segment (edge).
    /// NOTE: due to unoptimized implementation, this function allocates one object per call!
    /// TODO: adapt the Aether code as was done for circle to improve performance and get rid of said
    /// allocation.
    let rayCastSegment2d segmentBegin segmentEnd (input : RayCast2Input inref) (output : RayCast2Output outref) =
        let point1 = Common.Vector2 (input.RayBegin.X, input.RayBegin.Y)
        let point2 = Common.Vector2 (input.RayEnd.X, input.RayEnd.Y)
        let mutable identity = Common.Transform.Identity // NOTE: superfluous copy of identity to satisfy EdgeShap.RayCast's use of byref instead of inref.
        let mutable inputAether = Collision.RayCastInput (MaxFraction = 1.0f, Point1 = point1, Point2 = point2)
        let mutable outputAether = Unchecked.defaultof<Collision.RayCastOutput>
        let edgeShape = Collision.Shapes.EdgeShape (segmentBegin, segmentEnd) // NOTE: unecessary allocation, ugh!
        let result = edgeShape.RayCast (&outputAether, &inputAether, &identity, 0)
        output.Normal <- Vector2 (outputAether.Normal.X, outputAether.Normal.Y)
        output.Fraction <- outputAether.Fraction
        result