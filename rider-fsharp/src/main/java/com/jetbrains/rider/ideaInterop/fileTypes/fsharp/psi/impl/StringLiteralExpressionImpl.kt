package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl

import com.intellij.codeInsight.CodeInsightUtilCore
import com.intellij.psi.LiteralTextEscaper
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.impl.source.tree.LeafElement
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.FSharpStringLiteralType

class FSharpStringLiteralExpressionImpl(type: FSharpElementType) : FSharpPsiElementBase(type),
    FSharpStringLiteralExpression {

    override val literalType: FSharpStringLiteralType
        get() {
            return when (val elementType = firstChild.node.elementType) {
                FSharpTokenType.STRING,
                FSharpTokenType.UNFINISHED_STRING,
                FSharpTokenType.UNFINISHED_STRING -> FSharpStringLiteralType.RegularString

                FSharpTokenType.VERBATIM_STRING,
                FSharpTokenType.VERBATIM_BYTEARRAY,
                FSharpTokenType.UNFINISHED_VERBATIM_STRING -> FSharpStringLiteralType.VerbatimString

                FSharpTokenType.TRIPLE_QUOTED_STRING,
                FSharpTokenType.UNFINISHED_TRIPLE_QUOTED_STRING -> FSharpStringLiteralType.TripleQuoteString

                FSharpTokenType.BYTEARRAY -> FSharpStringLiteralType.ByteArray
                else -> error("invalid element type $elementType")
            }
        }

    override fun isValidHost(): Boolean {
        return when (literalType) {
            FSharpStringLiteralType.RegularString,
            FSharpStringLiteralType.VerbatimString,
            FSharpStringLiteralType.TripleQuoteString -> true
            else -> false
        }
    }

    override fun updateText(text: String): PsiLanguageInjectionHost {
        val valueNode = node.firstChildNode
        assert(valueNode is LeafElement)
        (valueNode as LeafElement).replaceWithText(text)
        return this
    }

    override fun createLiteralTextEscaper(): LiteralTextEscaper<out PsiLanguageInjectionHost> {
        return when (literalType) {
            FSharpStringLiteralType.VerbatimString -> VerbatimStringEscaper(this)
            FSharpStringLiteralType.RegularString -> RegularStringEscaper(this)
            FSharpStringLiteralType.TripleQuoteString -> TripleQuotedStringEscaper(this)
            FSharpStringLiteralType.ByteArray -> ByteArrayEscaper(this)
            else -> error("invalid literal type $literalType")
        }
    }

    companion object {
        fun parseStringCharacters(chars: String, outChars: StringBuilder, sourceOffsets: IntArray?): Boolean {
            return CodeInsightUtilCore.parseStringCharacters(chars, outChars, sourceOffsets)
        }
    }
}
