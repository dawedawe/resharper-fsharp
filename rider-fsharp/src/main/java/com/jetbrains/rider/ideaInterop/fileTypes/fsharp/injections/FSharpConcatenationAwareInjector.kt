package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.injections

import com.intellij.openapi.util.TextRange
import com.intellij.psi.ElementManipulators
import com.intellij.refactoring.suggested.startOffset
import com.jetbrains.rider.ideaInterop.fileTypes.common.psi.ClrLanguageInterpolatedStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpInterpolatedStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpInterpolatedStringLiteralExpressionPart
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralType
import com.jetbrains.rider.plugins.appender.database.common.ClrLanguageConcatenationAwareInjector
import org.intellij.plugins.intelliLang.inject.config.BaseInjection

class FSharpConcatenationAwareInjector :
  ClrLanguageConcatenationAwareInjector(FSharpInjectionSupport.FSHARP_SUPPORT_ID) {
  override fun getInjectionProcessor(injection: BaseInjection) = FSharpInjectionProcessor(injection)

  protected class FSharpInjectionProcessor(injection: BaseInjection) : InjectionProcessor(injection) {
    override fun registerInterpolatedStringValueRanges(literal: ClrLanguageInterpolatedStringLiteralExpression): List<TextRange> {
      return when (literal) {
        is FSharpInterpolatedStringLiteralExpression -> {
          when (literal.literalType) {
            FSharpStringLiteralType.RegularInterpolatedString,
            FSharpStringLiteralType.VerbatimInterpolatedString,
            FSharpStringLiteralType.TripleQuoteInterpolatedString -> {
              val parts = literal.children.filterIsInstance<FSharpInterpolatedStringLiteralExpressionPart>()

              // can't reliably inspect injected PSI with interpolations
              if (parts.count() > 1) {
                disableInspections = true
              }

              parts.mapIndexed { index, part ->
                val startOffsetInPart =
                  if (index == 0) ElementManipulators.getValueTextRange(literal).startOffset else 1
                TextRange(part.startOffsetInParent + startOffsetInPart, part.startOffsetInParent + part.textLength - 1)
              }
            }

            else -> emptyList()
          }
        }

        else -> emptyList()
      }
    }
  }
}
