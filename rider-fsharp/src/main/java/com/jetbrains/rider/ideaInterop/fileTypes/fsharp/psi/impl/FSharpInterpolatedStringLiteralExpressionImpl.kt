package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl

import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.util.TextRange
import com.intellij.psi.LiteralTextEscaper
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.PsiReference
import com.intellij.psi.PsiReferenceService
import com.intellij.psi.impl.source.resolve.reference.ReferenceProvidersRegistry
import com.intellij.refactoring.suggested.endOffset
import com.jetbrains.rider.ideaInterop.fileTypes.csharp.kotoparser.CSharpElementType
import com.jetbrains.rider.ideaInterop.fileTypes.csharp.psi.impl.CSharpPsiElementBase
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpInterpolatedStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralType

abstract class FSharpStringElementBase(type: FSharpElementType) : FSharpPsiElementBase(type),
    FSharpStringLiteralExpression {
    override fun getReferences(): Array<PsiReference> = ReferenceProvidersRegistry.getReferencesFromProviders(this, PsiReferenceService.Hints.NO_HINTS)
}

class FSharpInterpolatedStringLiteralExpressionImpl(type: FSharpElementType) : FSharpStringElementBase(type),
    FSharpInterpolatedStringLiteralExpression {
    override val hasInterpolations: Boolean
        get() = true //CSharpStringExpressionUtil.hasInterpolations(this)

    override val literalType: FSharpStringLiteralType
        get() = FSharpStringLiteralType.RegularInterpolatedString //CSharpStringExpressionUtil.getLiteralType(this)

    override fun isValidHost(): Boolean =
        true
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
