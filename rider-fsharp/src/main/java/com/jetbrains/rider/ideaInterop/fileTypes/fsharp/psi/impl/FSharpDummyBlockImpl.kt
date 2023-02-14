package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl

import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpDummyBlock
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpReparseableElementType

class FSharpDummyBlockImpl(blockType: FSharpReparseableElementType, buffer: CharSequence?) :
  FSharpReparseableElementBase(blockType, buffer), FSharpDummyBlock
