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

fun FSharpStringLiteralExpression.getRelativeRangeTrimQuotes() =
  getRangeTrimQuotes().shiftLeft(textRange.startOffset)
