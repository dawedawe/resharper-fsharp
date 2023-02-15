package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.injections

import com.intellij.openapi.util.TextRange
import com.intellij.psi.ElementManipulators
import com.intellij.refactoring.suggested.endOffset
import com.intellij.refactoring.suggested.startOffset
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
          val parts = literal.children.filterIsInstance<FSharpInterpolatedStringLiteralExpressionPart>()
          val partsCount = parts.count()

          // can't reliably inspect injected PSI with interpolations
          if (partsCount > 1) disableInspections = true

          val range = ElementManipulators.getValueTextRange(literal)

          parts.mapIndexed { index, part ->
            val startOffsetInPart =
              if (index == 0) range.startOffset else part.startOffsetInParent + 1

            val endOffsetInPart =
              if (index == partsCount - 1) range.endOffset else part.startOffsetInParent + part.textLength - 1

            TextRange(startOffsetInPart, endOffsetInPart)
          }
        }

        else -> emptyList()
      }
    }
  }
}
