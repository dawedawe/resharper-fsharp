// ${COMPLETE_ITEM:fun int -> string ->}
module Module

let f (a: int) (b: int -> string -> unit, c: int) = ()

f 1 ({caret}, 1)
 
