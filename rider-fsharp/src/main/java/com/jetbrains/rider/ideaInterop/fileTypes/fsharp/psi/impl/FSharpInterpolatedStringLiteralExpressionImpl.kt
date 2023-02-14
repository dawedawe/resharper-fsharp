package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl

import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.psi.LiteralTextEscaper
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.impl.source.tree.CompositePsiElement
import com.intellij.refactoring.suggested.endOffset
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpInterpolatedStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralType

class FSharpInterpolatedStringLiteralExpressionImpl(type: FSharpElementType) : CompositePsiElement(type),
  FSharpInterpolatedStringLiteralExpression {
  override val hasInterpolations: Boolean
    get() = true //CSharpStringExpressionUtil.hasInterpolations(this)

  override val literalType: FSharpStringLiteralType
    get() = FSharpStringLiteralType.RegularInterpolatedString //CSharpStringExpressionUtil.getLiteralType(this)

  override fun isValidHost(): Boolean = true
  //CSharpStringExpressionUtil.isValidInjectionHost(this, allowInterpolations = true)

  override fun updateText(text: String): PsiLanguageInjectionHost {
    FileDocumentManager.getInstance()
      .getDocument(this.containingFile.virtualFile)
      ?.replaceString(this.startOffset, this.endOffset, text)
    return this
  }

  override fun createLiteralTextEscaper(): LiteralTextEscaper<out PsiLanguageInjectionHost> {
    //return CSharpStringExpressionUtil.createLiteralTextEscaper(this)
    return RegularStringEscaper(this)
  }
}
