﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace Nu
open System
open System.Collections.Generic
open System.Linq
open System.Numerics
open System.Runtime.InteropServices
open BulletSharp
open Prime

type [<CustomEquality; NoComparison>] private UnscaledPointsKey =
    { HashCode : int
      Vertices : Vector3 array }

    static member hash chs =
        chs.HashCode

    static member equals left right =
        left.HashCode = right.HashCode && // TODO: ensure this isn't just a minor pessimization.
        Enumerable.SequenceEqual (left.Vertices, right.Vertices)

    static member comparer =
        HashIdentity.FromFunctions UnscaledPointsKey.hash UnscaledPointsKey.equals

    static member make (vertices : Vector3 array) =
        let hashCode =
            hash vertices.Length ^^^
            (if vertices.Length > 0 then vertices.[0].GetHashCode () else 0) ^^^
            (if vertices.Length > 0 then vertices.[vertices.Length / 2].GetHashCode () else 0) ^^^
            (if vertices.Length > 0 then vertices.[vertices.Length - 1].GetHashCode () else 0)
        { HashCode = hashCode
          Vertices = vertices }

    interface UnscaledPointsKey IEquatable with
        member this.Equals that =
            UnscaledPointsKey.equals this that

    override this.Equals that =
        match that with
        | :? UnscaledPointsKey as that -> UnscaledPointsKey.equals this that
        | _ -> false

    override this.GetHashCode () =
        this.HashCode

type private KinematicCharacter3d =
    { GravityOverride : Vector3 option
      CenterOffset : Vector3
      mutable Center : Vector3
      mutable Rotation : Quaternion
      mutable LinearVelocity : Vector3
      mutable AngularVelocity : Vector3
      CharacterController : KinematicCharacterController
      Ghost : GhostObject }

/// The 3d implementation of PhysicsEngine in terms of Bullet Physics.
/// TODO: only record the collisions for bodies that have event subscriptions associated with them?
type [<ReferenceEquality>] PhysicsEngine3d =
    private
        { PhysicsContext : DynamicsWorld
          Constraints : Dictionary<BodyJointId, TypedConstraint>
          Bodies : Dictionary<BodyId, RigidBody>
          BodiesGravitating : Dictionary<BodyId, Vector3 option * RigidBody>
          Objects : Dictionary<BodyId, CollisionObject>
          Ghosts : Dictionary<BodyId, GhostObject>
          KinematicCharacters : Dictionary<BodyId, KinematicCharacter3d>
          mutable CollisionsFiltered : SDictionary<BodyId * BodyId, Vector3> // TODO: consider using a double buffer dict instead of an sdict.
          CollisionsGround : Dictionary<BodyId, Vector3 List>
          CollisionConfiguration : CollisionConfiguration
          CollisionDispatcher : Dispatcher
          BroadPhaseInterface : BroadphaseInterface
          GhostPairCallback : GhostPairCallback
          ConstraintSolver : ConstraintSolver
          UnscaledPointsCached : Dictionary<UnscaledPointsKey, Vector3 array>
          CreateBodyJointMessages : Dictionary<BodyId, CreateBodyJointMessage List>
          IntegrationMessages : IntegrationMessage List }

    static member private handlePenetration physicsEngine (bodyId : BodyId) (bodyId2 : BodyId) normal =
        let bodyPenetrationMessage =
            { BodyShapeSource = { BodyId = bodyId; BodyShapeIndex = 0 }
              BodyShapeSource2 = { BodyId = bodyId2; BodyShapeIndex = 0 }
              Normal = normal }
        let integrationMessage = BodyPenetrationMessage bodyPenetrationMessage
        physicsEngine.IntegrationMessages.Add integrationMessage

    static member private handleSeparation physicsEngine (bodyId : BodyId) (bodyId2 : BodyId) =
        let bodySeparationMessage =
            { BodyShapeSource = { BodyId = bodyId; BodyShapeIndex = 0 }
              BodyShapeSource2 = { BodyId = bodyId2; BodyShapeIndex = 0 }}
        let integrationMessage = BodySeparationMessage bodySeparationMessage
        physicsEngine.IntegrationMessages.Add integrationMessage

    static member private configureBodyShapeProperties (_ : BodyProperties) (_ : BodyShapeProperties option) (shape : CollisionShape) =
        shape.Margin <- Constants.Physics.Collision3dMargin

    static member private configureCollisionObjectProperties (bodyProperties : BodyProperties) (object : CollisionObject) =
        if bodyProperties.Enabled then
            object.ActivationState <- object.ActivationState ||| ActivationState.ActiveTag
            object.ActivationState <- object.ActivationState &&& ~~~ActivationState.DisableSimulation
        else
            object.ActivationState <- object.ActivationState ||| ActivationState.DisableSimulation
            object.ActivationState <- object.ActivationState &&& ~~~ActivationState.ActiveTag
        object.Friction <- bodyProperties.Friction
        object.Restitution <- bodyProperties.Restitution
        match bodyProperties.CollisionDetection with
        | Discontinuous ->
            object.CcdMotionThreshold <- 0.0f
            object.CcdSweptSphereRadius <- 0.0f
        | Continuous (motionThreshold, sweptSphereRadius) ->
            object.CcdMotionThreshold <- motionThreshold
            object.CcdSweptSphereRadius <- sweptSphereRadius
        match bodyProperties.BodyType with
        | Static ->
            object.CollisionFlags <- object.CollisionFlags ||| CollisionFlags.StaticObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.KinematicObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.CharacterObject
            object.ActivationState <- object.ActivationState &&& ~~~ActivationState.DisableDeactivation
        | Kinematic ->
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.StaticObject
            object.CollisionFlags <- object.CollisionFlags ||| CollisionFlags.KinematicObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.CharacterObject
            object.ActivationState <- object.ActivationState ||| ActivationState.DisableDeactivation
        | KinematicCharacter ->
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.StaticObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.KinematicObject
            object.CollisionFlags <- object.CollisionFlags ||| CollisionFlags.CharacterObject
            object.ActivationState <- object.ActivationState &&& ~~~ActivationState.DisableDeactivation
        | Dynamic ->
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.StaticObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.KinematicObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.CharacterObject
            object.ActivationState <- object.ActivationState &&& ~~~ActivationState.DisableDeactivation
        | DynamicCharacter ->
            Log.infoOnce "DynamicCharacter not yet supported by PhysicsEngine3d. Using Dynamic configuration instead."
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.StaticObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.KinematicObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.CharacterObject
            object.ActivationState <- object.ActivationState &&& ~~~ActivationState.DisableDeactivation

    static member private configureBodyProperties (bodyProperties : BodyProperties) (body : RigidBody) gravity =
        PhysicsEngine3d.configureCollisionObjectProperties bodyProperties body
        body.WorldTransform <- Matrix4x4.CreateFromTrs (bodyProperties.Center, bodyProperties.Rotation, v3One)
        body.MotionState.WorldTransform <- body.WorldTransform
        if bodyProperties.SleepingAllowed // TODO: see if we can find a more reliable way to disable sleeping.
        then body.SetSleepingThresholds (Constants.Physics.SleepingThresholdLinear, Constants.Physics.SleepingThresholdAngular)
        else body.SetSleepingThresholds (0.0f, 0.0f)
        body.LinearVelocity <- bodyProperties.LinearVelocity
        body.LinearFactor <- if bodyProperties.BodyType = Static then v3Zero else v3One
        body.AngularVelocity <- bodyProperties.AngularVelocity
        body.AngularFactor <- if bodyProperties.BodyType = Static then v3Zero else bodyProperties.AngularFactor
        body.SetDamping (bodyProperties.LinearDamping, bodyProperties.AngularDamping)
        body.Gravity <- match bodyProperties.GravityOverride with Some gravityOverride -> gravityOverride | None -> gravity

    static member private attachBoxShape bodySource (bodyProperties : BodyProperties) (boxShape : Nu.BoxShape) (compoundShape : CompoundShape) centerMassInertiaDisposes =
        let box = new BoxShape (boxShape.Size * 0.5f)
        PhysicsEngine3d.configureBodyShapeProperties bodyProperties boxShape.PropertiesOpt box
        box.LocalScaling <-
            bodyProperties.Scale *
            match boxShape.TransformOpt with
            | Some transform -> transform.Scale
            | None -> v3One
        box.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              BodyShapeIndex = match boxShape.PropertiesOpt with Some p -> p.BodyShapeIndex | None -> 0 }
        let center =
            match boxShape.TransformOpt with
            | Some transform -> transform.Translation
            | None -> v3Zero
        let mass =
            match bodyProperties.Substance with
            | Density density ->
                let volume = boxShape.Size.X * boxShape.Size.Y * boxShape.Size.Z
                volume * density
            | Mass mass -> mass
        let inertia = box.CalculateLocalInertia mass
        compoundShape.AddChildShape (Matrix4x4.CreateTranslation center, box)
        (center, mass, inertia, id) :: centerMassInertiaDisposes

    static member private attachSphereShape bodySource (bodyProperties : BodyProperties) (sphereShape : Nu.SphereShape) (compoundShape : CompoundShape) centerMassInertiaDisposes =
        let sphere = new SphereShape (sphereShape.Radius)
        PhysicsEngine3d.configureBodyShapeProperties bodyProperties sphereShape.PropertiesOpt sphere
        sphere.LocalScaling <-
            bodyProperties.Scale *
            match sphereShape.TransformOpt with
            | Some transform -> transform.Scale
            | None -> v3One
        sphere.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              BodyShapeIndex = match sphereShape.PropertiesOpt with Some p -> p.BodyShapeIndex | None -> 0 }
        let center =
            match sphereShape.TransformOpt with
            | Some transform -> transform.Translation
            | None -> v3Zero
        let mass =
            match bodyProperties.Substance with
            | Density density ->
                let volume = 4.0f / 3.0f * MathF.PI * pown sphereShape.Radius 3
                volume * density
            | Mass mass -> mass
        let inertia = sphere.CalculateLocalInertia mass
        compoundShape.AddChildShape (Matrix4x4.CreateTranslation center, sphere)
        (center, mass, inertia, id) :: centerMassInertiaDisposes

    static member private attachCapsuleShape bodySource (bodyProperties : BodyProperties) (capsuleShape : Nu.CapsuleShape) (compoundShape : CompoundShape) centerMassInertiaDisposes =
        let capsule = new CapsuleShape (capsuleShape.Radius, capsuleShape.Height)
        PhysicsEngine3d.configureBodyShapeProperties bodyProperties capsuleShape.PropertiesOpt capsule
        capsule.LocalScaling <-
            bodyProperties.Scale *
            match capsuleShape.TransformOpt with
            | Some transform -> transform.Scale
            | None -> v3One
        capsule.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              BodyShapeIndex = match capsuleShape.PropertiesOpt with Some p -> p.BodyShapeIndex | None -> 0 }
        let center =
            match capsuleShape.TransformOpt with
            | Some transform -> transform.Translation
            | None -> v3Zero
        let mass =
            match bodyProperties.Substance with
            | Density density ->
                let volume = MathF.PI * pown capsuleShape.Radius 2 * (4.0f / 3.0f * capsuleShape.Radius * capsuleShape.Height)
                volume * density
            | Mass mass -> mass
        let inertia = capsule.CalculateLocalInertia mass
        compoundShape.AddChildShape (Matrix4x4.CreateTranslation center, capsule)
        (center, mass, inertia, id) :: centerMassInertiaDisposes

    static member private attachBoxRoundedShape bodySource (bodyProperties : BodyProperties) (boxRoundedShape : BoxRoundedShape) (compoundShape : CompoundShape) centerMassInertiaDisposes =
        Log.info "Rounded box not yet implemented via PhysicsEngine3d; creating a normal box instead."
        let boxShape = { Size = boxRoundedShape.Size; TransformOpt = boxRoundedShape.TransformOpt; PropertiesOpt = boxRoundedShape.PropertiesOpt }
        PhysicsEngine3d.attachBoxShape bodySource bodyProperties boxShape compoundShape centerMassInertiaDisposes

    static member private attachBodyConvexHull bodySource (bodyProperties : BodyProperties) (pointsShape : PointsShape) (compoundShape : CompoundShape) centerMassInertiaDisposes physicsEngine =
        let unscaledPointsKey = UnscaledPointsKey.make pointsShape.Points
        let (optimized, vertices) =
            match physicsEngine.UnscaledPointsCached.TryGetValue unscaledPointsKey with
            | (true, unscaledVertices) -> (true, unscaledVertices)
            | (false, _) -> (false, pointsShape.Points)
        let hull = new ConvexHullShape (vertices)
        PhysicsEngine3d.configureBodyShapeProperties bodyProperties pointsShape.PropertiesOpt hull
        if not optimized then
            hull.OptimizeConvexHull ()
            let unscaledPoints =
                match hull.UnscaledPoints with
                | null -> [|v3Zero|] // guarding against null
                | unscaledPoints -> Array.ofSeq unscaledPoints
            physicsEngine.UnscaledPointsCached.Add (unscaledPointsKey, unscaledPoints)
        hull.LocalScaling <-
            bodyProperties.Scale *
            match pointsShape.TransformOpt with
            | Some transform -> transform.Scale
            | None -> v3One
        hull.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              BodyShapeIndex = match pointsShape.PropertiesOpt with Some p -> p.BodyShapeIndex | None -> 0 }
        // NOTE: we approximate volume with the volume of a bounding box.
        // TODO: use a more accurate volume calculation.
        let mutable min = v3Zero
        let mutable max = v3Zero
        hull.GetAabb (m4Identity, &min, &max)
        let center =
            match pointsShape.TransformOpt with
            | Some transform -> transform.Translation
            | None -> v3Zero
        let box = box3 min max
        let mass =
            match bodyProperties.Substance with
            | Density density ->
                let volume = box.Width * box.Height * box.Depth
                volume * density
            | Mass mass -> mass
        let inertia = hull.CalculateLocalInertia mass
        compoundShape.AddChildShape (Matrix4x4.CreateTranslation center, hull)
        (center, mass, inertia, id) :: centerMassInertiaDisposes

    static member private attachBodyBvhTriangles bodySource (bodyProperties : BodyProperties) (geometryShape : GeometryShape) (compoundShape : CompoundShape) centerMassInertiaDisposes =
        let vertexArray = new TriangleIndexVertexArray (Array.init geometryShape.Vertices.Length id, geometryShape.Vertices)
        let shape = new BvhTriangleMeshShape (vertexArray, true)
        shape.BuildOptimizedBvh ()
        PhysicsEngine3d.configureBodyShapeProperties bodyProperties geometryShape.PropertiesOpt shape
        shape.LocalScaling <-
            bodyProperties.Scale *
            match geometryShape.TransformOpt with
            | Some transform -> transform.Scale
            | None -> v3One
        shape.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              BodyShapeIndex = match geometryShape.PropertiesOpt with Some p -> p.BodyShapeIndex | None -> 0 }
        // NOTE: we approximate volume with the volume of a bounding box.
        // TODO: use a more accurate volume calculation?
        let mutable min = v3Zero
        let mutable max = v3Zero
        shape.GetAabb (m4Identity, &min, &max)
        let center =
            match geometryShape.TransformOpt with
            | Some transform -> transform.Translation
            | None -> v3Zero
        let box = box3 min max
        let mass =
            match bodyProperties.Substance with
            | Density density ->
                let volume = box.Width * box.Height * box.Depth
                volume * density
            | Mass mass -> mass
        let inertia = shape.CalculateLocalInertia mass
        compoundShape.AddChildShape (Matrix4x4.CreateTranslation center, shape)
        (center, mass, inertia, id) :: centerMassInertiaDisposes

    static member private attachGeometryShape bodySource (bodyProperties : BodyProperties) (geometryShape : GeometryShape) (compoundShape : CompoundShape) centerMassInertiaDisposes physicsEngine =
        if geometryShape.Convex then
            let pointsShape = { Points = geometryShape.Vertices; TransformOpt = geometryShape.TransformOpt; PropertiesOpt = geometryShape.PropertiesOpt }
            PhysicsEngine3d.attachBodyConvexHull bodySource bodyProperties pointsShape compoundShape centerMassInertiaDisposes physicsEngine
        else PhysicsEngine3d.attachBodyBvhTriangles bodySource bodyProperties geometryShape compoundShape centerMassInertiaDisposes

    // TODO: add some error logging.
    static member private attachStaticModelShape bodySource (bodyProperties : BodyProperties) (staticModelShape : StaticModelShape) (compoundShape : CompoundShape) centerMassInertiaDisposes physicsEngine =
        match Metadata.tryGetStaticModelMetadata staticModelShape.StaticModel with
        | Some staticModel ->
            Seq.fold (fun centerMassInertiaDisposes i ->
                let surface = staticModel.Surfaces.[i]
                let transform =
                    match staticModelShape.TransformOpt with
                    | Some transform ->
                        Affine.make
                            (transform.Translation.Transform surface.SurfaceMatrix)
                            (transform.Rotation * surface.SurfaceMatrix.Rotation)
                            (transform.Scale.Transform surface.SurfaceMatrix)
                    | None -> Affine.makeFromMatrix surface.SurfaceMatrix
                let staticModelSurfaceShape = { StaticModel = staticModelShape.StaticModel; SurfaceIndex = i; Convex = staticModelShape.Convex; TransformOpt = Some transform; PropertiesOpt = staticModelShape.PropertiesOpt }
                PhysicsEngine3d.attachStaticModelShapeSurface bodySource bodyProperties staticModelSurfaceShape compoundShape centerMassInertiaDisposes physicsEngine)
                centerMassInertiaDisposes
                [0 .. dec staticModel.Surfaces.Length]
        | None -> centerMassInertiaDisposes

    // TODO: add some error logging.
    static member private attachStaticModelShapeSurface bodySource (bodyProperties : BodyProperties) (staticModelSurfaceShape : StaticModelSurfaceShape) (compoundShape : CompoundShape) centerMassInertiaDisposes physicsEngine =
        match Metadata.tryGetStaticModelMetadata staticModelSurfaceShape.StaticModel with
        | Some staticModel ->
            if  staticModelSurfaceShape.SurfaceIndex > -1 &&
                staticModelSurfaceShape.SurfaceIndex < staticModel.Surfaces.Length then
                let geometry = staticModel.Surfaces.[staticModelSurfaceShape.SurfaceIndex].PhysicallyBasedGeometry
                let geometryShape = { Vertices = geometry.Vertices; Convex = staticModelSurfaceShape.Convex; TransformOpt = staticModelSurfaceShape.TransformOpt; PropertiesOpt = staticModelSurfaceShape.PropertiesOpt }
                PhysicsEngine3d.attachGeometryShape bodySource bodyProperties geometryShape compoundShape centerMassInertiaDisposes physicsEngine
            else centerMassInertiaDisposes
        | None -> centerMassInertiaDisposes

    static member private attachTerrainShape tryGetAssetFilePath bodySource (bodyProperties : BodyProperties) (terrainShape : TerrainShape) (compoundShape : CompoundShape) centerMassInertiaDisposes =
        let resolution = terrainShape.Resolution
        let bounds = terrainShape.Bounds
        match HeightMap.tryGetMetadata tryGetAssetFilePath bounds v2One terrainShape.HeightMap with
        | Some heightMapMetadata ->
            let heights = Array.zeroCreate heightMapMetadata.HeightsNormalized.Length
            for i in 0 .. dec heightMapMetadata.HeightsNormalized.Length do
                heights.[i] <- heightMapMetadata.HeightsNormalized.[i] * bounds.Height
            let handle = GCHandle.Alloc (heights, GCHandleType.Pinned)
            try let positionsPtr = handle.AddrOfPinnedObject ()
                let terrain = new HeightfieldTerrainShape (resolution.X, resolution.Y, positionsPtr, 1.0f, 0.0f, bounds.Height, 1, PhyScalarType.Single, false)
                let scale = v3 (bounds.Width / single (dec resolution.X)) 1.0f (bounds.Depth / single (dec resolution.Y))
                terrain.LocalScaling <-
                    bodyProperties.Scale *
                    match terrainShape.TransformOpt with
                    | Some transform -> transform.Scale * scale
                    | None -> scale
                terrain.SetFlipTriangleWinding true // match terrain winding order - I think!
                PhysicsEngine3d.configureBodyShapeProperties bodyProperties terrainShape.PropertiesOpt terrain
                terrain.UserObject <-
                    { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
                      BodyShapeIndex = match terrainShape.PropertiesOpt with Some p -> p.BodyShapeIndex | None -> 0 }
                let center =
                    match terrainShape.TransformOpt with
                    | Some transform -> transform.Translation
                    | None -> v3Zero
                let mass = 0.0f // infinite mass
                let inertia = terrain.CalculateLocalInertia mass
                compoundShape.AddChildShape (Matrix4x4.CreateTranslation center, terrain)
                (center, mass, inertia, fun () -> handle.Free ()) :: centerMassInertiaDisposes
            with _ -> centerMassInertiaDisposes
        | None -> centerMassInertiaDisposes

    static member private attachBodyShapes tryGetAssetFilePath bodySource bodyProperties bodyShapes compoundShape centerMassInertiaDisposes physicsEngine =
        List.fold (fun centerMassInertiaDisposes bodyShape ->
            let centerMassInertiaDisposes' = PhysicsEngine3d.attachBodyShape tryGetAssetFilePath bodySource bodyProperties bodyShape compoundShape centerMassInertiaDisposes physicsEngine
            centerMassInertiaDisposes' @ centerMassInertiaDisposes)
            centerMassInertiaDisposes
            bodyShapes

    static member private attachBodyShape tryGetAssetFilePath bodySource bodyProperties bodyShape compoundShape centerMassInertiaDisposes physicsEngine =
        match bodyShape with
        | EmptyShape -> centerMassInertiaDisposes
        | BoxShape boxShape -> PhysicsEngine3d.attachBoxShape bodySource bodyProperties boxShape compoundShape centerMassInertiaDisposes
        | SphereShape sphereShape -> PhysicsEngine3d.attachSphereShape bodySource bodyProperties sphereShape compoundShape centerMassInertiaDisposes
        | CapsuleShape capsuleShape -> PhysicsEngine3d.attachCapsuleShape bodySource bodyProperties capsuleShape compoundShape centerMassInertiaDisposes
        | BoxRoundedShape boxRoundedShape -> PhysicsEngine3d.attachBoxRoundedShape bodySource bodyProperties boxRoundedShape compoundShape centerMassInertiaDisposes
        | PointsShape pointsShape -> PhysicsEngine3d.attachBodyConvexHull bodySource bodyProperties pointsShape compoundShape centerMassInertiaDisposes physicsEngine
        | GeometryShape geometryShape -> PhysicsEngine3d.attachGeometryShape bodySource bodyProperties geometryShape compoundShape centerMassInertiaDisposes physicsEngine
        | StaticModelShape staticModelShape -> PhysicsEngine3d.attachStaticModelShape bodySource bodyProperties staticModelShape compoundShape centerMassInertiaDisposes physicsEngine
        | StaticModelSurfaceShape staticModelSurfaceShape -> PhysicsEngine3d.attachStaticModelShapeSurface bodySource bodyProperties staticModelSurfaceShape compoundShape centerMassInertiaDisposes physicsEngine
        | TerrainShape terrainShape -> PhysicsEngine3d.attachTerrainShape tryGetAssetFilePath bodySource bodyProperties terrainShape compoundShape centerMassInertiaDisposes
        | BodyShapes bodyShapes -> PhysicsEngine3d.attachBodyShapes tryGetAssetFilePath bodySource bodyProperties bodyShapes compoundShape centerMassInertiaDisposes physicsEngine

    static member private createBody3 attachBodyShape (bodyId : BodyId) (bodyProperties : BodyProperties) physicsEngine =
        let (compoundShape, centerMassInertiaDisposes) =
            let compoundShape = new CompoundShape ()
            let centerMassInertiaDisposes = attachBodyShape bodyProperties compoundShape []
            (compoundShape, centerMassInertiaDisposes)
        let shape =
            if  compoundShape.ChildList.Count = 1 &&
                (compoundShape.ChildList.[0].Transform = m4Identity ||
                 bodyProperties.BodyType = KinematicCharacter) then // attempt to strip compound shape
                let shape = compoundShape.ChildList.[0].ChildShape
                compoundShape.RemoveChildShape shape
                compoundShape.Dispose ()
                shape
            else compoundShape
        let (_, mass, inertia, disposer) =
            // TODO: make this more accurate by making each c weighted proportionately to its respective m.
            List.fold (fun (c, m, i, d) (c', m', i', d') -> (c + c', m + m', i + i', fun () -> d (); d' ())) (v3Zero, 0.0f, v3Zero, id) centerMassInertiaDisposes
        let shapeSensorOpt = match bodyProperties.BodyShape.PropertiesOpt with Some properties -> properties.SensorOpt | None -> None
        let userIndex = if Option.defaultValue false shapeSensorOpt || bodyProperties.Sensor || bodyProperties.Observable then 1 else -1
        if bodyProperties.Sensor then
            let ghost = new GhostObject ()
            ghost.CollisionShape <- shape
            ghost.CollisionFlags <- ghost.CollisionFlags ||| CollisionFlags.NoContactResponse
            ghost.WorldTransform <- Matrix4x4.CreateFromTrs (bodyProperties.Center, bodyProperties.Rotation, bodyProperties.Scale)
            ghost.UserObject <- { BodyId = bodyId; Dispose = disposer }
            ghost.UserIndex <- userIndex
            PhysicsEngine3d.configureCollisionObjectProperties bodyProperties ghost
            physicsEngine.PhysicsContext.AddCollisionObject (ghost, bodyProperties.CollisionCategories, bodyProperties.CollisionMask)
            if physicsEngine.Ghosts.TryAdd (bodyId, ghost) then
                physicsEngine.Objects.Add (bodyId, ghost)
                ghost.Activate true // force activation since it seems to be unconditionally disabled somewhere in the above bullet processes
            else Log.error ("Could not add body for '" + scstring bodyId + "'.")
        elif bodyProperties.BodyType = KinematicCharacter then
            match shape with
            | :? ConvexShape as convexShape ->
                let shapeTransform =
                    match bodyProperties.BodyShape.TransformOpt with
                    | Some transform -> transform
                    | None -> Affine.Identity
                if shapeTransform.Rotation <> quatIdentity || shapeTransform.Scale <> v3One then
                    Log.info "Shape rotation / scale are not supported for KinematicCharacter."
                let ghost = new PairCachingGhostObject ()
                ghost.CollisionShape <- convexShape
                ghost.CollisionFlags <- ghost.CollisionFlags &&& ~~~CollisionFlags.NoContactResponse
                ghost.WorldTransform <- Matrix4x4.CreateFromTrs (bodyProperties.Center + shapeTransform.Translation, bodyProperties.Rotation, bodyProperties.Scale)
                ghost.UserObject <- { BodyId = bodyId; Dispose = disposer }
                ghost.UserIndex <- userIndex
                PhysicsEngine3d.configureCollisionObjectProperties bodyProperties ghost
                physicsEngine.PhysicsContext.AddCollisionObject (ghost, bodyProperties.CollisionCategories, bodyProperties.CollisionMask)
                let mutable up = v3Up
                let characterProperties = bodyProperties.CharacterProperties
                let characterController = new KinematicCharacterController (ghost, convexShape, characterProperties.StepHeight, &up)
                characterController.MaxPenetrationDepth <- characterProperties.PenetrationDepthMax
                characterController.MaxSlope <- characterProperties.SlopeMax
                characterController.Gravity <- Option.defaultValue physicsEngine.PhysicsContext.Gravity bodyProperties.GravityOverride
                physicsEngine.PhysicsContext.AddAction characterController
                let character =
                    { GravityOverride = bodyProperties.GravityOverride
                      CenterOffset = shapeTransform.Translation
                      Center = ghost.WorldTransform.Translation
                      Rotation = ghost.WorldTransform.Rotation
                      LinearVelocity = v3Zero
                      AngularVelocity = v3Zero
                      CharacterController = characterController
                      Ghost = ghost }
                if physicsEngine.KinematicCharacters.TryAdd (bodyId, character) then
                    physicsEngine.Objects.Add (bodyId, ghost)
                    ghost.Activate true // force activation since it seems to be unconditionally disabled somewhere in the above bullet processes
                else Log.error ("Could not add body for '" + scstring bodyId + "'.")
            | _ -> Log.info "Non-convex body shapes are unsupported for KinematicCharacter."
        else
            let constructionInfo = new RigidBodyConstructionInfo (mass, new DefaultMotionState (), shape, inertia)
            let body = new RigidBody (constructionInfo)
            body.WorldTransform <- Matrix4x4.CreateFromTrs (bodyProperties.Center, bodyProperties.Rotation, bodyProperties.Scale)
            body.MotionState.WorldTransform <- body.WorldTransform
            body.UserObject <- { BodyId = bodyId; Dispose = disposer }
            body.UserIndex <- userIndex
            PhysicsEngine3d.configureBodyProperties bodyProperties body physicsEngine.PhysicsContext.Gravity
            physicsEngine.PhysicsContext.AddRigidBody (body, bodyProperties.CollisionCategories, bodyProperties.CollisionMask)
            if physicsEngine.Bodies.TryAdd (bodyId, body) then
                match bodyProperties.BodyType with 
                | KinematicCharacter | Dynamic | DynamicCharacter ->
                    physicsEngine.BodiesGravitating.Add (bodyId, (bodyProperties.GravityOverride, body))
                | Static | Kinematic -> ()
                physicsEngine.Objects.Add (bodyId, body)
            else Log.error ("Could not add body for '" + scstring bodyId + "'.")

    static member private createBody4 bodyShape (bodyId : BodyId) bodyProperties physicsEngine =
        PhysicsEngine3d.createBody3 (fun ps cs cmas ->
            PhysicsEngine3d.attachBodyShape Metadata.tryGetFilePath bodyId.BodySource ps bodyShape cs cmas physicsEngine)
            bodyId bodyProperties physicsEngine

    static member private createBody (createBodyMessage : CreateBodyMessage) physicsEngine =

        // attempt to create body
        let bodyId = createBodyMessage.BodyId
        let bodyProperties = createBodyMessage.BodyProperties
        PhysicsEngine3d.createBody4 bodyProperties.BodyShape bodyId bodyProperties physicsEngine

        // attempt to run any related body joint creation functions
        match physicsEngine.CreateBodyJointMessages.TryGetValue bodyId with
        | (true, createBodyJointMessages) ->
            for createBodyJointMessage in createBodyJointMessages do
                let bodyJointId = { BodyJointSource = createBodyJointMessage.BodyJointSource; BodyJointIndex = createBodyJointMessage.BodyJointProperties.BodyJointIndex }
                PhysicsEngine3d.destroyBodyJointInternal bodyJointId physicsEngine
                PhysicsEngine3d.createBodyJointInternal createBodyJointMessage.BodyJointProperties bodyJointId physicsEngine
        | (false, _) -> ()

    static member private createBodies (createBodiesMessage : CreateBodiesMessage) physicsEngine =
        List.iter (fun (bodyProperties : BodyProperties) ->
            let createBodyMessage =
                { BodyId = { BodySource = createBodiesMessage.BodySource; BodyIndex = bodyProperties.BodyIndex }
                  BodyProperties = bodyProperties }
            PhysicsEngine3d.createBody createBodyMessage physicsEngine)
            createBodiesMessage.BodiesProperties

    static member private destroyBody (destroyBodyMessage : DestroyBodyMessage) physicsEngine =

        // attempt to run any related body joint destruction functions
        let bodyId = destroyBodyMessage.BodyId
        match physicsEngine.CreateBodyJointMessages.TryGetValue bodyId with
        | (true, createBodyJointMessages) ->
            for createBodyJointMessage in createBodyJointMessages do
                let bodyJointId = { BodyJointSource = createBodyJointMessage.BodyJointSource; BodyJointIndex = createBodyJointMessage.BodyJointProperties.BodyJointIndex }
                PhysicsEngine3d.destroyBodyJointInternal bodyJointId physicsEngine
        | (false, _) -> ()

        // attempt to destroy body
        match physicsEngine.Objects.TryGetValue bodyId with
        | (true, object) ->
            match object with
            | :? RigidBody as body ->
                physicsEngine.Objects.Remove bodyId |> ignore
                physicsEngine.BodiesGravitating.Remove bodyId |> ignore
                physicsEngine.Bodies.Remove bodyId |> ignore
                physicsEngine.PhysicsContext.RemoveRigidBody body
                let userObject = body.UserObject :?> BodyUserObject
                userObject.Dispose ()
            | :? GhostObject as ghost ->
                match physicsEngine.KinematicCharacters.TryGetValue bodyId with
                | (true, character) ->
                    physicsEngine.KinematicCharacters.Remove bodyId |> ignore
                    physicsEngine.PhysicsContext.RemoveAction character.CharacterController
                    character.CharacterController.Dispose ()
                | (false, _) -> ()
                physicsEngine.Ghosts.Remove bodyId |> ignore
                physicsEngine.Objects.Remove bodyId |> ignore
                physicsEngine.PhysicsContext.RemoveCollisionObject ghost
                let userObject = ghost.UserObject :?> BodyUserObject
                userObject.Dispose ()
            | _ -> ()
        | (false, _) -> ()

    static member private destroyBodies (destroyBodiesMessage : DestroyBodiesMessage) physicsEngine =
        List.iter (fun bodyId ->
            PhysicsEngine3d.destroyBody { BodyId = bodyId } physicsEngine)
            destroyBodiesMessage.BodyIds

    static member private createBodyJointInternal bodyJointProperties bodyJointId physicsEngine =
        match bodyJointProperties.BodyJoint with
        | EmptyJoint -> ()
        | _ ->
            let bodyId = bodyJointProperties.BodyJointTarget
            let body2Id = bodyJointProperties.BodyJointTarget2
            match (physicsEngine.Bodies.TryGetValue bodyId, physicsEngine.Bodies.TryGetValue body2Id) with
            | ((true, body), (true, body2)) ->
                let constrainOpt =
                    match bodyJointProperties.BodyJoint with
                    | EmptyJoint ->
                        failwithumf () // already checked
                    | AngleJoint angleJoint ->
                        let hinge = new HingeConstraint (body, body2, angleJoint.Anchor, angleJoint.Anchor2, angleJoint.Axis, angleJoint.Axis2, true)
                        hinge.SetLimit (angleJoint.Angle, angleJoint.Angle, angleJoint.Softness, angleJoint.BiasFactor, 1.0f)
                        Some (hinge :> TypedConstraint)
                    | DistanceJoint distanceJoint ->
                        let slider = new SliderConstraint (body, body2, Matrix4x4.CreateTranslation distanceJoint.Anchor, Matrix4x4.CreateTranslation distanceJoint.Anchor2, true)
                        slider.LowerLinearLimit <- distanceJoint.Length
                        slider.UpperLinearLimit <- distanceJoint.Length
                        Some slider
                    | HingeJoint hingeJoint ->
                        let hinge = new HingeConstraint (body, body2, hingeJoint.Anchor, hingeJoint.Anchor2, hingeJoint.Axis, hingeJoint.Axis2, true)
                        hinge.AngularOnly <- hingeJoint.AngularOnly
                        hinge.SetLimit (hingeJoint.AngleMin, hingeJoint.AngleMax, hingeJoint.Softness, hingeJoint.BiasFactor, hingeJoint.RelaxationFactor)
                        Some (hinge :> TypedConstraint)
                    | SliderJoint sliderJoint ->
                        let frameInA = Matrix4x4.CreateFromTrs (sliderJoint.Anchor, Quaternion.CreateFromYawPitchRoll (sliderJoint.Axis.Y, sliderJoint.Axis.X, sliderJoint.Axis.Z), v3One)
                        let frameInB = Matrix4x4.CreateFromTrs (sliderJoint.Anchor2, Quaternion.CreateFromYawPitchRoll (sliderJoint.Axis2.Y, sliderJoint.Axis2.X, sliderJoint.Axis2.Z), v3One)
                        let slider = new SliderConstraint (body, body2, frameInA, frameInB, true)
                        slider.LowerLinearLimit <- sliderJoint.LinearLimitLower
                        slider.UpperLinearLimit <- sliderJoint.LinearLimitUpper
                        slider.LowerAngularLimit <- sliderJoint.AngularLimitLower
                        slider.UpperAngularLimit <- sliderJoint.AngularLimitUpper
                        slider.SoftnessDirLinear <- sliderJoint.DirectionLinearSoftness
                        slider.RestitutionDirLinear <- sliderJoint.DirectionLinearRestitution
                        slider.DampingDirLinear <- sliderJoint.DirectionLinearDamping
                        slider.SoftnessDirAngular <- sliderJoint.DirectionAngularSoftness
                        slider.RestitutionDirAngular <- sliderJoint.DirectionAngularRestitution
                        slider.DampingDirAngular <- sliderJoint.DirectionAngularDamping
                        slider.SoftnessLimLinear <- sliderJoint.LimitLinearSoftness
                        slider.RestitutionLimLinear <- sliderJoint.LimitLinearRestitution
                        slider.DampingLimLinear <- sliderJoint.LimitLinearDamping
                        slider.SoftnessLimAngular <- sliderJoint.LimitAngularSoftness
                        slider.RestitutionLimAngular <- sliderJoint.LimitAngularRestitution
                        slider.DampingLimAngular <- sliderJoint.LimitAngularDamping
                        slider.SoftnessOrthoLinear <- sliderJoint.OrthoLinearSoftness
                        slider.RestitutionOrthoLinear <- sliderJoint.OrthoLinearRestitution
                        slider.DampingOrthoLinear <- sliderJoint.OrthoLinearDamping
                        slider.SoftnessOrthoAngular <- sliderJoint.OrthoAngularSoftness
                        slider.RestitutionOrthoAngular <- sliderJoint.OrthoAngularRestitution
                        slider.DampingOrthoAngular <- sliderJoint.OrthoAngularDamping
                        Some slider
                    | UserDefinedBulletJoint bulletJoint ->
                        Some (bulletJoint.CreateBodyJoint body body2)
                    | _ ->
                        Log.warn ("Joint type '" + getCaseName bodyJointProperties.BodyJoint + "' not implemented for PhysicsEngine3d.")
                        None
                match constrainOpt with
                | Some constrain ->
                    constrain.BreakingImpulseThreshold <- bodyJointProperties.BreakImpulseThreshold
                    // TODO: implement CollideConnected.
                    constrain.IsEnabled <- bodyJointProperties.BodyJointEnabled
                    body.Activate true
                    body2.Activate true
                    physicsEngine.PhysicsContext.AddConstraint (constrain, false)
                    if physicsEngine.Constraints.TryAdd (bodyJointId, constrain)
                    then () // nothing to do
                    else Log.warn ("Could not add joint for '" + scstring bodyJointId + "'.")
                | None -> ()
            | (_, _) -> ()

    static member private createBodyJoint (createBodyJointMessage : CreateBodyJointMessage) physicsEngine =

        // log creation message
        for bodyTarget in [createBodyJointMessage.BodyJointProperties.BodyJointTarget; createBodyJointMessage.BodyJointProperties.BodyJointTarget2] do
            match physicsEngine.CreateBodyJointMessages.TryGetValue bodyTarget with
            | (true, messages) -> messages.Add createBodyJointMessage
            | (false, _) -> physicsEngine.CreateBodyJointMessages.Add (bodyTarget, List [createBodyJointMessage])

        // attempt to add body joint
        let bodyJointId = { BodyJointSource = createBodyJointMessage.BodyJointSource; BodyJointIndex = createBodyJointMessage.BodyJointProperties.BodyJointIndex }
        PhysicsEngine3d.createBodyJointInternal createBodyJointMessage.BodyJointProperties bodyJointId physicsEngine

    static member private destroyBodyJointInternal (bodyJointId : BodyJointId) physicsEngine =
        match physicsEngine.Constraints.TryGetValue bodyJointId with
        | (true, contrain) ->
            physicsEngine.Constraints.Remove bodyJointId |> ignore
            physicsEngine.PhysicsContext.RemoveConstraint contrain
        | (false, _) -> ()

    static member private destroyBodyJoint (destroyBodyJointMessage : DestroyBodyJointMessage) physicsEngine =

        // unlog creation message
        for bodyTarget in [destroyBodyJointMessage.BodyJointTarget; destroyBodyJointMessage.BodyJointTarget2] do
            match physicsEngine.CreateBodyJointMessages.TryGetValue bodyTarget with
            | (true, messages) ->
                messages.RemoveAll (fun message ->
                    message.BodyJointSource = destroyBodyJointMessage.BodyJointId.BodyJointSource &&
                    message.BodyJointProperties.BodyJointIndex = destroyBodyJointMessage.BodyJointId.BodyJointIndex) |>
                ignore<int>
            | (false, _) -> ()

        // attempt to destroy body joint
        PhysicsEngine3d.destroyBodyJointInternal destroyBodyJointMessage.BodyJointId physicsEngine

    static member private setBodyEnabled (setBodyEnabledMessage : SetBodyEnabledMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyEnabledMessage.BodyId with
        | (true, object) ->
                if setBodyEnabledMessage.Enabled then
                    object.ActivationState <- object.ActivationState ||| ActivationState.ActiveTag
                    object.ActivationState <- object.ActivationState &&& ~~~ActivationState.DisableSimulation
                else
                    object.ActivationState <- object.ActivationState ||| ActivationState.DisableSimulation
                    object.ActivationState <- object.ActivationState &&& ~~~ActivationState.ActiveTag
        | (false, _) -> ()

    static member private setBodyCenter (setBodyCenterMessage : SetBodyCenterMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyCenterMessage.BodyId with
        | (true, object) ->
            let translation =
                match physicsEngine.KinematicCharacters.TryGetValue setBodyCenterMessage.BodyId with
                | (true, character) -> setBodyCenterMessage.Center + character.CenterOffset
                | (false, _) -> setBodyCenterMessage.Center
            if object.WorldTransform.Translation <> translation then
                let mutable transform = object.WorldTransform
                transform.Translation <- translation
                object.WorldTransform <- transform
                match object with
                | :? RigidBody as body -> body.MotionState.WorldTransform <- object.WorldTransform
                | _ -> ()
                object.Activate true // force activation so that a transform message will be produced
        | (false, _) -> ()

    static member private setBodyRotation (setBodyRotationMessage : SetBodyRotationMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyRotationMessage.BodyId with
        | (true, object) ->
            let transform = object.WorldTransform.SetRotation setBodyRotationMessage.Rotation
            if object.WorldTransform <> transform then
                object.WorldTransform <- object.WorldTransform.SetRotation setBodyRotationMessage.Rotation
                match object with
                | :? RigidBody as body -> body.MotionState.WorldTransform <- object.WorldTransform
                | _ -> ()
                object.Activate true // force activation so that a transform message will be produced
        | (false, _) -> ()

    static member private setBodyLinearVelocity (setBodyLinearVelocityMessage : SetBodyLinearVelocityMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyLinearVelocityMessage.BodyId with
        | (true, (:? RigidBody as body)) ->
            body.LinearVelocity <- setBodyLinearVelocityMessage.LinearVelocity
            body.Activate ()
        | (_, _) ->
            match physicsEngine.KinematicCharacters.TryGetValue setBodyLinearVelocityMessage.BodyId with
            | (true, character) ->
                character.LinearVelocity <- setBodyLinearVelocityMessage.LinearVelocity
                character.Ghost.Activate ()
            | (false, _) -> ()

    static member private setBodyAngularVelocity (setBodyAngularVelocityMessage : SetBodyAngularVelocityMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyAngularVelocityMessage.BodyId with
        | (true, (:? RigidBody as body)) ->
            body.AngularVelocity <- setBodyAngularVelocityMessage.AngularVelocity
            body.Activate ()
        | (_, _) ->
            match physicsEngine.KinematicCharacters.TryGetValue setBodyAngularVelocityMessage.BodyId with
            | (true, character) ->
                character.AngularVelocity <- setBodyAngularVelocityMessage.AngularVelocity
                character.Ghost.Activate ()
            | (false, _) -> ()

    static member private applyBodyLinearImpulse (applyBodyLinearImpulseMessage : ApplyBodyLinearImpulseMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyLinearImpulseMessage.BodyId with
        | (true, (:? RigidBody as body)) ->
            if not (Single.IsNaN applyBodyLinearImpulseMessage.LinearImpulse.X) then
                body.ApplyImpulse (applyBodyLinearImpulseMessage.LinearImpulse, applyBodyLinearImpulseMessage.Offset)
                body.Activate ()
            else Log.info ("Applying invalid linear impulse '" + scstring applyBodyLinearImpulseMessage.LinearImpulse + "'; this may destabilize Bullet.")
        | (_, _) -> ()

    static member private applyBodyAngularImpulse (applyBodyAngularImpulseMessage : ApplyBodyAngularImpulseMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyAngularImpulseMessage.BodyId with
        | (true, (:? RigidBody as body)) ->
            if not (Single.IsNaN applyBodyAngularImpulseMessage.AngularImpulse.X) then
                body.ApplyTorqueImpulse (applyBodyAngularImpulseMessage.AngularImpulse)
                body.Activate ()
            else Log.info ("Applying invalid angular impulse '" + scstring applyBodyAngularImpulseMessage.AngularImpulse + "'; this may destabilize Bullet.")
        | (_, _) -> ()

    static member private applyBodyForce (applyBodyForceMessage : ApplyBodyForceMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyForceMessage.BodyId with
        | (true, (:? RigidBody as body)) ->
            if not (Single.IsNaN applyBodyForceMessage.Force.X) then
                body.ApplyForce (applyBodyForceMessage.Force, applyBodyForceMessage.Offset)
                body.Activate ()
            else Log.info ("Applying invalid force '" + scstring applyBodyForceMessage.Force + "'; this may destabilize Bullet.")
        | (_, _) -> ()

    static member private applyBodyTorque (applyBodyTorqueMessage : ApplyBodyTorqueMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyTorqueMessage.BodyId with
        | (true, (:? RigidBody as body)) ->
            if not (Single.IsNaN applyBodyTorqueMessage.Torque.X) then
                body.ApplyTorque applyBodyTorqueMessage.Torque
                body.Activate ()
            else Log.info ("Applying invalid torque '" + scstring applyBodyTorqueMessage.Torque + "'; this may destabilize Bullet.")
        | (_, _) -> ()

    static member private jumpBody (jumpBodyMessage : JumpBodyMessage) physicsEngine =
        match physicsEngine.KinematicCharacters.TryGetValue jumpBodyMessage.BodyId with
        | (true, character) ->
            if jumpBodyMessage.CanJumpInAir || character.CharacterController.OnGround then
                character.CharacterController.JumpSpeed <- jumpBodyMessage.JumpSpeed
                character.CharacterController.Jump ()
        | (false, _) -> ()

    static member private handlePhysicsMessage physicsEngine physicsMessage =
        match physicsMessage with
        | CreateBodyMessage createBodyMessage -> PhysicsEngine3d.createBody createBodyMessage physicsEngine
        | CreateBodiesMessage createBodiesMessage -> PhysicsEngine3d.createBodies createBodiesMessage physicsEngine
        | DestroyBodyMessage destroyBodyMessage -> PhysicsEngine3d.destroyBody destroyBodyMessage physicsEngine
        | DestroyBodiesMessage destroyBodiesMessage -> PhysicsEngine3d.destroyBodies destroyBodiesMessage physicsEngine
        | CreateBodyJointMessage createBodyJointMessage -> PhysicsEngine3d.createBodyJoint createBodyJointMessage physicsEngine
        | DestroyBodyJointMessage destroyBodyJointMessage -> PhysicsEngine3d.destroyBodyJoint destroyBodyJointMessage physicsEngine
        | SetBodyEnabledMessage setBodyEnabledMessage -> PhysicsEngine3d.setBodyEnabled setBodyEnabledMessage physicsEngine
        | SetBodyCenterMessage setBodyCenterMessage -> PhysicsEngine3d.setBodyCenter setBodyCenterMessage physicsEngine
        | SetBodyRotationMessage setBodyRotationMessage -> PhysicsEngine3d.setBodyRotation setBodyRotationMessage physicsEngine
        | SetBodyLinearVelocityMessage setBodyLinearVelocityMessage -> PhysicsEngine3d.setBodyLinearVelocity setBodyLinearVelocityMessage physicsEngine
        | SetBodyAngularVelocityMessage setBodyAngularVelocityMessage -> PhysicsEngine3d.setBodyAngularVelocity setBodyAngularVelocityMessage physicsEngine
        | ApplyBodyLinearImpulseMessage applyBodyLinearImpulseMessage -> PhysicsEngine3d.applyBodyLinearImpulse applyBodyLinearImpulseMessage physicsEngine
        | ApplyBodyAngularImpulseMessage applyBodyAngularImpulseMessage -> PhysicsEngine3d.applyBodyAngularImpulse applyBodyAngularImpulseMessage physicsEngine
        | ApplyBodyForceMessage applyBodyForceMessage -> PhysicsEngine3d.applyBodyForce applyBodyForceMessage physicsEngine
        | ApplyBodyTorqueMessage applyBodyTorqueMessage -> PhysicsEngine3d.applyBodyTorque applyBodyTorqueMessage physicsEngine
        | JumpBodyMessage jumpBodyMessage -> PhysicsEngine3d.jumpBody jumpBodyMessage physicsEngine
        | SetGravityMessage gravity ->

            // set gravity of all gravitating bodies
            physicsEngine.PhysicsContext.Gravity <- gravity
            for bodyEntry in physicsEngine.BodiesGravitating do
                let (gravityOverride, body) = bodyEntry.Value
                match gravityOverride with
                | Some gravity -> body.Gravity <- gravity
                | None -> body.Gravity <- gravity

            // set gravity of all kinematic characters
            for characterEntry in physicsEngine.KinematicCharacters do
                let character = characterEntry.Value
                match character.GravityOverride with
                | Some gravity -> character.CharacterController.Gravity <- gravity
                | None -> character.CharacterController.Gravity <- gravity

        | ClearPhysicsMessageInternal ->

            // collect body user objects as we proceed
            let bodyUserObjects = List ()

            // destroy constraints
            for constrain in physicsEngine.Constraints.Values do
                physicsEngine.PhysicsContext.RemoveConstraint constrain
            physicsEngine.Constraints.Clear ()

            // destroy bullet objects
            for object in physicsEngine.Objects.Values do
                bodyUserObjects.Add (object.UserObject :?> BodyUserObject)
            physicsEngine.Objects.Clear ()

            // destroy ghosts
            for ghost in physicsEngine.Ghosts.Values do
                bodyUserObjects.Add (ghost.UserObject :?> BodyUserObject)
                physicsEngine.PhysicsContext.RemoveCollisionObject ghost
            physicsEngine.Ghosts.Clear ()

            // destroy kinematic characters
            for character in physicsEngine.KinematicCharacters.Values do
                bodyUserObjects.Add (character.Ghost.UserObject :?> BodyUserObject)
                physicsEngine.PhysicsContext.RemoveCollisionObject character.Ghost
                physicsEngine.PhysicsContext.RemoveAction character.CharacterController
                character.CharacterController.Dispose ()
            physicsEngine.KinematicCharacters.Clear ()

            // clear gravitating bodies
            physicsEngine.BodiesGravitating.Clear ()

            // destroy bodies
            for body in physicsEngine.Bodies.Values do
                physicsEngine.PhysicsContext.RemoveRigidBody body
            physicsEngine.Bodies.Clear ()

            // dispose body user objects
            for bodyUserObject in bodyUserObjects do
                bodyUserObject.Dispose ()

            // clear integration messages
            physicsEngine.IntegrationMessages.Clear ()

    static member private tryIntegrate stepTime physicsEngine =        
        match (Constants.GameTime.DesiredFrameRate, stepTime) with
        | (StaticFrameRate frameRate, UpdateTime frames) ->
            let physicsStepAmount = 1.0f / single frameRate * single frames
            if physicsStepAmount > 0.0f then
                let stepsTaken = physicsEngine.PhysicsContext.StepSimulation (physicsStepAmount, 16, 1.0f / single (frameRate * 2L))
                ignore stepsTaken
                true
            else false
        | (DynamicFrameRate _, ClockTime physicsStepAmount) ->
            if physicsStepAmount > 0.0f then
                // The following line is what Bullet seems to recommend (https://pybullet.org/Bullet/phpBB3/viewtopic.php?t=2438) -
                //let stepsTaken = physicsEngine.PhysicsContext.StepSimulation (physicsStepAmount, 16, 1.0f / 120.0f)
                // However, the following line of code seems to give smoother results -
                let stepsTaken = physicsEngine.PhysicsContext.StepSimulation (physicsStepAmount, 2, physicsStepAmount / 2.0f - 0.0001f)
                ignore stepsTaken
                true
            else false
        | (_, _) -> failwithumf ()

    static member private createIntegrationMessages physicsEngine =

        // create collision entries
        let collisionsOld = physicsEngine.CollisionsFiltered
        physicsEngine.CollisionsFiltered <- SDictionary.make HashIdentity.Structural
        physicsEngine.CollisionsGround.Clear ()
        let numManifolds = physicsEngine.PhysicsContext.Dispatcher.NumManifolds
        for i in 0 .. dec numManifolds do

            // ensure at least ONE contact point is either intersecting or touching by checking distance.
            // this will filter out manifolds contacting only on the broadphase level according to -
            // https://github.com/timbeaudet/knowledge_base/blob/main/issues/bullet_contact_report_issue.md
            let manifold = physicsEngine.PhysicsContext.Dispatcher.GetManifoldByIndexInternal i
            let mutable intersecting = false
            let mutable j = 0
            while not intersecting && j < manifold.NumContacts do
                let pt = manifold.GetContactPoint j
                if pt.Distance <= 0.0f
                then intersecting <- true
                else j <- inc j
            if intersecting then

                // create non-ground collision entry if unfiltered
                let body0 = manifold.Body0
                let body1 = manifold.Body1
                let body0Source = (body0.UserObject :?> BodyUserObject).BodyId
                let body1Source = (body1.UserObject :?> BodyUserObject).BodyId
                let collisionKey = (body0Source, body1Source)
                let mutable normal = v3Zero
                let numContacts = manifold.NumContacts
                for j in 0 .. dec numContacts do
                    let contact = manifold.GetContactPoint j
                    normal <- normal - contact.NormalWorldOnB
                normal <- normal / single numContacts
                if  body0.UserIndex = 1 ||
                    body1.UserIndex = 1 then
                    physicsEngine.CollisionsFiltered.[collisionKey] <- normal // NOTE: incoming collision keys are not necessarily unique.

                // create ground collision entry for body0 if needed
                normal <- -normal
                let theta = normal.Dot Vector3.UnitY |> acos |> abs
                if theta < Constants.Physics.GroundAngleMax then
                    match physicsEngine.CollisionsGround.TryGetValue body0Source with
                    | (true, collisions) -> collisions.Add normal
                    | (false, _) -> physicsEngine.CollisionsGround.Add (body0Source, List [normal])

                // create ground collision entry for body1 if needed
                normal <- -normal
                let theta = -theta
                if theta < Constants.Physics.GroundAngleMax then
                    match physicsEngine.CollisionsGround.TryGetValue body1Source with
                    | (true, collisions) -> collisions.Add normal
                    | (false, _) -> physicsEngine.CollisionsGround.Add (body1Source, List [normal])

        // create penetration messages
        for entry in physicsEngine.CollisionsFiltered do
            let (bodySourceA, bodySourceB) = entry.Key
            if not (collisionsOld.ContainsKey entry.Key) then
                PhysicsEngine3d.handlePenetration physicsEngine bodySourceA bodySourceB entry.Value
                PhysicsEngine3d.handlePenetration physicsEngine bodySourceB bodySourceA -entry.Value

        // create separation messages
        for entry in collisionsOld do
            let (bodySourceA, bodySourceB) = entry.Key
            if  not (physicsEngine.CollisionsFiltered.ContainsKey entry.Key) &&
                physicsEngine.Objects.ContainsKey bodySourceA &&
                physicsEngine.Objects.ContainsKey bodySourceB then
                PhysicsEngine3d.handleSeparation physicsEngine bodySourceA bodySourceB
                PhysicsEngine3d.handleSeparation physicsEngine bodySourceB bodySourceA

        // create gravitating body transform messages
        for bodyEntry in physicsEngine.BodiesGravitating do
            let (_, body) = bodyEntry.Value
            if body.IsActive then
                let bodyTransformMessage =
                    BodyTransformMessage
                        { BodyId = (body.UserObject :?> BodyUserObject).BodyId
                          Center = body.WorldTransform.Translation
                          Rotation = body.WorldTransform.Rotation
                          LinearVelocity = body.LinearVelocity
                          AngularVelocity = body.AngularVelocity }
                physicsEngine.IntegrationMessages.Add bodyTransformMessage

        // create kinematic character transform messages
        for characterEntry in physicsEngine.KinematicCharacters do
            let character = characterEntry.Value
            if character.Ghost.IsActive then
                let center = character.Ghost.WorldTransform.Translation
                let forward = character.Ghost.WorldTransform.Rotation.Forward
                let sign = if v3Up.Dot (forward.Cross character.Rotation.Forward) < 0.0f then 1.0f else -1.0f
                let angleBetweenOpt = forward.AngleBetween character.Rotation.Forward
                character.LinearVelocity <- center - character.Center
                character.AngularVelocity <- if Single.IsNaN angleBetweenOpt then v3Zero else v3 0.0f (angleBetweenOpt * sign) 0.0f
                character.Center <- center
                character.Rotation <- character.Ghost.WorldTransform.Rotation
                let bodyTransformMessage =
                    BodyTransformMessage
                        { BodyId = (character.Ghost.UserObject :?> BodyUserObject).BodyId
                          Center = character.Center - character.CenterOffset
                          Rotation = character.Rotation
                          LinearVelocity = character.LinearVelocity
                          AngularVelocity = character.AngularVelocity }
                physicsEngine.IntegrationMessages.Add bodyTransformMessage

    static member make gravity =
        use collisionConfigurationInfo = new DefaultCollisionConstructionInfo ()
        let collisionConfiguration = new DefaultCollisionConfiguration (collisionConfigurationInfo)
        let collisionDispatcher = new CollisionDispatcher (collisionConfiguration)
        let broadPhaseInterface = new DbvtBroadphase ()
        let constraintSolver = new SequentialImpulseConstraintSolver ()
        let world = new DiscreteDynamicsWorld (collisionDispatcher, broadPhaseInterface, constraintSolver, collisionConfiguration)
        let ghostPairCallback = new GhostPairCallback ()
        world.Broadphase.OverlappingPairCache.SetInternalGhostPairCallback ghostPairCallback
        world.DispatchInfo.AllowedCcdPenetration <- Constants.Physics.AllowedCcdPenetration3d
        world.Gravity <- gravity
        let physicsEngine =
            { PhysicsContext = world
              Constraints = Dictionary HashIdentity.Structural
              Bodies = Dictionary HashIdentity.Structural
              BodiesGravitating = Dictionary HashIdentity.Structural
              Objects = Dictionary HashIdentity.Structural
              Ghosts = Dictionary HashIdentity.Structural
              KinematicCharacters = Dictionary HashIdentity.Structural
              CollisionsFiltered = SDictionary.make HashIdentity.Structural
              CollisionsGround = dictPlus HashIdentity.Structural []
              CollisionConfiguration = collisionConfiguration
              CollisionDispatcher = collisionDispatcher
              BroadPhaseInterface = broadPhaseInterface
              ConstraintSolver = constraintSolver
              GhostPairCallback = ghostPairCallback
              UnscaledPointsCached = dictPlus UnscaledPointsKey.comparer []
              CreateBodyJointMessages = Dictionary<BodyId, CreateBodyJointMessage List> HashIdentity.Structural
              IntegrationMessages = List () }
        physicsEngine

    static member cleanUp physicsEngine =
        physicsEngine.PhysicsContext.Dispose ()
        physicsEngine.GhostPairCallback.Dispose ()
        physicsEngine.BroadPhaseInterface.Dispose ()
        physicsEngine.CollisionDispatcher.Dispose ()
        physicsEngine.CollisionConfiguration.Dispose ()

    interface PhysicsEngine with

        member physicsEngine.GetBodyExists bodyId =
            physicsEngine.Objects.ContainsKey bodyId

        member physicsEngine.GetBodyContactNormals bodyId =
            [for collision in physicsEngine.CollisionsFiltered do
                let (body0, body1) = collision.Key
                if body0 = bodyId then -collision.Value
                elif body1 = bodyId then collision.Value]

        member physicsEngine.GetBodyLinearVelocity bodyId =
            match physicsEngine.Bodies.TryGetValue bodyId with
            | (true, body) -> body.LinearVelocity
            | (false, _) ->
                if physicsEngine.Ghosts.ContainsKey bodyId then v3Zero
                else
                    match physicsEngine.KinematicCharacters.TryGetValue bodyId with
                    | (true, character) -> character.LinearVelocity
                    | (false, _) -> failwith ("No body with BodyId = " + scstring bodyId + ".")

        member physicsEngine.GetBodyAngularVelocity bodyId =
            match physicsEngine.Bodies.TryGetValue bodyId with
            | (true, body) -> body.AngularVelocity
            | (false, _) ->
                if physicsEngine.Ghosts.ContainsKey bodyId then v3Zero
                else
                    match physicsEngine.KinematicCharacters.TryGetValue bodyId with
                    | (true, character) -> character.AngularVelocity
                    | (false, _) -> failwith ("No body with BodyId = " + scstring bodyId + ".")

        member physicsEngine.GetBodyToGroundContactNormals bodyId =
            match physicsEngine.CollisionsGround.TryGetValue bodyId with
            | (true, collisions) -> List.ofSeq collisions
            | (false, _) -> []

        member physicsEngine.GetBodyToGroundContactNormalOpt bodyId =
            let groundNormals = (physicsEngine :> PhysicsEngine).GetBodyToGroundContactNormals bodyId
            match groundNormals with
            | [] -> None
            | _ ->
                let averageNormal = List.reduce (fun normal normal2 -> (normal + normal2) * 0.5f) groundNormals
                Some averageNormal

        member physicsEngine.GetBodyToGroundContactTangentOpt bodyId =
            match (physicsEngine :> PhysicsEngine).GetBodyToGroundContactNormalOpt bodyId with
            | Some normal -> Some (Vector3.Cross (v3Forward, normal))
            | None -> None

        member physicsEngine.GetBodyGrounded bodyId =
            match physicsEngine.KinematicCharacters.TryGetValue bodyId with
            | (true, character) -> character.CharacterController.OnGround
            | (false, _) ->
                let groundNormals = (physicsEngine :> PhysicsEngine).GetBodyToGroundContactNormals bodyId
                List.notEmpty groundNormals

        member physicsEngine.RayCast (start, stop, collisionCategories, collisionMask, closestOnly) =
            let mutable start = start
            let mutable stop = stop
            use rrc =
                if closestOnly
                then new ClosestRayResultCallback (&start, &stop) :> RayResultCallback
                else new AllHitsRayResultCallback (start, stop)
            rrc.CollisionFilterGroup <- collisionCategories
            rrc.CollisionFilterMask <- collisionMask
            physicsEngine.PhysicsContext.RayTest (start, stop, rrc)
            if rrc.HasHit then
                match rrc with
                | :? ClosestRayResultCallback as crrc ->
                    [|match crrc.CollisionObject.UserObject with
                      | :? BodyUserObject as bodyUserObject ->
                        match crrc.CollisionObject.CollisionShape.UserObject with
                        | :? BodyShapeIndex as shapeIndex -> (crrc.HitPointWorld, crrc.HitNormalWorld, crrc.ClosestHitFraction, shapeIndex, bodyUserObject.BodyId)
                        | _ -> failwithumf ()
                      | _ -> failwithumf ()|]
                | :? AllHitsRayResultCallback as ahrrc ->
                    [|for i in 0 .. dec ahrrc.CollisionObjects.Count do
                        let collisionObject = ahrrc.CollisionObjects.[i]
                        let hitPointWorld = ahrrc.HitPointWorld.[i]
                        let hitNormalWorld = ahrrc.HitNormalWorld.[i]
                        let hitFraction = ahrrc.HitFractions.[i]
                        match collisionObject.UserObject with
                        | :? BodyUserObject as bodyUserObject ->
                            match collisionObject.CollisionShape.UserObject with
                            | :? BodyShapeIndex as shapeIndex -> (hitPointWorld, hitNormalWorld, hitFraction, shapeIndex, bodyUserObject.BodyId)
                            | _ -> failwithumf ()
                        | _ -> failwithumf ()|]
                | _ -> failwithumf ()
            else [||]

        member physicsEngine.HandleMessage physicsMessage =
            PhysicsEngine3d.handlePhysicsMessage physicsEngine physicsMessage

        member physicsEngine.TryIntegrate stepTime =
            if PhysicsEngine3d.tryIntegrate stepTime physicsEngine then
                PhysicsEngine3d.createIntegrationMessages physicsEngine
                let integrationMessages = SArray.ofSeq physicsEngine.IntegrationMessages
                physicsEngine.IntegrationMessages.Clear ()
                Some integrationMessages
            else None

        member physicsEngine.CleanUp () =
            PhysicsEngine3d.cleanUp physicsEngine