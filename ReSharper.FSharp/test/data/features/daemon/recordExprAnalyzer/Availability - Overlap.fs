type Record = { Foo: int }
type Record0 = { Foo0: Record }
type Record1 = { Foo1A: Record0; Foo1B: Record0 }
type Record2 = { Foo2: Record1 }

let f item =
    { item with Foo2 = { item.Foo2 with Foo1A = { item.Foo2.Foo1B with Foo0 = { item.Foo2.Foo1B.Foo0 with Foo = 3 } } } }
