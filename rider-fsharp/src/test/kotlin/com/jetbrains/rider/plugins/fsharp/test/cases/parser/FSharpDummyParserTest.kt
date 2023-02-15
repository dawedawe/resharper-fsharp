package com.jetbrains.rider.plugins.fsharp.test.cases.parser

import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.FSharpParserDefinition
import com.jetbrains.rider.ideaInterop.fileTypes.fsharp.FSharpScriptParserDefinition
import com.jetbrains.rider.test.RiderFrontendParserTest

class FSharpDummyParserTests : RiderFrontendParserTest("", "fs", FSharpParserDefinition()) {
  fun `test concatenation 01 - simple`() = doTest()
  fun `test concatenation 02 - space before plus`() = doTest()
  fun `test concatenation 03 - multiline`() = doTest()
  fun `test concatenation 04 - multiline with wrong offset`() = doTest()
  fun `test concatenation 05 - with ident`() = doTest()
  fun `test concatenation 06 - unfinished`() = doTest()
  fun `test concatenation 07 - multiline string`() = doTest()
  fun `test concatenation 08 - multiline string with wrong offset`() = doTest()
  fun `test concatenation 09 - with interpolated`() = doTest()

  fun `test regular strings 01`() = doTest()
  fun `test regular strings 02 - unfinished`() = doTest()

  fun `test interpolated strings 01`() = doTest()
  fun `test interpolated strings 02 - unfinished`() = doTest()

  fun `test namespaces 01`() = doTest()
  fun `test namespaces 02 - recovery`() = doTest()

  fun `test top level module 01`() = doTest()
  fun `test top level module 02 - rec`() = doTest()
  fun `test top level module 03`() = doTest()
  fun `test top level module 04 - attribute`() = doTest()

  fun `test nested module 01`() = doTest()

  fun `test dummy blocks 01`() = doTest()
  fun `test dummy blocks 02`() = doTest()
  fun `test dummy blocks 03 - new lines`() = doTest()
}


class FSharpScriptDummyParserTests : RiderFrontendParserTest("", "fsi", FSharpScriptParserDefinition()) {
  fun `test no module 01`() = doTest()
}
