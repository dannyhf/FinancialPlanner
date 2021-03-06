module FinancialPlanner.ConsoleUI

open System
open System.Text
open FinancialPlanner.Domain
open FinancialPlanner.Domain.Spending
open FinancialPlanner.Data
open FinancialPlanner.CommandParameter
open FinancialPlanner.Domain.Currency

let spendingPresentation spending =
    match spending with
    | Actual a ->
        $"  Id: %A{a.Id}\n"
        + $"  Creation date: {a.CreationDate:``dd-MM-yyyy``}\n"
        + $"  Expenditure object: %s{a.ExpenditureObject}\n"
        + $"  Estimated amount of money {a.EstimatedCost:N2} %s{a.Currency |> getPostfix}\n"
        + $"  Spent date: {a.SpentDate:``dd-MM-yyyy``}\n"
        + $"  Actual money spent: {a.ActualSpent:N2} %s{a.Currency |> getPostfix}"
    | Expected e ->
        $"  Id: %A{e.Id}\n"
        + $"  Creation date: {e.CreationDate:``dd-MM-yyyy``}\n"
        + $"  Expenditure object: %s{e.ExpenditureObject}\n"
        + $"  Estimated amount of money {e.EstimatedCost:N2} %s{e.Currency |> getPostfix}"

let shortStatisticsPresentation statistics =
    let builder = StringBuilder ()
    statistics |> List.iter (fun u -> builder.Append ($"%s{u.Currency |> getCode}:\n" +
                                                      $"   Total spent {u.TotalSpent:N2}\n" +
                                                      $"   Still expected to spend {u.StillExpectedToSpend:N2}\n" +
                                                      $"   Difference between planned and spent {u.DifferenceBetweenPlannedAndSpent:N2}\n \n") |> ignore)
    builder.ToString ()
    
let executeCommand command =
    async {
        let ctx = JsonDataContext ()
        let! spendings = ctx.getSpendings ()

        match command with
        | ShowSpendings cmd ->
            match spendings
                  |> Ok
                  |> (filterShowSpending cmd.FilterParameters) with
            | Ok s -> s |> List.iter (fun u -> printf $"%s{u |> spendingPresentation} \n \n")
            | Error e -> printfn $"%A{e}"
        | CreateExpectedSpending cmd ->
            do!
                cmd.Form
                |> createExpected
                |> Expected
                |> ctx.add
        | MakeActualSpending cmd ->
            match (spendings |> List.tryFind (fun u -> cmd.ExpectedSpendingId = (u |> getId)), cmd.ActualCost.Currency) with
            | Some (Expected ex), c when c = ex.Currency ->
                do! ex
                    |> makeActual (cmd.ActualCost.Amount, cmd.SpendDate)
                    |> Actual
                    |> ctx.put
            | Some (Expected ex), c when not (c = ex.Currency) -> printfn $"Currency doesn't match. Must be %s{c |> getCode}"                                                   
            | Some (Actual _), _ -> printfn "Spending must be expected, but was actual"
            | None, _ -> printfn $"Spending with guid %A{cmd.ExpectedSpendingId}"
            | _ -> failwith "todo"
        | DeleteSpending cmd ->
            let! deleting = cmd.SpendingId |> ctx.delete
            match deleting with
            | Some _ -> printfn "Deleted"
            | None -> printfn $"Can't find spending with id %s{string <| cmd.SpendingId}"
        | GetShortStatistics -> printfn $"%s{spendings |> prepareShortStatistics |> shortStatisticsPresentation}"
        | ClearConsole -> Console.Clear ()

        do! ctx.saveChanges
    }
 