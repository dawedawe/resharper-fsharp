package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl

import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.psi.LiteralTextEscaper
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.impl.source.tree.CompositePsiElement
import com.intellij.psi.util.elementType
import com.intellij.refactoring.suggested.endOffset
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpInterpolatedStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpInterpolatedStringLiteralExpressionPart
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralType

class FSharpInterpolatedStringLiteralExpressionImpl(type: FSharpElementType) : CompositePsiElement(type),
  FSharpInterpolatedStringLiteralExpression {

  override val literalType: FSharpStringLiteralType
    get() =
      when (firstChild) {
        is FSharpInterpolatedStringLiteralExpressionPart ->
          when (firstChild.firstChild.elementType) {
            FSharpTokenType.REGULAR_INTERPOLATED_STRING_START,
            FSharpTokenType.REGULAR_INTERPOLATED_STRING ->
              FSharpStringLiteralType.RegularInterpolatedString

            FSharpTokenType.VERBATIM_INTERPOLATED_STRING_START,
            FSharpTokenType.VERBATIM_INTERPOLATED_STRING ->
              FSharpStringLiteralType.VerbatimInterpolatedString

            FSharpTokenType.TRIPLE_QUOTE_INTERPOLATED_STRING_START,
            FSharpTokenType.TRIPLE_QUOTE_INTERPOLATED_STRING,
            FSharpTokenType.TRIPLE_QUOTED_STRING ->
              FSharpStringLiteralType.TripleQuoteInterpolatedString

            else -> error("invalid element type " + firstChild.elementType)
          }

        else -> error("invalid first child $firstChild")
      }

  override fun isValidHost() = true

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
