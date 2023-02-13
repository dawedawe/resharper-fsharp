package com.jetbrains.rider.ideaInterop.fileTypes.fsharp

import com.jetbrains.rider.ideaInterop.fileTypes.RiderLanguageFileTypeBase
import com.jetbrains.rider.plugins.fsharp.FSharpIcons

object FSharpFileType : RiderLanguageFileTypeBase(FSharpLanguage) {
  override fun getName() = FSharpLanguage.displayName
  override fun getDefaultExtension() = "fs"
  override fun getDescription() = "F# file"
  override fun getIcon() = FSharpIcons.FSharp
}

object FSharpScriptFileType : RiderLanguageFileTypeBase(FSharpScriptLanguage) {
  override fun getName() = FSharpScriptLanguage.displayName
  override fun getDefaultExtension() = "fsx"
  override fun getDescription() = "F# script file"
  override fun getIcon() = FSharpIcons.FSharpScript
}