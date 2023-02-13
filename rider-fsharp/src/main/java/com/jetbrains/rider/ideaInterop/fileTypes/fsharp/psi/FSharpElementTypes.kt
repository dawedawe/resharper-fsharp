package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi

import com.intellij.lang.ASTNode
import com.intellij.lang.Language
import com.intellij.psi.tree.ICompositeElementType
import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.IFileElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.FSharpLanguage
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiElement
import com.intellij.psi.tree.IReparseableElementType
import com.intellij.psi.util.elementType
import com.jetbrains.rider.ideaInterop.fileTypes.RiderFileElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.FSharpScriptLanguage
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenNodeType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpIndentationBlockImpl

class FSharpFileElementType : RiderFileElementType("RIDER_FSHARP_FILE", FSharpLanguage, FSharpFileElementType) {
  companion object {
    private val FSharpFileElementType = IElementType("RIDER_FSHARP", FSharpScriptLanguage)
  }
}

class FSharpScriptElementType :
  RiderFileElementType("RIDER_FSHARP_SCRIPT_FILE", FSharpScriptLanguage, FSharpScriptElementType) {
  companion object {
    private val FSharpScriptElementType = IElementType("RIDER_FSHARP_SCRIPT", FSharpScriptLanguage)
  }
}

open class FSharpElementType(debugName: String, val text: String = debugName) : IElementType(debugName, FSharpLanguage)

abstract class FSharpCompositeElementType(debugName: String) : FSharpElementType(debugName), ICompositeElementType

abstract class FSharpReparseableElementType(debugName: String) : IReparseableElementType(debugName, FSharpLanguage),
  ICompositeElementType {
  abstract override fun createNode(text: CharSequence?): ASTNode?
}

class FSharpIndentationBlockType : FSharpReparseableElementType("INDENTATION_BLOCK") {
  override fun createNode(text: CharSequence?): ASTNode {
    return FSharpIndentationBlockImpl(this, text)
  }

  override fun createCompositeNode(): ASTNode {
    return FSharpIndentationBlockImpl(this, null)
  }

  override fun isReparseable(
    currentNode: ASTNode, newText: CharSequence, fileLanguage: Language, project: Project
  ): Boolean {
    val parentIndent =
      if (currentNode is PsiElement && currentNode.parent != null && currentNode.parent.elementType is FSharpIndentationBlockType) {
        if (currentNode.parent.firstChild == FSharpTokenType.WHITESPACE) currentNode.parent.firstChild.textLength
        else 0
      } else {
        -1
      }

    val lines = newText.split("\n").filter { !it.isNullOrBlank() }
    var firstLineIndent = 0
    for ((i, line) in lines.withIndex()) {
      var indentOfCurrentLine = line.indexOfFirst { it != ' ' }
      if (indentOfCurrentLine == -1) indentOfCurrentLine = line.length
      if (i == 0) {
        firstLineIndent = indentOfCurrentLine
        if (firstLineIndent <= parentIndent) return false
        continue
      }
      if (indentOfCurrentLine <= firstLineIndent) return false
    }
    return true
  }
}

class FSharpNamespaceType : FSharpReparseableElementType("NAMESPACE") {
  override fun createNode(text: CharSequence?) = FSharpIndentationBlockImpl(this, text)
  override fun createCompositeNode() = FSharpIndentationBlockImpl(this, null)
  override fun isReparseable(
    currentNode: ASTNode, newText: CharSequence, fileLanguage: Language, project: Project
  ): Boolean {
    val trimmed = newText.trim()
    return when {
      !trimmed.startsWith("namespace") || trimmed.contains("\nnamespace") -> false
      else -> true
    }
  }
}

class FSharpTopLevelModuleType : FSharpReparseableElementType("TOP_LEVEL_MODULE") {
  override fun createNode(text: CharSequence?): ASTNode {
    return FSharpIndentationBlockImpl(this, text)
  }

  override fun createCompositeNode(): ASTNode {
    return FSharpIndentationBlockImpl(this, null)
  }

  override fun isReparseable(
    currentNode: ASTNode, newText: CharSequence, fileLanguage: Language, project: Project
  ): Boolean {
    val trimmed = newText.trim()
    return when {
      !trimmed.startsWith("module") || trimmed.contains("\nnamespace") -> false
      else -> true
    }
  }
}

class FSharpCommentType : FSharpReparseableElementType("COMMENT") {
  override fun createNode(text: CharSequence?) = FSharpIndentationBlockImpl(this, text)
  override fun createCompositeNode() = FSharpIndentationBlockImpl(this, null)
  override fun isReparseable(currentNode: ASTNode, newText: CharSequence, fileLanguage: Language, project: Project) =
    newText.startsWith("(*") && newText.endsWith("*)") || newText.startsWith("//")
}

inline fun createCompositeElementType(debugName: String, crossinline elementFactory: (FSharpElementType) -> ASTNode) =
  object : FSharpCompositeElementType(debugName) {
    override fun createCompositeNode() = elementFactory(this)
  }
