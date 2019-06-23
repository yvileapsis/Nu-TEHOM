﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2018.

namespace Nu
open System
open Prime
open Nu

// TODO: P1: remove some of the code duplication in here.

/// A marker interface for simulant descriptors.
type SimulantDescriptor =
    interface
        abstract Children : SimulantDescriptor list
        end

/// Describes a game value independent of the engine.
type [<NoComparison>] GameDescriptor =
    { GameDispatcherName : string
      GameProperties : Map<string, Symbol>
      Screens : ScreenDescriptor list }

    /// The empty game descriptor.
    static member empty =
        { GameDispatcherName = String.Empty
          GameProperties = Map.empty
          Screens = [] }

    interface SimulantDescriptor with
        member this.Children =
            this.Screens |> enumerable<SimulantDescriptor> |> List.ofSeq

/// Describes a screen value independent of the engine.
and [<NoComparison>] ScreenDescriptor =
    { ScreenDispatcherName : string
      ScreenProperties : Map<string, Symbol>
      Layers : LayerDescriptor list }

    /// The empty screen descriptor.
    static member empty =
        { ScreenDispatcherName = String.Empty
          ScreenProperties = Map.empty
          Layers = [] }

    /// Derive a name from the dispatcher.
    static member getNameOpt dispatcher =
        dispatcher.ScreenProperties |>
        Map.tryFind (Property? Name) |>
        Option.map symbolToValue<string>
          
    interface SimulantDescriptor with
        member this.Children =
            this.Layers |> enumerable<SimulantDescriptor> |> List.ofSeq

/// Describes a layer value independent of the engine.
and [<NoComparison>] LayerDescriptor =
    { LayerDispatcherName : string
      LayerProperties : Map<string, Symbol>
      Entities : EntityDescriptor list }

    /// The empty layer descriptor.
    static member empty =
        { LayerDispatcherName = String.Empty
          LayerProperties = Map.empty
          Entities = [] }

    /// Derive a name from the dispatcher.
    static member getNameOpt dispatcher =
        dispatcher.LayerProperties |>
        Map.tryFind (Property? Name) |>
        Option.map symbolToValue<string>

    interface SimulantDescriptor with
        member this.Children =
            this.Entities |> enumerable<SimulantDescriptor> |> List.ofSeq

/// Describes an entity value independent of the engine.
and [<NoComparison>] EntityDescriptor =
    { EntityDispatcherName : string
      EntityProperties : Map<string, Symbol> }

    /// The empty entity descriptor.
    static member empty =
        { EntityDispatcherName = String.Empty
          EntityProperties = Map.empty }

    /// Derive a name from the dispatcher.
    static member getNameOpt dispatcher =
        dispatcher.EntityProperties |>
        Map.tryFind (Property? Name) |>
        Option.map symbolToValue<string>

    interface SimulantDescriptor with
        member this.Children = []

type [<NoEquality; NoComparison>] DescriptiveDefinition =
    | PropertyDefinition of PropertyDefinition
    | Equate of string * World Lens * bool

/// Contains primitives for describing simulants.
module Describe =

    /// Describe a game with the given definitions and contained screens.
    let game3 dispatcherName (definitions : DescriptiveDefinition seq) (screens : ScreenDescriptor seq) (game : Game) world =
        let properties =
            definitions |>
            Seq.map (fun def -> match def with PropertyDefinition def -> Some (def.PropertyName, def.PropertyExpr) | Equate _ -> None) |>
            Seq.definitize |>
            Seq.map (mapSnd (function DefineExpr value -> value | VariableExpr fn -> fn world)) |>
            Seq.map (mapSnd valueToSymbol) |>
            Map.ofSeq
        let equations =
            definitions |>
            Seq.map (fun def -> match def with Equate (leftName, right, breaking) -> Some (leftName, game :> Simulant, right, breaking) | PropertyDefinition _ -> None) |>
            Seq.definitize |>
            Seq.toList
        let descriptor =
            { GameDispatcherName = dispatcherName
              GameProperties = properties
              Screens = List.ofSeq screens }
        (descriptor, equations)

    /// Describe a game with the given definitions and contained screens.
    let game<'d when 'd :> GameDispatcher> properties screens game world =
        game3 typeof<'d>.Name properties screens game world

    /// Describe a screen with the given definitions and contained layers.
    let screen3 dispatcherName (definitions : DescriptiveDefinition seq) (layers : LayerDescriptor seq) (screen : Screen) world =
        let properties =
            definitions |>
            Seq.map (fun def -> match def with PropertyDefinition def -> Some (def.PropertyName, def.PropertyExpr) | Equate _ -> None) |>
            Seq.definitize |>
            Seq.map (mapSnd (function DefineExpr value -> value | VariableExpr fn -> fn world)) |>
            Seq.map (mapSnd valueToSymbol) |>
            Map.ofSeq
        let equations =
            definitions |>
            Seq.map (fun def -> match def with Equate (leftName, right, breaking) -> Some (leftName, screen :> Simulant, right, breaking) | PropertyDefinition _ -> None) |>
            Seq.definitize |>
            Seq.toList
        let descriptor =
            { ScreenDispatcherName = dispatcherName
              ScreenProperties = properties
              Layers = List.ofSeq layers }
        (descriptor, equations)

    /// Describe a screen with the given definitions and contained layers.
    let screen<'d when 'd :> ScreenDispatcher> definitions layers screen world =
        screen3 typeof<'d>.Name definitions layers screen world

    /// Describe a layer with the given definitions and contained entities.
    let layer3 dispatcherName (definitions : DescriptiveDefinition seq) (entities : EntityDescriptor seq) (layer : Layer) world =
        let properties =
            definitions |>
            Seq.map (fun def -> match def with PropertyDefinition def -> Some (def.PropertyName, def.PropertyExpr) | Equate _ -> None) |>
            Seq.definitize |>
            Seq.map (mapSnd (function DefineExpr value -> value | VariableExpr fn -> fn world)) |>
            Seq.map (mapSnd valueToSymbol) |>
            Map.ofSeq
        let equations =
            definitions |>
            Seq.map (fun def -> match def with Equate (leftName, right, breaking) -> Some (leftName, layer :> Simulant, right, breaking) | PropertyDefinition _ -> None) |>
            Seq.definitize |>
            Seq.toList
        let descriptor =
            { LayerDispatcherName = dispatcherName
              LayerProperties = properties
              Entities = List.ofSeq entities }
        (descriptor, equations)

    /// Describe a layer with the given definitions and contained entities.
    let layer<'d when 'd :> LayerDispatcher> definitions entities world =
        layer3 typeof<'d>.Name definitions entities world

    /// Describe an entity with the given definitions.
    let entity2 dispatcherName (definitions : DescriptiveDefinition seq) (entity : Entity) world =
        let properties =
            definitions |>
            Seq.map (fun def -> match def with PropertyDefinition def -> Some (def.PropertyName, def.PropertyExpr) | Equate _ -> None) |>
            Seq.definitize |>
            Seq.map (mapSnd (function DefineExpr value -> value | VariableExpr fn -> fn world)) |>
            Seq.map (mapSnd valueToSymbol) |>
            Map.ofSeq
        let equations =
            definitions |>
            Seq.map (fun def -> match def with Equate (leftName, right, breaking) -> Some (leftName, entity :> Simulant, right, breaking) | PropertyDefinition _ -> None) |>
            Seq.definitize |>
            Seq.toList
        let descriptor =
            { EntityDispatcherName = dispatcherName
              EntityProperties = properties }
        (descriptor, equations)

    /// Describe an entity with the given definitions.
    let entity<'d when 'd :> EntityDispatcher> definitions world =
        entity2 typeof<'d>.Name definitions world