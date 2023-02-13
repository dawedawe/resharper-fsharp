package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.injections

import com.intellij.lang.injection.ConcatenationAwareInjector
import com.intellij.lang.injection.InjectedLanguageManager
import com.intellij.lang.injection.MultiHostRegistrar
import com.intellij.openapi.util.TextRange
import com.intellij.openapi.util.Trinity
import com.intellij.psi.ElementManipulators
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.PsiWhiteSpace
import com.intellij.refactoring.suggested.startOffset
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpInterpolatedStringLiteralExpressionPart
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralType
import org.intellij.plugins.intelliLang.Configuration
import org.intellij.plugins.intelliLang.inject.InjectedLanguage
import org.intellij.plugins.intelliLang.inject.InjectorUtils
import org.intellij.plugins.intelliLang.inject.LanguageInjectionSupport
import org.intellij.plugins.intelliLang.inject.TemporaryPlacesRegistry
import org.intellij.plugins.intelliLang.inject.config.BaseInjection

class FSharpConcatenationAwareInjector : ConcatenationAwareInjector {
    override fun getLanguagesToInject(registrar: MultiHostRegistrar, vararg operands: PsiElement) {
        if (operands.isEmpty()) return
        val host = operands.firstOrNull { it is FSharpStringLiteralExpression } as FSharpStringLiteralExpression? ?: return
        val csharpInjectionSupport = InjectorUtils.findNotNullInjectionSupport(
            FSharpInjectionSupport.FSHARP_SUPPORT_ID) as FSharpInjectionSupport

        val project = host.project
        val tempPlacesRegistry = TemporaryPlacesRegistry.getInstance(project)

        //
        // Try the following language injection sources:
        // 1. Comment instruction
        // 2. Temp injection (explicitly requested by user or annotation-based coming from the backend)
        // 3. Pattern from project configuration
        //
        var tempInjectedLanguage: InjectedLanguage? = null
        var settingsAvailable = false

        val injection = csharpInjectionSupport.findCommentInjection(host)
            ?: tempPlacesRegistry.getLanguageFor(host, host.containingFile)?.let {
                tempInjectedLanguage = it
                BaseInjection(TemporaryPlacesRegistry.SUPPORT_ID, it.id, it.prefix, it.suffix)
            }
            ?: Configuration.getProjectInstance(project)
                .getInjections(csharpInjectionSupport.id)
                .firstOrNull { it.acceptsPsiElement(host) }?.let {
                    settingsAvailable = true
                    it
                }
            ?: return

        val language = injection.injectedLanguage ?: return

        val processor = InjectionProcessor(injection)
        operands.forEach(processor::accept)
        processor.finish()

        if (processor.result.isEmpty()) return

        InjectorUtils.registerInjection(
            injection.injectedLanguage,
            processor.result,
            host.containingFile,
            registrar
        )

        // note: in case of temp injection use temp injection support for proper unregistering
        InjectorUtils.registerSupport(
            if (tempInjectedLanguage == null) csharpInjectionSupport else tempPlacesRegistry.languageInjectionSupport,
            settingsAvailable,
            host,
            language)

        if (tempInjectedLanguage != null) {
            InjectorUtils.putInjectedFileUserData(
                host,
                language,
                LanguageInjectionSupport.TEMPORARY_INJECTED_LANGUAGE,
                tempInjectedLanguage)

        }

        if (processor.disableInspections) {
            InjectorUtils.putInjectedFileUserData(
                host,
                language,
                InjectedLanguageManager.FRANKENSTEIN_INJECTION,
                java.lang.Boolean.TRUE
            )
        }
    }

    private class InjectionProcessor(private val injection: BaseInjection) {
        var disableInspections = false
        val result = mutableListOf<Trinity<PsiLanguageInjectionHost, InjectedLanguage, TextRange>>()

        private var prefix = StringBuilder()
        private var suffix = StringBuilder().append(injection.prefix) // note: initial suffix is used as a prefix for the first literal
        private var literal: FSharpStringLiteralExpression? = null

        fun accept(element: PsiElement) {
            if (element is FSharpStringLiteralExpression) {
                val currentLiteral = literal
                if (currentLiteral != null) registerCurrentLiteral(currentLiteral)

                literal = element
                prefix = suffix
                suffix = StringBuilder()
            }
            else if (element !is PsiWhiteSpace) {
                // can't reliably inspect injected PSI with arbitrary expressions
                disableInspections = true
                suffix.append(element.text)
            }
        }

        private fun registerCurrentLiteral(literal: FSharpStringLiteralExpression) = registerValueRanges(literal)

        private fun registerValueRanges(literal: FSharpStringLiteralExpression, useSuffix: Boolean = false) {
            when (literal.literalType) {
                FSharpStringLiteralType.RegularString,
                FSharpStringLiteralType.VerbatimString -> {
                    val templates = Regex("\\{\\d+}").findAll(ElementManipulators.getValueText(literal))
                    val ranges = mutableListOf<TextRange>()
                    val valueTextRange = ElementManipulators.getValueTextRange(literal)
                    val firstFragmentStartOffset = valueTextRange.startOffset
                    var nextFragmentStart = firstFragmentStartOffset

                    for (fragment in templates) {
                        // can't reliably inspect injected PSI with arbitrary template substitutions
                        disableInspections = true

                        val range = TextRange(nextFragmentStart, fragment.range.first + firstFragmentStartOffset)
                        if (!range.isEmpty) ranges.add(range)
                        nextFragmentStart = fragment.range.last + firstFragmentStartOffset + 1
                    }

                    val lastRange = TextRange(nextFragmentStart, valueTextRange.endOffset)
                    if (!lastRange.isEmpty) ranges.add(lastRange)
                    else if (suffix.isEmpty()) suffix.append("dummyIdentifier")

                    registerRanges(ranges, useSuffix, literal)
                }
                FSharpStringLiteralType.RegularInterpolatedString, FSharpStringLiteralType.VerbatimInterpolatedString -> {
                    val parts = literal.children.filterIsInstance<FSharpInterpolatedStringLiteralExpressionPart>()

                    // can't reliably inspect injected PSI with interpolations
                    if (parts.count() > 1) {
                        disableInspections = true
                    }

                    val ranges = parts.mapIndexed { index, part ->
                        val startOffsetInPart = if (index == 0) ElementManipulators.getValueTextRange(literal).startOffset else 1
                        TextRange(part.startOffsetInParent + startOffsetInPart, part.startOffsetInParent + part.textLength - 1)
                    }

                    registerRanges(ranges, useSuffix, literal)
                }

                else -> {}
            }
        }

        private fun registerRanges(ranges: List<TextRange>, useSuffix: Boolean, literal: FSharpStringLiteralExpression) {
            ranges.forEachIndexed { index, range ->
                val suffix = if (useSuffix && index == ranges.lastIndex) suffix.toString() else ""
                val injectedLanguage = InjectedLanguage.create(injection.injectedLanguageId, prefix.toString(), suffix, true)
                result.add(Trinity.create(literal, injectedLanguage, range))

                if (index != ranges.lastIndex) prefix.clear().append(PLACEHOLDER_IDENTIFIER)
            }
        }

        fun finish() {
            val literal = literal ?: return

            suffix.append(injection.suffix)
            registerValueRanges(literal, true)
        }
    }

    companion object {
        const val PLACEHOLDER_IDENTIFIER = "jetbrainsRiderDummyIdentifier"
        const val PLACEHOLDER_IDENTIFIER_QUOTED_ESCAPED = "\\\"$PLACEHOLDER_IDENTIFIER\\\""
    }
}