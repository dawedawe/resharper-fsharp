[<Attr>]
module Kek =
    let x =
        5

[<Attr>]
module Lol =
   5


let [<Literal>] paketTargets = "Paket.Restore.targets"

[<ShellComponent>]
type PaketTargetsProjectLoadModificator() =