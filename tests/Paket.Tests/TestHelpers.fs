﻿module Paket.TestHelpers

open Paket
open System
open Paket.Requirements
open Paket.PackageSources
open PackageResolver
open System.Xml
open System.IO
open Paket.Domain

type GraphDependency = string * VersionRequirement * FrameworkRestrictions

type DependencyGraph = list<string * string * (GraphDependency) list>

let OfSimpleGraph (g:seq<string * string * (string * VersionRequirement) list>) : DependencyGraph =
  g
  |> Seq.map (fun (x, y, (rqs)) ->
    x, y, rqs |> List.map (fun (a,b) -> (a, b, FrameworkRestrictionList [])))
  |> Seq.toList

let OfGraphWithRestriction (g:seq<string * string * (string * VersionRequirement * FrameworkRestrictions) list>) : DependencyGraph =
  g
  |> Seq.map (fun (x, y, (rqs)) ->
    x, y, rqs |> List.map (fun (a,b,c) -> (a, b, c)))
  |> Seq.toList

let GraphOfNuspecs (g:seq<string>) : DependencyGraph =
  g
  |> Seq.map (fun nuspecText ->
    let nspec = Nuspec.Load("in-memory", nuspecText)
    nspec.OfficialName, nspec.Version, nspec.Dependencies |> List.map (fun (a,b,c) -> a.GetCompareString(), b, c))
  |> Seq.toList

let PackageDetailsFromGraph (graph : DependencyGraph) sources groupName (package:PackageName) (version:SemVerInfo) = 
    let name,dependencies = 
        graph
        |> Seq.filter (fun (p, v, _) -> (PackageName p) = package && SemVer.Parse v = version)
        |> Seq.map (fun (n, _, d) -> PackageName n,d |> List.map (fun (x,y,z) -> PackageName x,y,z))
        |> Seq.head

    { Name = name
      Source = Seq.head sources
      DownloadLink = ""
      LicenseUrl = ""
      Unlisted = false
      DirectDependencies = Set.ofList dependencies }

let VersionsFromGraph (graph : DependencyGraph) sources resolverStrategy groupName packageName = 
    let versions =
        graph
        |> Seq.filter (fun (p, _, _) -> (PackageName p) = packageName)
        |> Seq.map (fun (_, v, _) -> SemVer.Parse v)
        |> Seq.toList
        |> List.map (fun v -> v,sources)

    match resolverStrategy with
    | ResolverStrategy.Max -> List.sortDescending versions
    | ResolverStrategy.Min -> List.sort versions

let VersionsFromGraphAsSeq (graph : DependencyGraph) sources resolverStrategy groupName packageName = 
   VersionsFromGraph graph sources resolverStrategy groupName packageName
   |> Seq.ofList

let safeResolve graph (dependencies : (string * VersionRange) list)  = 
    let sources = [ PackageSource.NuGetV2Source "" ]
    let packages = 
        dependencies
        |> List.map (fun (n, v) -> 
               { Name = PackageName n
                 VersionRequirement = VersionRequirement(v, PreReleaseStatus.No)
                 Parent = PackageRequirementSource.DependenciesFile ""
                 Graph = []
                 Sources = sources
                 Settings = InstallSettings.Default
                 ResolverStrategyForDirectDependencies = Some ResolverStrategy.Max 
                 ResolverStrategyForTransitives = Some ResolverStrategy.Max })
        |> Set.ofList

    PackageResolver.Resolve(VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph, Constants.MainDependencyGroup, None, None, FrameworkRestrictionList [], packages, UpdateMode.UpdateAll)

let resolve graph dependencies = (safeResolve graph dependencies).GetModelOrFail()

let ResolveWithGraph(dependenciesFile:DependenciesFile,getSha1,getVersionsF, getPackageDetailsF) =
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    dependenciesFile.Resolve(true,getSha1,getVersionsF,getPackageDetailsF,groups,UpdateMode.UpdateAll)

let getVersion (resolved:ResolvedPackage) = resolved.Version.ToString()

let getSource (resolved:ResolvedPackage) = resolved.Source

let removeLineEndings (text : string) = 
    text.Replace("\r\n", "").Replace("\r", "").Replace("\n", "")

let toLines (text : string) = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')

let noSha1 owner repo branch = failwith "no github configured"

let fakeSha1 owner repo branch = "12345"

let normalizeXml(text:string) =
    let doc = new XmlDocument()
    doc.LoadXml(text)
    use stringWriter = new StringWriter()
    let settings = XmlWriterSettings()
    settings.Indent <- true
        
    use xmlTextWriter = XmlWriter.Create(stringWriter, settings)
    doc.WriteTo(xmlTextWriter)
    xmlTextWriter.Flush()
    stringWriter.GetStringBuilder().ToString()

let toPath elems = System.IO.Path.Combine(elems |> Seq.toArray)

let ensureDir () = System.Environment.CurrentDirectory <-  NUnit.Framework.TestContext.CurrentContext.TestDirectory