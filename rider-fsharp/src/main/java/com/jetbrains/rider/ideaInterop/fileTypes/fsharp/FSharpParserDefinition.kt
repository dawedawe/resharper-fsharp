package com.jetbrains.rider.ideaInterop.fileTypes.fsharp

import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.TokenSet
import com.intellij.psi.util.PsiUtilCore
import com.jetbrains.rd.platform.util.getLogger
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpLexer
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpElementTypes
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpFileImpl
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpScriptImpl

abstract class FSharpParserDefinitionBase : ParserDefinition {
  private val logger = getLogger<FSharpParserDefinitionBase>()
  override fun createLexer(project: Project?) = FSharpLexer()
  override fun createParser(project: Project): PsiParser = FSharpDummyParser()
  override fun getCommentTokens(): TokenSet = FSharpTokenType.COMMENTS
  override fun getStringLiteralElements(): TokenSet = FSharpTokenType.ALL_STRINGS
  abstract override fun createFile(p0: FileViewProvider): PsiFile
  abstract override fun getFileNodeType(): IFileElementType
  override fun createElement(node: ASTNode): PsiElement {
    if (node is PsiElement) {
      logger.error("Dummy blocks should be lazy and not parsed like this")
      return node
    }

    logger.error("An attempt to parse unexpected element")
    return PsiUtilCore.NULL_PSI_ELEMENT
  }
}


class FSharpParserDefinition : FSharpParserDefinitionBase() {
  override fun createFile(viewProvider: FileViewProvider) = FSharpFileImpl(viewProvider)
  override fun getFileNodeType(): IFileElementType = FSharpElementTypes.FILE
}

class FSharpScriptParserDefinition : FSharpParserDefinitionBase() {
  override fun createFile(viewProvider: FileViewProvider) = FSharpScriptImpl(viewProvider)
  override fun getFileNodeType(): IFileElementType = FSharpElementTypes.SCRIPT_FILE
}
