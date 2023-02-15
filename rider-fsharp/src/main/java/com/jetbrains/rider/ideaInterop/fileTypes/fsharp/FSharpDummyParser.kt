package com.jetbrains.rider.ideaInterop.fileTypes.fsharp

import com.intellij.lang.ASTNode
import com.intellij.lang.PsiBuilder
import com.intellij.lang.PsiParser
import com.intellij.openapi.util.Key
import com.intellij.psi.tree.IElementType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.lexer.FSharpTokenType
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.psi.impl.FSharpElementTypes

class FSharpDummyParser : PsiParser {
  override fun parse(root: IElementType, builder: PsiBuilder): ASTNode {
    builder.putUserData(currentLineStartKey, 0)
    when (root) {
      FSharpElementTypes.FILE -> builder.parseFile(root)
      FSharpElementTypes.SCRIPT_FILE -> builder.parseFile(root)
      FSharpElementTypes.NAMESPACE -> builder.parseNamespace()
      FSharpElementTypes.TOP_LEVEL_MODULE -> builder.tryParseTopLevelModule()
      FSharpElementTypes.INDENTATION_BLOCK -> builder.parseBlock()
      else -> {
        error("unknown root $root in parsing request")
      }
    }
    return builder.treeBuilt
  }

  private fun PsiBuilder.parseFile(fileElementType: IElementType) {
    parse(fileElementType) {
      whileMakingProgress {
        eatFilteredTokens()
        when (tokenType) {
          FSharpTokenType.NAMESPACE -> parseNamespace()
          FSharpTokenType.MODULE -> if (!tryParseTopLevelModule()) parseBlock()
          FSharpTokenType.LBRACK_LESS -> parseAttribute()
          else -> parseBlock()
        }
        true
      }
    }
  }

  private fun PsiBuilder.parseNamespace() {
    parse(FSharpElementTypes.NAMESPACE) {
      advanceLexer() // skip namespace token
      eatUntilAny(FSharpTokenType.NEW_LINE)
      eatFilteredTokens()
      whileMakingProgress {
        parseBlock()
        tokenType != FSharpTokenType.NAMESPACE
      }
    }
  }

  private fun PsiBuilder.tryParseTopLevelModule() =
    parseOrRollback(FSharpElementTypes.TOP_LEVEL_MODULE) {
      advanceLexer() // skip module token
      eatFilteredTokens()
      if (tokenType == FSharpTokenType.LBRACK_LESS) {
        parseAttribute()
        eatFilteredTokens()
      }
      if (tokenType == FSharpTokenType.REC) {
        advanceLexer() // skip rec token
        eatFilteredTokens()
      }
      processQualifiedName()
      eatFilteredTokens()
      when (tokenType) {
        // parse nested module as a simple indentation block
        FSharpTokenType.EQUALS -> false
        else -> {
          whileMakingProgress { parseBlock() }
          true
        }
      }
    }

  // parse [<...>]
  private fun PsiBuilder.parseAttribute() {
    eatUntilAny(FSharpTokenType.GREATER_RBRACK)
    tryEatAnyToken(FSharpTokenType.GREATER_RBRACK)
  }

  private fun PsiBuilder.parseBlock(): Boolean {
    return tryParse(FSharpElementTypes.INDENTATION_BLOCK) {
      val myIndentation = getCurrentIndentation()
      val isTopLevel = myIndentation == 0

      parseExpressionsOnLine()
      advanceLexerWithNewLineCounting()
      skipEmptyLines()
      parseBlockBody(myIndentation) || isTopLevel
    }
  }

  private fun PsiBuilder.parseBlockBody(parentIndentation: Int): Boolean {
    var hasBody = false
    while (getCurrentIndentation() > parentIndentation) {
      hasBody = true
      parseBlock()
    }
    return hasBody
  }

  private fun PsiBuilder.getCurrentIndentation() =
    if (tokenType == FSharpTokenType.WHITESPACE) tokenText!!.length else 0

  private fun PsiBuilder.parseDummyExpression() = parseConcatenation()

  private fun PsiBuilder.parseConcatenation() =
    tryParse(FSharpElementTypes.DUMMY_EXPRESSION) {
      val stringLineOffset = getCurrentTokenOffsetInLine()

      if (!parseStringExpression()) false
      else if (!scanOrRollback { tryParseConcatenationPartAhead(stringLineOffset) }) false
      else {
        whileMakingProgress {
          scanOrRollback { tryParseConcatenationPartAhead(stringLineOffset) }
        }
        true
      }
    }

  private fun PsiBuilder.tryParseConcatenationPartAhead(requiredStringOffset: Int): Boolean {
    val hasSpaceBeforePlus = eatFilteredTokens()
    if (tokenType != FSharpTokenType.PLUS) return false
    advanceLexer() // eat plus token
    // since "123" +"123" is not allowed
    if (hasSpaceBeforePlus && !eatFilteredTokens()) return false
    // since
    //    "123"
    // + "123"
    // is not allowed
    return getCurrentTokenOffsetInLine() >= requiredStringOffset && parseStringExpression()
  }

  private fun PsiBuilder.parseStringExpression() =
    parseInterpolatedStringExpression() || parseAnyStringExpression()

  private fun PsiBuilder.parseAnyStringExpression() =
    if (tokenType !in FSharpTokenType.ALL_STRINGS) false
    else parse {
      val interpolated = tokenType in FSharpTokenType.INTERPOLATED_STRINGS
      advanceLexerWithNewLineCounting()
      if (interpolated) FSharpElementTypes.INTERPOLATED_STRING_LITERAL_EXPRESSION_PART
      else FSharpElementTypes.STRING_LITERAL_EXPRESSION
    }

  private fun PsiBuilder.parseInterpolatedStringExpression() =
    if (!FSharpTokenType.INTERPOLATED_STRINGS.contains(tokenType)) false
    else parse(FSharpElementTypes.INTERPOLATED_STRING_LITERAL_EXPRESSION) {
      var nestingDepth = 0
      whileMakingProgress {
        if (tokenType in FSharpTokenType.INTERPOLATED_STRING_STARTS) nestingDepth += 1
        if (tokenType in FSharpTokenType.INTERPOLATED_STRING_ENDS) nestingDepth -= 1
        if (!parseAnyStringExpression()) advanceLexerWithNewLineCounting()
        nestingDepth != 0
      }
    }

  private fun PsiBuilder.parseExpressionsOnLine() {
    whileMakingProgress {
      if (!parseDummyExpression() && tokenType != FSharpTokenType.NEW_LINE) advanceLexerWithNewLineCounting()
      tokenType != FSharpTokenType.NEW_LINE
    }
  }

  private fun PsiBuilder.processQualifiedName() =
    tryEatAllTokens(FSharpTokenType.IDENT, FSharpTokenType.DOT)

  private fun PsiBuilder.eatFilteredTokens() =
    tryEatAllTokens(
      FSharpTokenType.WHITESPACE,
      FSharpTokenType.NEW_LINE,
      FSharpTokenType.LINE_COMMENT,
      FSharpTokenType.BLOCK_COMMENT
    )

  private fun PsiBuilder.skipEmptyLines() {
    whileMakingProgress {
      scanOrRollback {
        tryEatAllTokens(
          FSharpTokenType.WHITESPACE,
          FSharpTokenType.LINE_COMMENT,
          FSharpTokenType.BLOCK_COMMENT
        )
        if (tokenType != FSharpTokenType.NEW_LINE) false else {
          advanceLexerWithNewLineCounting()
          true
        }
      }
    }
  }

  private fun PsiBuilder.advanceLexerWithNewLineCounting() {
    if (eof()) return
    when (tokenType) {
      FSharpTokenType.NEW_LINE -> {
        advanceLexer()
        putUserData(currentLineStartKey, currentOffset)
      }

      in FSharpTokenType.STRINGS -> {
        val lastEndOfLineIndex = tokenText!!.lastIndexOf('\n')
        val stringStartOffset = currentOffset
        advanceLexer()
        if (lastEndOfLineIndex != -1) putUserData(currentLineStartKey, stringStartOffset + lastEndOfLineIndex + 1)
      }

      else -> advanceLexer()
    }
  }

  private fun PsiBuilder.getCurrentTokenOffsetInLine() = currentOffset - getUserData(currentLineStartKey)!!

  /** If current token is in expected - eats and returns true */
  private fun PsiBuilder.tryEatAnyToken(vararg tokens: IElementType): Boolean = if (tokenType in tokens) {
    advanceLexerWithNewLineCounting()
    true
  } else false

  /** Eats tokens until current token is not in given. Returns true if builder was advanced */
  private fun PsiBuilder.tryEatAllTokens(vararg tokens: IElementType): Boolean {
    var count = 0
    while (tokenType in tokens) {
      advanceLexerWithNewLineCounting()
      count++
    }
    return count > 0
  }

  /** Advance lexer until (exclusive) any given token type */
  private fun PsiBuilder.eatUntilAny(vararg tokenTypes: IElementType) {
    while (tokenType != null && tokenType !in tokenTypes) {
      advanceLexerWithNewLineCounting()
    }
  }

  /** Repeats given action until lexer advances and action returns true */
  private inline fun PsiBuilder.whileMakingProgress(action: PsiBuilder.() -> Boolean) {
    var position = currentOffset
    while (action() && position != currentOffset) {
      position = currentOffset
    }
  }

  /**
   * Parse node of given type if builder was advanced.
   * Returns true if node was parsed (and builder was advanced)
   */
  private inline fun PsiBuilder.parse(nodeType: IElementType, action: PsiBuilder.() -> Unit): Boolean {
    val position = rawTokenIndex()
    val mark = mark()
    action()
    if (position == rawTokenIndex()) {
      mark.drop()
      return false
    } else {
      mark.done(nodeType)
      return true
    }
  }

  /**
   * Parse node of returned type, or just scan, if action returns null.
   * Returns true if builder was advanced
   * */
  private inline fun PsiBuilder.parse(action: () -> IElementType?): Boolean {
    val mark = mark()
    val positionBefore = rawTokenIndex()

    val elementType = action()
    if (elementType == null) {
      mark.drop()
      return positionBefore != rawTokenIndex()
    }
    if (positionBefore == rawTokenIndex()) {
      mark.drop()
      return false
    } else {
      mark.done(elementType)
      return true
    }
  }

  /**
   * Parse node of returned type, or just scan, if action returns false.
   * Returns true if node was parsed (and builder was advanced)
   * */
  private inline fun PsiBuilder.tryParse(type: IElementType, action: () -> Boolean): Boolean {
    val mark = mark()
    val positionBefore = rawTokenIndex()

    if (!action()) {
      mark.drop()
      return false
    }
    if (positionBefore == rawTokenIndex()) {
      mark.drop()
      return false
    }

    mark.done(type)
    return true
  }

  /**
   * Parse node if action returns true or rollback otherwise.
   * Node will not be created if lexer was not advance
   * returns true if node was parsed (and builder was advanced)
   * */
  private inline fun PsiBuilder.parseOrRollback(nodeType: IElementType, action: () -> Boolean): Boolean {
    val mark = mark()
    val positionBefore = rawTokenIndex()

    if (!action()) {
      mark.rollbackTo()
      return false
    }
    if (positionBefore == rawTokenIndex()) {
      mark.drop()
      return false
    } else {
      mark.done(nodeType)
      return true
    }
  }

  /** Scans lexer. Allows to rollback, if action returns true. Returns true if lexer was advanced */
  private inline fun PsiBuilder.scanOrRollback(action: () -> Boolean): Boolean {
    val mark = mark()
    val positionBefore = rawTokenIndex()

    if (!action()) {
      mark.rollbackTo()
      return false
    }
    mark.drop()
    return positionBefore != rawTokenIndex()
  }

  private val currentLineStartKey = Key<Int>("currentLineStart")
}
