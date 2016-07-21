#load "../packages/Deedle/Deedle.fsx"
#r "../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

open System
open System.Globalization
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Deedle

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

type CsvFile = CsvFile of string
type JsonFile = JsonFile of string

type Price = {
    Date: DateTime
    Open: float
    High: float
    Low: float
    Close: float
    Volume: float
    AdjustedClose: float
    Ticker: string
}

[<CLIMutable>]
type Response = {
    [<JsonProperty("results")>]
    Game: Game
} and [<CLIMutable>] Game = {
    [<JsonProperty("name")>]
    Name: string
    [<JsonProperty("original_release_date")>]
    ReleaseDate: DateTime
}

let frame (CsvFile pricesFile) (JsonFile releasesFile) = 
    let releases file =
        File.ReadLines(file)
        |> Seq.map (fun json -> JsonConvert.DeserializeObject<Response>(json, new JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore)))
        |> Seq.map (fun r -> r.Game)
        |> Seq.filter (fun g -> g.ReleaseDate > DateTime.MinValue)
        |> Seq.sortBy (fun g -> g.ReleaseDate)
        |> Seq.groupBy (fun g -> g.ReleaseDate)
        |> Seq.map (fun (date, v) -> date => (v |> Seq.map (fun g -> g.Name) |> String.concat "/ "))
        |> series

    Frame.ReadCsv(pricesFile, hasHeaders = true)
    |> Frame.groupRowsBy "Date"
    |> Frame.mapRowKeys fst
    |> Frame.join JoinKind.Right (series [ "Release" => releases releasesFile ] |> Frame.ofColumns)
    |> Frame.sortRowsByKey

let activisionBlizzardFrame = 
    frame (CsvFile "../../Stock_prices/ATVI.csv") (JsonFile "json/atvi_games.json")

#I __SOURCE_DIRECTORY__
#r "../packages/Suave/lib/net40/Suave.dll"

open Suave
open Suave.Files
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open Newtonsoft.Json
open Newtonsoft.Json.Serialization

let JSON v =
  OK (JsonConvert.SerializeObject(v, new JsonSerializerSettings(ContractResolver = new CamelCasePropertyNamesContractResolver())))
  >=> setMimeType "application/json; charset=utf-8"
  >=> setHeader "Access-Control-Allow-Origin" "*"
  >=> setHeader "Access-Control-Allow-Headers" "content-type"

type Data = {
    Items: DataItem list
} and DataItem = {
    Date: string
    HasRelease: bool
    ReleaseName: string
    Open: float
    Close: float
    High: float
    Low: float
}

let app = 
    GET >=> choose
        [ path "/data" 
            >=> JSON (activisionBlizzardFrame 
                      |> Frame.rows 
                      |> Series.observations
                      |> Seq.map (fun (date, series) -> 
                        {
                            Date = date.ToString("yyyy-MM-dd")
                            HasRelease = series.TryGetAs<string>("Release").HasValue
                            ReleaseName = 
                                match series.TryGetAs<string>("Release") with
                                | OptionalValue.Missing -> ""
                                | OptionalValue.Present(v) -> v
                            Open = series?Open
                            Close = series?Close
                            High = series?High
                            Low = series?Low
                        })
                      |> Seq.toList)]

startWebServer defaultConfig app
