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

  // parse [<...>]
  private fun PsiBuilder.parseAttribute() {
    eatUntilAny(FSharpTokenType.GREATER_RBRACK)
    tryEatAnyToken(FSharpTokenType.GREATER_RBRACK)
  }

  private fun PsiBuilder.parseBlock(): Boolean {
    return tryParse(FSharpElementTypes.INDENTATION_BLOCK) {
      val myIndentation = getCurrentIndentation()
      val isTopLevel = myIndentation == 0
      parseExpressions()
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

  //todo: multiline string token
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
    if (!FSharpTokenType.INTERPOLATED_STRING_STARTS.contains(tokenType)) false
    else parse(FSharpElementTypes.INTERPOLATED_STRING_LITERAL_EXPRESSION) {
      var nestingDepth = 0
      whileMakingProgress {
        if (tokenType in FSharpTokenType.INTERPOLATED_STRING_STARTS) nestingDepth += 1
        if (tokenType in FSharpTokenType.INTERPOLATED_STRING_ENDS) nestingDepth -= 1
        if (!parseAnyStringExpression()) advanceLexerWithNewLineCounting()
        nestingDepth != 0
      }
    }

  private fun PsiBuilder.parseExpressions() {
    while (!eof() && tokenType != FSharpTokenType.NEW_LINE) {
      if (!parseDummyExpression()) advanceLexerWithNewLineCounting()
    }
    if (eof()) return
    advanceLexerWithNewLineCounting()
    trySkipEmptyLines()
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
      trySkipEmptyLines()
      when (tokenType) {
        // parse nested module as a simple indentation block
        FSharpTokenType.EQUALS -> false
        else -> {
          whileMakingProgress { parseBlock() }
          true
        }
      }
    }

  private fun PsiBuilder.parseNamespace() {
    parse(FSharpElementTypes.NAMESPACE) {
      advanceLexer() // skip namespace token
      tryMoveToNextLine()
      trySkipEmptyLines()
      whileMakingProgress {
        parseBlock()
        tokenType != FSharpTokenType.NAMESPACE
      }
    }
  }

  private fun PsiBuilder.processQualifiedName() = tryEatAllTokens(FSharpTokenType.IDENT, FSharpTokenType.DOT)

  private fun PsiBuilder.tryMoveToNextLine(): Boolean {
    while (!eof() && tokenType != FSharpTokenType.NEW_LINE) {
      advanceLexerWithNewLineCounting()
    }
    if (eof()) return false
    advanceLexerWithNewLineCounting()
    return true
  }

  private fun PsiBuilder.eatFilteredTokens() =
    tryEatAllTokens(
      FSharpTokenType.WHITESPACE,
      FSharpTokenType.NEW_LINE,
      FSharpTokenType.LINE_COMMENT,
      FSharpTokenType.BLOCK_COMMENT
    )

  private fun PsiBuilder.trySkipEmptyLines(): Boolean {
    //include comments
    var wasSkipped = false
    while (isLineEmpty(this)) {
      wasSkipped = true
      if (!tryMoveToNextLine()) break
    }
    return wasSkipped
  }

  private fun PsiBuilder.advanceLexerWithNewLineCounting() {
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

  private fun isLineEmpty(builder: PsiBuilder): Boolean {
    var tokenType = builder.tokenType
    if (builder.tokenType == FSharpTokenType.WHITESPACE) {
      tokenType = builder.lookAhead(1)
    }
    return tokenType == FSharpTokenType.NEW_LINE
  }

  private fun PsiBuilder.getCurrentTokenOffsetInLine(): Int = currentOffset - getUserData(currentLineStartKey)!!

  fun PsiBuilder.nextIs(token: IElementType, step: Int = 1) = peekToken(step) == token


  /** Returns previous token type */
  fun PsiBuilder.previousToken(): IElementType? {
    var offset = 0
    while (true) {
      offset--
      val token = rawLookup(offset)
      if (token == null) return null
      if (!isWhitespaceOrComment(token)) return token
    }
  }

  /** If current token is in expected - eats and returns true */
  fun PsiBuilder.tryEatAnyToken(vararg tokens: IElementType): Boolean = if (tokenType in tokens) {
    advanceLexerWithNewLineCounting()
    true
  } else false

  /** Eats tokens until current token is not in given. Returns true if builder was advanced */
  fun PsiBuilder.tryEatAllTokens(vararg tokens: IElementType): Boolean {
    var count = 0
    while (tokenType in tokens) {
      advanceLexerWithNewLineCounting()
      count++
    }
    return count > 0
  }

  /**
   * Advances lexer if current token is of expected type, does nothing otherwise.
   * @return true if token matches, false otherwise.
   */
  fun PsiBuilder.tryEatToken(token: IElementType): Boolean = if (tokenType == token) {
    advanceLexerWithNewLineCounting()
    true
  } else false


  /** Advance lexer and returns eaten token */
  fun PsiBuilder.eatToken(): IElementType? {
    val token = tokenType
    advanceLexerWithNewLineCounting()
    return token
  }

  /** Advance lexer until (exclusive) any given token type */
  fun PsiBuilder.eatUntilAny(vararg tokenTypes: IElementType) {
    while (tokenType != null && tokenType !in tokenTypes) {
      advanceLexerWithNewLineCounting()
    }
  }

  /** Advance lexer until (exclusive) new line token */
  fun PsiBuilder.eatToNewLine() {
    var i = 0
    while (true) {
      val token = rawLookup(i)
      if (token == null || token == FSharpTokenType.NEW_LINE) {
        val offset = rawTokenIndex() + i
        while (!eof() && rawTokenIndex() < offset) {
          advanceLexerWithNewLineCounting()
        }
        return
      }
      i++
    }
  }

  /** Returns next token text */
  fun PsiBuilder.peekTokenText(): String? {
    val m = mark()
    advanceLexerWithNewLineCounting()
    val text = tokenText
    m.rollbackTo()
    return text
  }

  /** Returns next token text. if step == 0 then returns current text  */
  fun PsiBuilder.peekTokenText(step: Int): String? {
    val m = mark()
    if (step < 0) throw Exception()
    for (i in 0 until step) advanceLexerWithNewLineCounting()

    val text = tokenText
    m.rollbackTo()
    return text
  }

  /** Returns next token type*/
  fun PsiBuilder.peekToken(): IElementType? {
    var offset = 0
    while (true) {
      offset++
      val token = rawLookup(offset)
      if (token == null) return null
      if (!isWhitespaceOrComment(token)) return token
    }
  }

  /** Returns next token type. If step == 0 - returns current token type */
  fun PsiBuilder.peekToken(step: Int): IElementType? {
    if (step == 0) return tokenType
    var offset = 0
    var read = 0
    while (true) {
      offset++
      val token = rawLookup(offset)
      if (token == null) return null
      if (!isWhitespaceOrComment(token)) read++
      if (read >= step) return token
    }
  }

  /** Performs given check and returns its result. After checking - rollbacks builder */
  inline fun <T> PsiBuilder.checkAhead(look: PsiBuilder.() -> T): T {
    val point = mark()
    val result = look()
    point.rollbackTo()
    return result
  }

  /** Repeats given action until lexer advances and action returns true */
  inline fun PsiBuilder.whileMakingProgress(action: PsiBuilder.() -> Boolean) {
    var position = currentOffset
    while (action() && position != currentOffset) {
      position = currentOffset
    }
  }

  /** Returns true if builder was advanced during given action */
  inline fun PsiBuilder.isProgressMade(action: () -> Unit): Boolean {
    val position = currentOffset
    action()
    return currentOffset > position
  }

  /**
   * Parse node of given type if builder was advanced.
   * Returns true if node was parsed (and builder was advanced)
   */
  inline fun PsiBuilder.parse(nodeType: IElementType, action: PsiBuilder.() -> Unit): Boolean {
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
  inline fun PsiBuilder.parse(action: () -> IElementType?): Boolean {
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
  inline fun PsiBuilder.tryParse(type: IElementType, action: () -> Boolean): Boolean {
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
  inline fun PsiBuilder.parseOrRollback(nodeType: IElementType, action: () -> Boolean): Boolean {
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

  /**
   * Parse node if action returns not null or rollback otherwise.
   * Node will not be created if lexer was not advance
   * returns true if node was parsed (and builder was advanced)
   */
  inline fun PsiBuilder.parseOrRollback(action: () -> IElementType?): Boolean {
    val mark = mark()
    val positionBefore = rawTokenIndex()

    val result = action()
    if (result == null) {
      mark.rollbackTo()
      return false
    }
    if (positionBefore == rawTokenIndex()) {
      mark.drop()
      return false
    }
    mark.done(result)
    return true
  }

  /** Scans lexer. Allows to rollback, if action returns true. Returns true if lexer was advanced */
  inline fun PsiBuilder.scanOrRollback(action: () -> Boolean): Boolean {
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
