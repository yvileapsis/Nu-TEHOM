﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace Nu
open System
open System.Collections.Generic
open System.Numerics
open Prime

[<AutoOpen>]
module AssimpExtensions =

    type [<NoEquality; NoComparison; Struct>] BoneInfo =
        { BoneOffset : Assimp.Matrix4x4
          mutable TransformWorld : Assimp.Matrix4x4 }

        static member make offset =
            { BoneOffset = offset
              TransformWorld = Assimp.Matrix4x4.Identity }

    /// Convert a matrix from an Assimp representation to Nu's.
    let AssimpToNu (m : Assimp.Matrix4x4) =
        Matrix4x4
            (m.A1, m.B1, m.C1, m.D1,
                m.A2, m.B2, m.C2, m.D2,
                m.A3, m.B3, m.C3, m.D3,
                m.A4, m.B4, m.C4, m.D4)

    let TryGetAnimationChannel (pAnimation : Assimp.Animation, nodeName : string) =
        let mutable resultOpt = None
        let mutable i = 0
        while resultOpt.IsNone && i < pAnimation.NodeAnimationChannels.Count do
            let channel = pAnimation.NodeAnimationChannels.[i]
            if (channel.NodeName = nodeName) then resultOpt <- Some channel
            else i <- inc i
        resultOpt

    let FindPosition (animationTime : single, channel : Assimp.NodeAnimationChannel) =
        let mutable found = false
        let mutable i = 0
        while not found && i < dec channel.PositionKeyCount do
            if animationTime < single channel.PositionKeys.[inc i].Time then found <- true
            else i <- inc i
        i

    let FindRotation (animationTime : single, channel : Assimp.NodeAnimationChannel) =
        let mutable found = false
        let mutable i = 0
        while not found && i < dec channel.RotationKeyCount do
            if animationTime < single channel.RotationKeys.[inc i].Time then found <- true
            else i <- inc i
        i

    let FindScaling (animationTime : single, channel : Assimp.NodeAnimationChannel) =
        let mutable found = false
        let mutable i = 0
        while not found && i < dec channel.ScalingKeyCount do
            if animationTime < single channel.ScalingKeys.[inc i].Time then found <- true
            else i <- inc i
        i

    let CalcInterpolatedPosition (animationTime : single, channel : Assimp.NodeAnimationChannel) =
        if channel.PositionKeys.Count = 1 then
            channel.PositionKeys.[0].Value
        else
            let PositionIndex = FindPosition (animationTime, channel)
            let NextPositionIndex = inc PositionIndex
            assert (NextPositionIndex < channel.PositionKeys.Count)
            let DeltaTime = single (channel.PositionKeys.[NextPositionIndex].Time - channel.PositionKeys.[PositionIndex].Time)
            let Factor = (animationTime - single channel.PositionKeys.[PositionIndex].Time) / DeltaTime
            assert (Factor >= 0.0f && Factor <= 1.0f)
            let Start = channel.PositionKeys.[PositionIndex].Value
            let End = channel.PositionKeys.[NextPositionIndex].Value
            let Delta = End - Start
            let Result = Start + Factor * Delta
            Result

    let CalcInterpolatedRotation (animationTime : single, channel : Assimp.NodeAnimationChannel) =
        if channel.RotationKeys.Count = 1 then
            channel.RotationKeys.[0].Value
        else
            let RotationIndex = FindRotation (animationTime, channel)
            let NextRotationIndex = inc RotationIndex
            assert (NextRotationIndex < channel.RotationKeys.Count)
            let DeltaTime = single (channel.RotationKeys.[NextRotationIndex].Time - channel.RotationKeys.[RotationIndex].Time)
            let Factor = (animationTime - single channel.RotationKeys.[RotationIndex].Time) / DeltaTime
            assert (Factor >= 0.0f && Factor <= 1.0f)
            let StartRotationQ = channel.RotationKeys.[RotationIndex].Value
            let EndRotationQ = channel.RotationKeys.[NextRotationIndex].Value
            let Result = Assimp.Quaternion.Slerp (StartRotationQ, EndRotationQ, Factor)
            Result.Normalize () // TODO: consider not normalizing here since we should already have two unit quaternions.
            Result

    let CalcInterpolatedScaling (animationTime : single, channel : Assimp.NodeAnimationChannel) =
        if channel.ScalingKeys.Count = 1 then
            channel.ScalingKeys.[0].Value
        else
            let ScalingIndex = FindScaling (animationTime, channel)
            let NextScalingIndex = inc ScalingIndex
            assert (NextScalingIndex < channel.ScalingKeys.Count)
            let DeltaTime = single (channel.ScalingKeys.[NextScalingIndex].Time - channel.ScalingKeys.[ScalingIndex].Time)
            let Factor = (animationTime - single channel.ScalingKeys.[ScalingIndex].Time) / DeltaTime
            assert (Factor >= 0.0f && Factor <= 1.0f)
            let Start = channel.ScalingKeys.[ScalingIndex].Value
            let End = channel.ScalingKeys.[NextScalingIndex].Value
            let Delta = End - Start
            let Result = Start + Factor * Delta
            Result

    let rec UpdateBoneTransforms (boneMapping : Dictionary<string, int>, boneInfos : BoneInfo array, animationTime : single, animationIndex : int, node : Assimp.Node, parentTransform : Assimp.Matrix4x4, rootTransformInverse : Assimp.Matrix4x4, scene : Assimp.Scene) =

        // compute bone's local transform
        let name = node.Name
        let transformLocal =
            match TryGetAnimationChannel (scene.Animations.[animationIndex], name) with
            | Some channel ->
                let scale = Assimp.Matrix4x4.FromScaling (CalcInterpolatedScaling (animationTime, channel))
                let rotation = Assimp.Matrix4x4 ((CalcInterpolatedRotation (animationTime, channel)).GetMatrix ())
                let translation = Assimp.Matrix4x4.FromTranslation (CalcInterpolatedPosition (animationTime, channel))
                translation * rotation * scale // NOTE: should be a faster way.
            | None -> node.Transform

        // compute bone's world transform
        let transformWorld = parentTransform * transformLocal
        match boneMapping.TryGetValue name with
        | (true, boneId) ->
            let boneOffset = boneInfos.[boneId].BoneOffset
            boneInfos.[boneId].TransformWorld <- rootTransformInverse * transformWorld * boneOffset
        | (false, _) -> ()

        // recur
        for i in 0 .. dec node.Children.Count do
            let child = node.Children.[i]
            UpdateBoneTransforms (boneMapping, boneInfos, animationTime, animationIndex, child, transformWorld, rootTransformInverse, scene)

    /// Mesh extensions.
    type Assimp.Mesh with

        member this.AnimateBones (animationTime, animationIndex, scene : Assimp.Scene) =

            // pre-compute bone id dict and bone info storage (these should probably persist outside of this function and be reused)
            let boneIds = dictPlus StringComparer.Ordinal []
            let boneInfos = Array.zeroCreate<BoneInfo> this.Bones.Count
            for i in 0 .. dec this.Bones.Count do
                let bone = this.Bones.[i]
                let boneName = bone.Name
                boneIds.[boneName] <- i
                boneInfos.[i] <- BoneInfo.make bone.OffsetMatrix

            // write bone transforms to bone infos array
            let rootTransformInverse = scene.RootNode.Transform
            rootTransformInverse.Inverse ()
            UpdateBoneTransforms (boneIds, boneInfos, animationTime, animationIndex, scene.RootNode, Assimp.Matrix4x4.Identity, rootTransformInverse, scene)

            // convert bone info transforms to Nu's m4 representation
            Array.map (fun (boneInfo : BoneInfo) -> AssimpToNu boneInfo.TransformWorld) boneInfos

    /// Node extensions.
    type Assimp.Node with

        /// Get the world transform of the node.
        member this.TransformWorld =
            let mutable parentOpt = this.Parent
            let mutable transform = this.Transform
            while notNull parentOpt do
                transform <- transform * parentOpt.Transform
                parentOpt <- parentOpt.Parent
            transform

        /// Collect all the child nodes of a node, including the node itself.
        member this.CollectNodes () =
            seq {
                yield this
                for child in this.Children do
                    yield! child.CollectNodes () }

        /// Collect all the child nodes and transforms of a node, including the node itself.
        member this.CollectNodesAndTransforms (unitType, parentTransform : Matrix4x4) =
            seq {
                let localTransform = AssimpToNu this.Transform
                let worldTransform = localTransform * parentTransform
                yield (this, worldTransform)
                for child in this.Children do
                    yield! child.CollectNodesAndTransforms (unitType, worldTransform) }

        /// Map to a TreeNode.
        member this.Map<'a> (parentNames : string array, parentTransform : Matrix4x4, mapper : Assimp.Node -> string array -> Matrix4x4 -> 'a array TreeNode) : 'a array TreeNode =
            let localName = this.Name
            let localTransform = AssimpToNu this.Transform
            let worldNames = Array.append parentNames [|localName|]
            let worldTransform = localTransform * parentTransform
            let node = mapper this worldNames worldTransform
            for child in this.Children do
                let child = child.Map<'a> (worldNames, worldTransform, mapper)
                node.Add child
            node