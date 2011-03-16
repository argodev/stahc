
open Ornl.Csmd.Csrg.Stahc.Core
open CommandLine
open CommandLine.Text
open System
open System.Diagnostics

type CommandLineOptions = 

    [<Option("o", "operation", Required = true,
            HelpText = "Operation to perform. Must be one of the following: Deploy, RetrieveOutput, CleanUp, or FullTest")>]
    val public Operation : ControlOperation // = ControlOperation.Deploy
    
    /// Path to XML file that represents the settings for your job. Alternatively, you can 
    /// provide the parameters at the command line
    [<Option("x", "settingsFile", Required = true,
            HelpText = "Path to XML file that represents the settings for your job. Alternatively, you can provide the parameters at the command line")>]
    val public XmlSettingsFile : string

    [<HelpOption(HelpText = "Display this help screen")>]
    member public x.GetUsage() =
        let help = new HelpText("\nSTAHC Utility")
        help.AddPreOptionsLine("This utility takes a collection of files and settings, uploads them to the")
        help.AddPreOptionsLine("Windows Azure platform and then deploys them. Also provided are methods for ")
        help.AddPreOptionsLine("terminating the operation and cleaning up after one is done. ")
        help.AddOptions(x)
        help.AddPostOptionsLine("\n")
        help



let writeStatusLine message =
    printfn "%s" message


let writeStatus message =
    printf "%s" message






[<EntryPoint>]
let main (args : string[]) =
    
    // tell the user about the app
    writeStatusLine ""
    writeStatusLine "Scientific Tool for Applications Harnessing the Cloud (STAHC)"
    writeStatusLine(String.Format("Operation started at: {0}", DateTime.Now))
    writeStatusLine String.Empty

    let stopwatch = new Stopwatch()
    stopwatch.Start()








    // finish up and close nicely
    writeStatusLine String.Empty
    stopwatch.Stop()
    writeStatusLine(String.Format("Operation Completed. Elapsed Time: {0}", stopwatch.Elapsed));

    // program exit code
    0