package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi

import com.intellij.openapi.util.TextRange
import com.intellij.openapi.util.text.StringUtil
import com.intellij.psi.ElementManipulator

class FSharpStringElementManipulator : ElementManipulator<FSharpStringLiteralExpression> {
    override fun handleContentChange(
        element: FSharpStringLiteralExpression,
        newContent: String
    ): FSharpStringLiteralExpression =
        handleContentChange(element, getRangeInElement(element), newContent)

    override fun handleContentChange(
        element: FSharpStringLiteralExpression,
        range: TextRange, newContent: String
    ): FSharpStringLiteralExpression {
        val oldText = element.text
        var newText = when (element.literalType) {
            FSharpStringLiteralType.RegularString,
            FSharpStringLiteralType.ByteArray -> StringUtil.escapeStringCharacters(newContent)

            FSharpStringLiteralType.VerbatimString -> newContent.replace("\"\"", "\"").replace("\"", "\"\"")
            FSharpStringLiteralType.TripleQuoteString -> newContent
            FSharpStringLiteralType.RegularInterpolatedString -> StringUtil.escapeStringCharacters(newContent)
            FSharpStringLiteralType.VerbatimInterpolatedString -> newContent.replace("\"\"", "\"").replace("\"", "\"\"")
            FSharpStringLiteralType.TripleQuoteInterpolatedString -> newContent
        }

        newText = oldText.substring(0, range.startOffset) + newText + oldText.substring(range.endOffset)

        return element.updateText(newText) as FSharpStringLiteralExpression
    }

    override fun getRangeInElement(element: FSharpStringLiteralExpression) =
        element.getRelativeRangeTrimQuotes()
}
