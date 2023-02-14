package com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi

import com.intellij.lang.ASTNode
import com.intellij.lang.Language
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiElement
import com.intellij.psi.tree.ICompositeElementType
import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.IReparseableElementType
import com.intellij.psi.util.elementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.FSharpLanguage
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.FSharpScriptLanguage
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpDummyBlockImpl

class FSharpFileElementType : IFileElementType("FSharpFile", FSharpLanguage)
class FSharpScriptElementType : IFileElementType("FSharpScript", FSharpScriptLanguage)

open class FSharpElementType(debugName: String, val text: String = debugName) : IElementType(debugName, FSharpLanguage)

abstract class FSharpCompositeElementType(debugName: String) : FSharpElementType(debugName), ICompositeElementType

abstract class FSharpReparseableElementType(debugName: String) : IReparseableElementType(debugName, FSharpLanguage),
  ICompositeElementType {
  abstract override fun createNode(text: CharSequence?): ASTNode?
}

class FSharpDummyBlockType : FSharpReparseableElementType("DUMMY_BLOCK") {
  override fun createNode(text: CharSequence?) = FSharpDummyBlockImpl(this, text)
  override fun createCompositeNode() = FSharpDummyBlockImpl(this, null)

  override fun isReparseable(
    currentNode: ASTNode, newText: CharSequence, fileLanguage: Language, project: Project
  ): Boolean {
    val parentIndent =
      if (currentNode is PsiElement && currentNode.parent != null && currentNode.parent.elementType is FSharpDummyBlockType) {
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
  override fun createNode(text: CharSequence?) = FSharpDummyBlockImpl(this, text)
  override fun createCompositeNode() = FSharpDummyBlockImpl(this, null)
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
    return FSharpDummyBlockImpl(this, text)
  }

  override fun createCompositeNode(): ASTNode {
    return FSharpDummyBlockImpl(this, null)
  }

  override fun isReparseable(currentNode: ASTNode, newText: CharSequence, fileLanguage: Language, project: Project) =
    newText.trim().startsWith("module")
}

class FSharpCommentType : FSharpReparseableElementType("COMMENT") {
  override fun createNode(text: CharSequence?) = FSharpDummyBlockImpl(this, text)
  override fun createCompositeNode() = FSharpDummyBlockImpl(this, null)
  override fun isReparseable(currentNode: ASTNode, newText: CharSequence, fileLanguage: Language, project: Project) =
    newText.startsWith("(*") && newText.endsWith("*)") || newText.startsWith("//")
}

inline fun createCompositeElementType(debugName: String, crossinline elementFactory: (FSharpElementType) -> ASTNode) =
  object : FSharpCompositeElementType(debugName) {
    override fun createCompositeNode() = elementFactory(this)
  }
