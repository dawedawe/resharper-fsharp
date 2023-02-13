package com.jetbrains.rider.ideaInterop.fileTypes.fsharp

import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.lexer.Lexer
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.TokenSet
import com.intellij.psi.util.PsiUtilCore
import com.jetbrains.rd.platform.util.getLogger
import com.jetbrains.rider.ideaInterop.fileTypes.RiderFileElementType
import com.jetbrains.rider.ideaInterop.fileTypes.RiderParserDefinitionBase
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpLexer
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpElementTypes
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpFileImpl
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpScriptImpl

class FSharpParserDefinition : ParserDefinition {
  private val logger = getLogger<FSharpParserDefinition>()
  override fun createLexer(project: Project?): Lexer = FSharpLexer()
  override fun getFileNodeType(): IFileElementType = FSharpElementTypes.FILE
  override fun createParser(project: Project): PsiParser = FSharpDummyParser()
  override fun getCommentTokens(): TokenSet = FSharpTokenType.COMMENTS
  override fun getStringLiteralElements(): TokenSet = FSharpTokenType.ALL_STRINGS
  override fun createElement(node: ASTNode?): PsiElement {
    if (node is PsiElement) {
      logger.error("Dummy blocks should be lazy and not parsed like this")
      return node
    }

    logger.error("An attempt to parse unexpected element")
    return PsiUtilCore.NULL_PSI_ELEMENT
  }

  override fun createFile(viewProvider: FileViewProvider) = FSharpFileImpl(viewProvider)
}

class FSharpScriptParserDefinition : RiderParserDefinitionBase(FSharpScriptFileElementType, FSharpScriptFileType) {
  companion object {
    private val FSharpScriptElementType = IElementType("RIDER_FSHARP_SCRIPT", FSharpScriptLanguage)
    val FSharpScriptFileElementType =
      RiderFileElementType("RIDER_FSHARP_SCRIPT_FILE", FSharpScriptLanguage, FSharpScriptElementType)
  }

  override fun createLexer(project: Project?): Lexer = FSharpLexer()
  override fun getFileNodeType(): IFileElementType = FSharpScriptFileElementType
  override fun createFile(viewProvider: FileViewProvider): PsiFile {
    return FSharpScriptImpl(viewProvider)
  }
}
