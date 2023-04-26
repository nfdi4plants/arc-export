module Defaults

open System.Text.Json

let FormattedSerializerOptions = JsonSerializerOptions(WriteIndented = true)

let UnformattedSerializerOptions = JsonSerializerOptions()