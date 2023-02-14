package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.injections

import com.intellij.openapi.util.TextRange
import com.intellij.psi.ElementManipulators
import com.jetbrains.rider.ideaInterop.fileTypes.common.psi.ClrLanguageInterpolatedStringLiteralExpression
import com.jetbrains.rider.plugins.appender.database.common.ClrLanguageConcatenationAwareInjector
import org.intellij.plugins.intelliLang.inject.config.BaseInjection
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.*

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
              val count = parts.count()

              if (count == 0) return listOf(literal.getRangeTrimQuotes()) else {

                // can't reliably inspect injected PSI with interpolations
                if (count > 1) {
                  disableInspections = true
                }

                parts.mapIndexed { index, part ->
                  val startOffsetInPart =
                    if (index == 0) ElementManipulators.getValueTextRange(literal).startOffset else 1
                  TextRange(
                    part.startOffsetInParent + startOffsetInPart,
                    part.startOffsetInParent + part.textLength - 1
                  )
                }
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
