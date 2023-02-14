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
interface FSharpDummyBlock : FSharpReparseableElement

interface FSharpStringLiteralExpression : ClrLanguageStringLiteralExpression, FSharpElement, PsiLanguageInjectionHost {
  val literalType: FSharpStringLiteralType
  //fun getContentRange(): TextRange
}

/**
 * Represents a part of an interpolated string that does not contain any code.
 */
interface FSharpInterpolatedStringLiteralExpressionPart : ClrLanguageInterpolatedStringLiteralExpressionPart,
  FSharpExpression

interface FSharpInterpolatedStringLiteralExpression : ClrLanguageInterpolatedStringLiteralExpression,
  FSharpStringLiteralExpression
