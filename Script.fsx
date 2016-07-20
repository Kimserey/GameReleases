#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

open System
open System.Globalization
open System.IO
open FSharp.Data
open Newtonsoft.Json

type Publisher = {
    Name: string
    Games: Game []
} and Game = {
    Id: int
    Name: string
    Release: DateTime
}

type HTMLGamePage = HtmlProvider<"http://www.giantbomb.com/mirror-s-edge-catalyst/3030-27028/">
type BlizzardResponse = JsonProvider<"./json/atvi.json">

let blizzard = BlizzardResponse.GetSample().Results

let publisher = {
    Name = blizzard.Name
    Games =
        blizzard.PublishedGames
        |> Array.Parallel.map (fun game -> game, HTMLGamePage.Load(game.SiteDetailUrl))
        |> Array.choose (fun (game, page) -> 
            page.Tables.``Game details``.Rows
            |> Seq.filter (fun row -> row.Column1 = "First release date" && (String.IsNullOrWhiteSpace row.Column2 |> not))
            |> Seq.map (fun row -> Convert.ToDateTime(row.Column2))
            |> Seq.tryHead
            |> Option.map (fun date -> 
                { Id = game.Id
                  Name = game.Name
                  Release = date }))
}
