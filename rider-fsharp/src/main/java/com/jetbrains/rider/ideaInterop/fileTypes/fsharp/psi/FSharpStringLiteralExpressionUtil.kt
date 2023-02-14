package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi

import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.impl.source.tree.LeafElement
import com.intellij.psi.templateLanguages.OuterLanguageElement
import com.jetbrains.rider.ideaInterop.fileTypes.csharp.kotoparser.lexer.CSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.csharp.psi.CSharpInterpolatedStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.csharp.psi.CSharpStringExpressionUtil
import com.jetbrains.rider.ideaInterop.fileTypes.csharp.psi.StringLiteralType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType

fun FSharpStringLiteralExpression.getRangeTrimQuotes(): TextRange {
  val startOffset = when (literalType) {
    FSharpStringLiteralType.RegularString,
    FSharpStringLiteralType.ByteArray -> 1

    FSharpStringLiteralType.VerbatimString,
    FSharpStringLiteralType.RegularInterpolatedString -> 2

    FSharpStringLiteralType.TripleQuoteString,
    FSharpStringLiteralType.VerbatimInterpolatedString -> 3

    FSharpStringLiteralType.TripleQuoteInterpolatedString -> 4
  }
  val endOffset = when (literalType) {
    FSharpStringLiteralType.RegularString,
    FSharpStringLiteralType.VerbatimString,
    FSharpStringLiteralType.RegularInterpolatedString,
    FSharpStringLiteralType.VerbatimInterpolatedString -> 1

    FSharpStringLiteralType.ByteArray -> 2

    FSharpStringLiteralType.TripleQuoteString,
    FSharpStringLiteralType.TripleQuoteInterpolatedString -> 3
  }

  val start = (textRange.startOffset + startOffset).coerceAtMost(textRange.endOffset)
  val end = (textRange.endOffset - endOffset).coerceAtLeast(textRange.startOffset)
  return TextRange(start, end)
}

fun FSharpStringLiteralExpression.getRelativeRangeTrimQuotes(): TextRange {
  return getRangeTrimQuotes().shiftLeft(textRange.startOffset)
}

object FSharpStringExpressionUtil {
  private val PsiElement.firstNonOuterChild: PsiElement?
    get() {
      tailrec fun search(element: PsiElement?): PsiElement? {
        return when (element) {
          null -> null
          !is OuterLanguageElement -> element
          else -> search(element.nextSibling)
        }
      }

      return search(firstChild)
    }

  private fun tryGetFirstToken(expression: FSharpInterpolatedStringLiteralExpression): LeafElement? {
    return expression.firstNonOuterChild?.firstNonOuterChild?.node as LeafElement?
  }

//  fun getLiteralType(expression: FSharpInterpolatedStringLiteralExpression): FSharpStringLiteralType {
//    return when (tryGetFirstToken(expression)?.elementType) {
//      in FSharpTokenType.REGULAR_STRINGS -> StringLiteralType.RegularInterpolatedString
//      in FSharpTokenType.VERBATIM_STRINGS -> StringLiteralType.VerbatimInterpolatedString
//      FSharpTokenType.INTERPOLATED_STRING_RAW_MULTI_LINE_START -> StringLiteralType.MultiLineRawInterpolatedString
//      FSharpTokenType.INTERPOLATED_STRING_RAW_SINGLE_LINE_START -> StringLiteralType.SingleLineRawInterpolatedString
//      else -> error("invalid element type $expression.elementType")
//    }
//  }
}