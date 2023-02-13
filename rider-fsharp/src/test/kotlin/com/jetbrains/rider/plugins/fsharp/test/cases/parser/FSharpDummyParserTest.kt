package com.jetbrains.rider.plugins.fsharp.test.cases.parser

import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.FSharpParserDefinition
import com.jetbrains.rider.test.RiderFrontendParserTest

class FSharpDummyParserTests : RiderFrontendParserTest("", "fs", FSharpParserDefinition()) {
  fun `test concatenation 01 - simple`() = doTest()
  fun `test concatenation 02 - space before plus`() = doTest()
  fun `test concatenation 03 - multiline`() = doTest()
  fun `test concatenation 04 - multiline with wrong offset`() = doTest()
  fun `test concatenation 05 - with ident`() = doTest()
  fun `test dummy`() = doTest()
  fun `test namespaces 01`() = doTest()
  fun `test namespaces 02 - recovery`() = doTest()
  fun `test top level module 01`() = doTest()
}
