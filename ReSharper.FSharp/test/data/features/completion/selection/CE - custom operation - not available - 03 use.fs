module Module

type CE() =
    member this.Yield(x) = x
    [<CustomOperation("custom")>]
    member this.Custom(x) = x

let ce = CE()

let foo f = ()

let car = ()

ce {
    use x = 1
    {caret}
}
