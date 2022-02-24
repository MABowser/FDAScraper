// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open FSharp.Data
open System.IO

type RecalProvider = CsvProvider< @"C:\Users\Matt\Source\repos\FDAScraper\OutputExample.csv" >

let htmlRowToCsvRow (row: HtmlNode) =
  let dataElements = row.CssSelect("td") |>  List.toArray
  let date = DateTime.Parse(dataElements.[0].InnerText())
  let brandName = (Seq.head (dataElements.[1].Descendants("a"))).InnerText()
  let productName = dataElements.[2].InnerText()
  let reason = dataElements.[3].InnerText()
  let company = dataElements.[4].InnerText()
  RecalProvider.Row(date,brandName,productName,reason,company)

let htmlRowsToCsvRows (rows: HtmlNode list) =
  rows |> Seq.map htmlRowToCsvRow

let docToRows (doc:HtmlDocument) = 
  doc.CssSelect("table > tbody > tr")
  |> htmlRowsToCsvRows

let generateURL year =
  sprintf "https://wayback.archive-it.org/7993/20180126101453/https://www.fda.gov/Safety/Recalls/ArchiveRecalls/%i/default.htm" year
    
let tryGetHtmlDoc (url:string) =
  try
    Some (HtmlDocument.Load(url))
  with
  | ex -> None

let tryGetNextUrlFromDoc (doc:HtmlDocument) =
  let activePageNumber = Int32.Parse((doc.CssSelect(".pagination-clean > li.active") |> Seq.head).InnerText())
  let nextActivePageNumber = (activePageNumber+1).ToString()
  doc.CssSelect(".pagination-clean > li > a") 
  |> Seq.tryFind (fun a -> a.InnerText() = nextActivePageNumber)
  |> function
     | Some a ->  let linkExt = a.Attributes() 
                                |> Seq.find (fun a -> a.Name() = "href")
                                |> (fun f -> f.Value())
                  let fullUrl = sprintf "https://wayback.archive-it.org%s" linkExt
                  Some fullUrl
     | None   -> None

let rec getRowsFromDoc year url inc =
  seq {
    let doc = tryGetHtmlDoc url
    match doc with
    | Some d -> yield docToRows d
                match tryGetNextUrlFromDoc d with
                | Some u -> yield! getRowsFromDoc (year) u false
                | None   -> let nextYear = year+1
                            yield! getRowsFromDoc (nextYear) (generateURL nextYear) true
    | None   -> yield Seq.empty
  }

[<EntryPoint>]
let main argv =
  let year = 2010
  let initUrl = generateURL year
  let foo = getRowsFromDoc year initUrl false |> Seq.concat
  use tw = new StreamWriter(DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv")
  let myCsv = new RecalProvider(foo)
  myCsv.Save(tw)
  tw.Close()
  0 // return an integer exit code