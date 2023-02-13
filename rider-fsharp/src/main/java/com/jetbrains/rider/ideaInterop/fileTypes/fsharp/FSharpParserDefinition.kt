package com.jetbrains.rider.ideaInterop.fileTypes.fsharp

import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.lexer.Lexer
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

abstract class FSharpParserDefinitionBase(
  private val fileElementType: IFileElementType
) : ParserDefinition {
  private val logger = getLogger<FSharpParserDefinitionBase>()
  override fun createLexer(project: Project?): Lexer = FSharpLexer()
  override fun getFileNodeType() = fileElementType
  override fun createParser(project: Project): PsiParser = FSharpDummyParser()
  override fun getCommentTokens(): TokenSet = FSharpTokenType.COMMENTS
  override fun getStringLiteralElements(): TokenSet = FSharpTokenType.ALL_STRINGS
  abstract override fun createFile(p0: FileViewProvider): PsiFile
  override fun createElement(node: ASTNode): PsiElement {
    if (node is PsiElement) {
      logger.error("Dummy blocks should be lazy and not parsed like this")
      return node
    }

    logger.error("An attempt to parse unexpected element")
    return PsiUtilCore.NULL_PSI_ELEMENT
  }
}


class FSharpParserDefinition : FSharpParserDefinitionBase(FSharpElementTypes.FILE) {
  override fun createFile(viewProvider: FileViewProvider) = FSharpFileImpl(viewProvider)
}

class FSharpScriptParserDefinition : FSharpParserDefinitionBase(FSharpElementTypes.SCRIPT_FILE) {
  override fun createFile(viewProvider: FileViewProvider) = FSharpScriptImpl(viewProvider)
}
