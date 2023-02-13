package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi

import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.PsiLanguageInjectionHost
import com.jetbrains.rider.ideaInterop.fileTypes.common.psi.ClrLanguageInterpolatedStringLiteralExpression
import com.jetbrains.rider.ideaInterop.fileTypes.common.psi.ClrLanguageInterpolatedStringLiteralExpressionPart
import com.jetbrains.rider.ideaInterop.fileTypes.common.psi.ClrLanguageStringLiteralExpression

interface FSharpElement : PsiElement

interface FSharpFile : FSharpElement, PsiFile
interface FSharpScript : FSharpElement, PsiFile

interface FSharpExpression : FSharpElement
interface FSharpReparseableElement : FSharpElement
interface FSharpIndentationBlock : FSharpReparseableElement

interface FSharpStringLiteralExpression : ClrLanguageStringLiteralExpression, FSharpElement, PsiLanguageInjectionHost {
  val literalType: FSharpStringLiteralType
  //fun getContentRange(): TextRange
}

/**
 * Represents a part of an interpolated string that does not contain any code.
 */
interface FSharpInterpolatedStringLiteralExpressionPart : ClrLanguageInterpolatedStringLiteralExpressionPart,
  FSharpExpression

/**
 * Represents an entire interpolated string.
 * This node contains both interpolated string parts and code written between those parts.
 */
interface FSharpInterpolatedStringLiteralExpression : ClrLanguageInterpolatedStringLiteralExpression,
  FSharpStringLiteralExpression {
  val hasInterpolations: Boolean
}
