package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl

import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpElementType

class FSharpScriptReferenceImpl(type: FSharpElementType) : FSharpPsiElementBase(type) {
    override fun toString() = super.toString() + "(${this.text})"
}
