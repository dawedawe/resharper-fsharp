package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl

import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpIndentationBlock
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpReparseableElementType

class FSharpIndentationBlockImpl(blockType: FSharpReparseableElementType, buffer: CharSequence?) :
  FSharpReparseableElementBase(blockType, buffer), FSharpIndentationBlock
