#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

let getGameInfo gameId =
    let json =
        Http.RequestString (
            url     = "http://www.giantbomb.com/api/game/" + gameId, 
            query   = [ "api_key", config.GiantBomb.ApiKey
                        "format", "json"
                        "field_list", "name,original_release_date" ])
    printfn "%s" json
    try Some <| GiantBomb.GameResponse.Parse json
    with _ -> None
