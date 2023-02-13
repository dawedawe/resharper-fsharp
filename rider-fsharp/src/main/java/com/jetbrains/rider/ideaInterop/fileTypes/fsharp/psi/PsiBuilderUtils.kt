package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi

import com.intellij.lang.PsiBuilder
import com.intellij.psi.tree.IElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType

/** Returns true if next token has same type as given. Step == 0 means check current token*/
