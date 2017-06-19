using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using static System.Console;

namespace _2cc_trainset_powered_wagon_running_cost {
    internal static class Program {
        private static readonly string[] copyThese = {
                                                         "2ccts.pnml",
                                                         "docs",
                                                         "gfx",
                                                         "lang",
                                                         "src",
                                                     };

        public static readonly string TargetDir = $"{IOUtils.AppDir}\\sources";
        private const string SourceDir = @"C:\Users\Cadde\Desktop\OTTD stuff\2cc in NML\2cc hg";

        private static readonly Stopwatch sw = Stopwatch.StartNew();
        private static double lastTime;

        private static readonly double[] runningCostMultipliers = {
                                                                      0, 0.05, 0.1, 0.25, 0.33, 0.5, 0.66, 0.75, 0.9,
                                                                      1, 1.25, 1.33, 1.5, 1.66, 1.75,
                                                                      2, 2.5,
                                                                      3, 4, 5, 6, 7, 8
                                                                  };

        private static void Main(string[] args) {
            Title = "2cc Trainset running costs mass replace";
            ForegroundColor = ConsoleColor.Gray;

            if (PoseQuestion("Do you want to copy sources?")) {
                WriteLine();
                PrintHeader("COPY SOURCE FILES");
                CopySourceFiles();
                PrintElapsed();
            }

            if (PoseQuestion("Do you want to alter the sources?")) {
                WriteLine();
                PrintHeader("FIX INCONSISTENCIES");
                ApplyFixes();
                PrintElapsed();

                PrintHeader("ADD PARAMETERS");
                AddParameters();
                PrintElapsed();

                PrintHeader("ADD DEFINES");
                AddDefines();
                PrintElapsed();

                PrintHeader("ALTER RUNNING COSTS, RELIABILITY DECAY, LOADING SPEEDS AND CALLBACKS");
                IterateAllAndAlter();
                PrintElapsed();
            }

            if (PoseQuestion("Do you want to compile?")) {
                WriteLine();
                PrintHeader("COMPILE AND INSTALL");
                CompileAndInstall();
                PrintElapsed();
            }

            WriteLine();
            ForegroundColor = ConsoleColor.Green;
            WriteLine("Press a key to exit...");
            ReadKey(true);
        }

        private static void AddDefines() {
            ForegroundColor = ConsoleColor.Green;
            var file = $"{TargetDir}\\src\\loadingspeeds.pnml";
            var contents = File.ReadAllText(file);

            var index = contents.IndexOf("//Intercity vehicles\r\n#define LOADINGSPEEDDEF_INTERCITY", StringComparison.Ordinal);

            if (index > -1)
                contents = contents.Insert(index, "// Coaches\r\n" +
                                       "#define LOADINGSPEEDDEF_COACH loading_speed: isUltraSpeed ? 255 : LOADINGSPEED(12);\r\n\r\n");

            File.WriteAllText(file, contents);

            WriteLine("Added 'LOADINGSPEEDDEF_COACH' to defines.");
            WriteLine();
        }

        private static void ApplyFixes() {
            ForegroundColor = ConsoleColor.Green;
            var file = $"{TargetDir}\\src\\EMU\\Italy_FS_ETR300_Settebello_item.pnml";
            var contents = File.ReadAllText(file);
            File.WriteAllText(file, contents.Replace("speed: 300 km/h;", "speed: 200 km/h;"));
            WriteLine("Fixed 'item_emu_Italy_FS_ETR300_Settebello' max speed.");
            WriteLine();
        }

        private static void CompileAndInstall() {
            // cc -D REPO_REVISION=6318 -D NEWGRF_VERSION=6318 -C -E -nostdinc -x c-header -o 2ccts.nml 2ccts.pnml
            // nmlc -c --verbosity=3 --grf 2ccts.grf 2ccts.nml
            PrintProcessHeader("CC");
            StartProcess("cc", "--version");
            StartProcess("cc", "-D REPO_REVISION=80085 -D NEWGRF_VERSION=80085 -C -E -nostdinc -x c-header -time -o 2ccts.nml 2ccts.pnml", 0);
            PrintProcessFooter();

            PrintProcessHeader("CUSTOM TAGS");
            var contents = "VERSION            :v80085 LEL (CADDE)\n" +
                           "VERSION_STRING     :v80085 LEL (CADDE)\n" +
                           "TITLE              :2cc Trains In NML ALA Cadde (risotto)\n" +
                           "FILENAME           :2ccts.grf\n" +
                           "REPO_HASH          :cadde3133755\n" +
                           "NEWGRF_VERSION     :80085\n";
            ForegroundColor = ConsoleColor.White;
            WriteLine(contents);
            File.WriteAllText($"{TargetDir}\\custom_tags.txt", contents);
            ForegroundColor = ConsoleColor.Green;
            WriteLine("\\o/   PASS!   \\o/");
            PrintProcessFooter();

            PrintProcessHeader("NMLC");
            StartProcess("nmlc", "--version");
            StartProcess("nmlc", "-c --verbosity=3 --grf 2ccts.grf 2ccts.nml", 0);
            PrintProcessFooter();

            PrintProcessHeader("INSTALL");
            // WE ARE ON WINDOWS DAMMIT! YOU HAS NO CHOICE!
            ForegroundColor = ConsoleColor.White;
            var grfFile = $"{TargetDir}\\2ccts.grf";
            var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var newgrfFolder = $"{myDocs}\\OpenTTD\\newgrf";
            var targetLocation = $"{newgrfFolder}\\2cctscadde.grf";
            WriteLine($"Copy '{grfFile.Replace(TargetDir, "")}' to '{targetLocation.Replace(myDocs, "My Documents")}'");
            if (File.Exists(grfFile))
                File.Copy(grfFile, targetLocation, true);
            PrintProcessFooter();

            ForegroundColor = defaultColor;
        }

        private static void AddParameters() {
            var headerFile = $"{TargetDir}\\src\\header.pnml";
            var input = File.ReadAllText(headerFile);
            var pattern = @"param \{\n\s*ISCONCEPT \{.+?\}\n\s*\}";
            var regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var res = regex.Match(input);
            if (res.Success) {
                var match = res.Groups[0].Value;

                var newParam =
                    "param {\n" +
                    "        param_locomotive_running_cost_dynamic {\n" +
                    "            type:       bool;\n" +
                    "            name:       string(STR_PARAM_LOCOMOTIVE_RUNNING_COST_DYNAMIC);\n" +
                    "            desc:       string(STR_PARAM_LOCOMOTIVE_RUNNING_COST_DYNAMIC_DESC);\n" +
                    "            def_value:  0;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_locomotive_running_cost {\n" +
                    "            type:       int;\n" +
                    "            name:       string(STR_PARAM_LOCOMOTIVE_RUNNING_COST);\n" +
                    "            desc:       string(STR_PARAM_LOCOMOTIVE_RUNNING_COST_DESC);\n" +
                    "            def_value:  100;\n" +
                    "            min_value:  0;\n" +
                    "            max_value:  2550;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_coach_running_cost_dynamic {\n" +
                    "            type:       bool;\n" +
                    "            name:       string(STR_PARAM_COACH_RUNNING_COST_DYNAMIC);\n" +
                    "            desc:       string(STR_PARAM_COACH_RUNNING_COST_DYNAMIC_DESC);\n" +
                    "            def_value:  0;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_coach_running_cost {\n" +
                    "            type:       int;\n" +
                    "            name:       string(STR_PARAM_COACH_RUNNING_COST);\n" +
                    "            desc:       string(STR_PARAM_COACH_RUNNING_COST_DESC);\n" +
                    "            def_value:  100;\n" +
                    "            min_value:  0;\n" +
                    "            max_value:  2550;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_wagon_running_cost_dynamic {\n" +
                    "            type:       bool;\n" +
                    "            name:       string(STR_PARAM_WAGON_RUNNING_COST_DYNAMIC);\n" +
                    "            desc:       string(STR_PARAM_WAGON_RUNNING_COST_DYNAMIC_DESC);\n" +
                    "            def_value:  0;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_wagon_running_cost {\n" +
                    "            type:       int;\n" +
                    "            name:       string(STR_PARAM_WAGON_RUNNING_COST);\n" +
                    "            desc:       string(STR_PARAM_WAGON_RUNNING_COST_DESC);\n" +
                    "            def_value:  100;\n" +
                    "            min_value:  0;\n" +
                    "            max_value:  2550;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_wagon_powered_running_cost_dynamic {\n" +
                    "            type:       bool;\n" +
                    "            name:       string(STR_PARAM_WAGON_POWERED_RUNNING_COST_DYNAMIC);\n" +
                    "            desc:       string(STR_PARAM_WAGON_POWERED_RUNNING_COST_DYNAMIC_DESC);\n" +
                    "            def_value:  0;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_wagon_powered_running_cost {\n" +
                    "            type:       int;\n" +
                    "            name:       string(STR_PARAM_WAGON_POWERED_RUNNING_COST);\n" +
                    "            desc:       string(STR_PARAM_WAGON_POWERED_RUNNING_COST_DESC);\n" +
                    "            def_value:  100;\n" +
                    "            min_value:  0;\n" +
                    "            max_value:  2550;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_wagon_unpowered_running_cost_dynamic {\n" +
                    "            type:       bool;\n" +
                    "            name:       string(STR_PARAM_WAGON_UNPOWERED_RUNNING_COST_DYNAMIC);\n" +
                    "            desc:       string(STR_PARAM_WAGON_UNPOWERED_RUNNING_COST_DYNAMIC_DESC);\n" +
                    "            def_value:  0;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_wagon_unpowered_running_cost {\n" +
                    "            type:       int;\n" +
                    "            name:       string(STR_PARAM_WAGON_UNPOWERED_RUNNING_COST);\n" +
                    "            desc:       string(STR_PARAM_WAGON_UNPOWERED_RUNNING_COST_DESC);\n" +
                    "            def_value:  100;\n" +
                    "            min_value:  0;\n" +
                    "            max_value:  2550;\n" +
                    "        }\n" +
                    "    }\n" +
                    "    param {\n" +
                    "        param_reliability_decay {\n" +
                    "            type:       int;\n" +
                    "            name:       string(STR_PARAM_RELIABILITY_DECAY);\n" +
                    "            desc:       string(STR_PARAM_RELIABILITY_DECAY_DESC);\n" +
                    "            def_value:  20;\n" +
                    "            min_value:  0;\n" +
                    "            max_value:  255;\n" +
                    "        }\n" +
                    "    }\n";
                var replace = $"{newParam}{res.Groups[0].Value}";
                File.WriteAllText(headerFile, input.Replace(match, replace));
                WriteLine($"Added new parameters in '{headerFile.Replace(TargetDir, "")}'");
            } else
                throw new InvalidOperationException("Could not find the location to insert parameters at.");

            var appendStrings =
                "\n\n" +
                "STR_PARAM_LOCOMOTIVE_RUNNING_COST_DYNAMIC             :Use dynamic running costs for locomotives\n" +
                "STR_PARAM_LOCOMOTIVE_RUNNING_COST_DYNAMIC_DESC        :When this is checked, locomotive running costs will be based on the current speed of the train.\n" +
                "STR_PARAM_LOCOMOTIVE_RUNNING_COST                     :Locomotives running cost percentage\n" +
                "STR_PARAM_LOCOMOTIVE_RUNNING_COST_DESC                :Sets the running costs of locomotives as a percentage of their original value.\n" +
                "STR_PARAM_COACH_RUNNING_COST_DYNAMIC                  :Use dynamic running costs for coaches\n" +
                "STR_PARAM_COACH_RUNNING_COST_DYNAMIC_DESC             :When this is checked, coach (passengers and mail etc) running costs will be based on the current speed of the train.\n" +
                "STR_PARAM_COACH_RUNNING_COST                          :Coaches running cost percentage\n" +
                "STR_PARAM_COACH_RUNNING_COST_DESC                     :Sets the running costs of coaches (passengers and mail etc) as a percentage of their original value.\n" +
                "STR_PARAM_WAGON_RUNNING_COST_DYNAMIC                  :Use dynamic running costs for wagons\n" +
                "STR_PARAM_WAGON_RUNNING_COST_DYNAMIC_DESC             :When this is checked, wagon running costs will be based on the current speed of the train.\n" +
                "STR_PARAM_WAGON_RUNNING_COST                          :Wagon running cost percentage\n" +
                "STR_PARAM_WAGON_RUNNING_COST_DESC                     :Sets the running costs of wagons as a percentage of their original value.\n" +
                "STR_PARAM_WAGON_POWERED_RUNNING_COST_DYNAMIC          :Use dynamic running costs for powered wagons\n" +
                "STR_PARAM_WAGON_POWERED_RUNNING_COST_DYNAMIC_DESC     :When this is checked, powered wagon running costs will be based on the current speed of the train.\n" +
                "STR_PARAM_WAGON_POWERED_RUNNING_COST                  :Powered wagons running cost percentage\n" +
                "STR_PARAM_WAGON_POWERED_RUNNING_COST_DESC             :Sets the running costs of powered wagons as a percentage of their original value.\n" +
                "STR_PARAM_WAGON_UNPOWERED_RUNNING_COST_DYNAMIC        :Use dynamic running costs for unpowered wagons\n" +
                "STR_PARAM_WAGON_UNPOWERED_RUNNING_COST_DYNAMIC_DESC   :When this is checked, unpowered wagon running costs will be based on the current speed of the train.\n" +
                "STR_PARAM_WAGON_UNPOWERED_RUNNING_COST                :Unpowered wagons running cost percentage\n" +
                "STR_PARAM_WAGON_UNPOWERED_RUNNING_COST_DESC           :Sets the running costs of unpowered wagons as a percentage of their original value.\n" +
                "STR_PARAM_RELIABILITY_DECAY                           :Reliability decay\n" +
                "STR_PARAM_RELIABILITY_DECAY_DESC                      :Sets the reliability decay rate on the vehicles, lower values mean reliability drops slower.\n" +
                "";
                

            foreach (var file in Directory.GetFiles($"{TargetDir}\\lang")) {
                File.AppendAllText(file, appendStrings);
            }

            WriteLine("Updated all language files with parameter names and descriptions.\n");
        }

        private static void IterateAllAndAlter() {
            var files = Directory.GetFiles(TargetDir, "*.pnml", SearchOption.AllDirectories);
            var itemPattern = @"item\(FEAT_TRAINS, item_([A-Za-z0-9_]+?)\)";
            var reliabilityPattern = @"reliability_decay: (\d+?);";
            var locomotivePattern = @"item\(FEAT_TRAINS, item_(?!coach|mu|wagon|mtro_wagon).+?running_cost_factor: (\d+)?;.+?graphics \{";
            var coachPattern = @"item\(FEAT_TRAINS, item_(?:coach).+?running_cost_factor: (\d+)?;.+?graphics \{";
            var wagonPattern = @"item\(FEAT_TRAINS, item_(?:mu|wagon|mtro_wagon).+?running_cost_factor: (\d+)?;.+?graphics \{";
            var unpoweredWagonPattern = @"(livery_override \(.+?wagon_unpowered.*?running_cost_factor: )(\d+);";
            var poweredWagonPattern = @"(livery_override \(.+?wagon_powered.*?running_cost_factor: )(\d+);";

            var itemRegex = new Regex(itemPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var reliabilityRegex = new Regex(reliabilityPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var locomotiveRegex = new Regex(locomotivePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var coachRegex = new Regex(coachPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var wagonRegex = new Regex(wagonPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var unpoweredWagonRegex = new Regex(unpoweredWagonPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var poweredWagonRegex = new Regex(poweredWagonPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);

            var replacements = 0;
            var filesChanged = 0;

            var lastValue = 0;
            var i = 0;
            Write($"[{new string(' ', 100)}]");
            CursorLeft = 1;
            ForegroundColor = ConsoleColor.DarkRed;

            var largestFactors = new Dictionary<string, int>();
            foreach (var file in files) {
                var input = File.ReadAllText(file);

                var changes = false;

                var factors = new Dictionary<string, int>();
                var trainTypes =
                    new[] {
                        "locomotive",
                        "coach",
                        "wagon",
                        "wagon_powered",
                        "wagon_unpowered"
                    };


                var res = itemRegex.Match(input);
                string itemName;
                if (res.Success) {
                    itemName = res.Groups[1].Value;
                } else
                    continue;

                if (itemName.StartsWith("coach") && input.IndexOf("LOADINGSPEEDDEF_INTERCITY", StringComparison.Ordinal) > -1) {
                    input = input.Replace("LOADINGSPEEDDEF_INTERCITY", "LOADINGSPEEDDEF_COACH");
                    changes = true;
                    replacements++;
                }

                res = reliabilityRegex.Match(input);
                if (res.Success) {
                    var match = res.Groups[0].Value;
                    var replace = "reliability_decay: param_reliability_decay;";

                    input = input.Replace(match, replace);
                    changes = true;
                    replacements++;
                }

                res = locomotiveRegex.Match(input);
                if (res.Success) {
                    var match = res.Groups[0].Value;
                    var num = int.Parse(res.Groups[1].Value);
                    factors["locomotive"] = num;

                    var replace = res.Groups[0].Value;
                    replace += $"\n        running_cost_factor: switch_{itemName}_locomotive_running_cost_factor;\n" +
                               $"        purchase_running_cost_factor: ({num} * 10000) / (10000 / param_locomotive_running_cost) / 100;";

                    input = input.Replace(match, replace);
                    changes = true;
                    replacements++;
                }

                res = coachRegex.Match(input);
                if (res.Success) {
                    var match = res.Groups[0].Value;
                    var num = int.Parse(res.Groups[1].Value);
                    factors["coach"] = num;

                    var replace = res.Groups[0].Value;
                    replace += $"\n        running_cost_factor: switch_{itemName}_coach_running_cost_factor;\n" +
                               $"        purchase_running_cost_factor: ({num} * 10000) / (10000 / param_coach_running_cost) / 100;";

                    input = input.Replace(match, replace);
                    changes = true;
                    replacements++;
                }

                res = wagonRegex.Match(input);
                if (res.Success) {
                    var match = res.Groups[0].Value;
                    var num = int.Parse(res.Groups[1].Value);
                    factors["wagon"] = num;

                    var replace = res.Groups[0].Value;
                    replace += $"\n        running_cost_factor: switch_{itemName}_wagon_running_cost_factor;\n" +
                               $"        purchase_running_cost_factor: ({num} * 10000) / (10000 / param_wagon_running_cost) / 100;";

                    input = input.Replace(match, replace);
                    changes = true;
                    replacements++;
                }

                res = poweredWagonRegex.Match(input);
                if (res.Success) {
                    var match = res.Groups[0].Value;
                    var num = int.Parse(res.Groups[2].Value);
                    factors["wagon_powered"] = num;

                    var replace = res.Groups[1].Value;
                    replace += $"switch_{itemName}_wagon_powered_running_cost_factor;\n" +
                               $"        purchase_running_cost_factor: ({num} * 10000) / (10000 / param_wagon_powered_running_cost) / 100;";

                    input = input.Replace(match, replace);
                    changes = true;
                    replacements++;
                }

                res = unpoweredWagonRegex.Match(input);
                if (res.Success) {
                    var match = res.Groups[0].Value;
                    var num = int.Parse(res.Groups[2].Value);
                    factors["wagon_unpowered"] = num;

                    var replace = res.Groups[1].Value;
                    replace += $"switch_{itemName}_wagon_unpowered_running_cost_factor;\n" +
                               $"        purchase_running_cost_factor: ({num} * 10000) / (10000 / param_wagon_unpowered_running_cost) / 100;";

                    input = input.Replace(match, replace);
                    changes = true;
                    replacements++;

                }

                if (changes) {
                    filesChanged++;
                    File.WriteAllText(file, input);

                    var graphicsAppend = "";
                    var largestFactor = 0;
                    foreach (var type in trainTypes) {
                        if (factors.ContainsKey(type)) {
                            largestFactor = Math.Max(largestFactor, factors[type]);

                            graphicsAppend +=
                                $"\n\n// Dynamic running cost for {type}.\n" +
                                $"switch(FEAT_TRAINS, PARENT, switch_{itemName}_{type}_running_cost_factor,\n" +
                                $"    [STORE_TEMP(({factors[type]} * 10000) / (10000 / param_{type}_running_cost) / 100, 0),\n" +
                                $"    param_{type}_running_cost_dynamic]) {{\n" +
                                $"    0: LOAD_TEMP(0);\n" +
                                $"    1: return (current_speed * 100 / max_speed) * LOAD_TEMP(0) / 100;\n" +
                                //$"}}\n\n" +
                                //$"switch(FEAT_TRAINS, SELF, switch_{itemName}_{type}_purchase_running_cost_factor, 0) {{\n" +
                                //$"    return ({factors[type]} * 10000) / (10000 / param_{type}_running_cost) / 100;\n" +
                                $"}}";
                        }
                    }
                    largestFactors.Add(itemName, largestFactor);

                    File.AppendAllText(file.Replace("item.pnml", "graphics.pnml"), graphicsAppend);
                }
                var value = (int)(100 * ((double)i / files.Length));
                var diff = value - lastValue;
                Write(new string('█', diff));
                lastValue = value;
                i++;
            }
            ForegroundColor = ConsoleColor.Green;
            CursorLeft = 1;
            Write(new string('█', 100));

            var sorted = largestFactors.OrderBy(pair => pair.Value).Reverse().Take(10);

            WriteLine();
            WriteLine($"A total of {replacements} replacements in {filesChanged} files out of {files.Length} files were made.\n");
            WriteLine($"The ten largest encountered running cost factors were:\n");
            foreach (var factor in sorted)
                WriteLine($"{factor.Value,-6} in '{factor.Key}'");
            WriteLine();
            ForegroundColor = defaultColor;
        }

        private static void CopySourceFiles() {
            WriteLine($"Copying source files...\n  from: '{SourceDir}'\n  to:   '{TargetDir}'\n");

            if (!IOUtils.TryDeleteDirectory(TargetDir, true))
                throw new IOException("Could not delete old source files!");
            if (!IOUtils.TryCreateDirectory(TargetDir))
                throw new IOException("Could not create source directory!");

            foreach (var fileOrDirectory in copyThese) {
                ForegroundColor = defaultColor;
                WriteLine($"{fileOrDirectory} ...");
                Write($"[{new string(' ', 100)}]");
                CursorLeft = 1;
                var absoluteSourcePath = $"{SourceDir}\\{fileOrDirectory}";
                var absoluteTargetPath = $"{TargetDir}\\{fileOrDirectory}";
                if (File.Exists(absoluteSourcePath)) {
                    var directoryName = new FileInfo(absoluteTargetPath).DirectoryName;
                    if (directoryName != null)
                        IOUtils.TryCreateDirectory(directoryName);
                    File.Copy(absoluteSourcePath, absoluteTargetPath);
                    ForegroundColor = ConsoleColor.Green;
                    Write(new string('█', 100));
                } else {
                    Directory.CreateDirectory(absoluteTargetPath);
                    foreach (var dir in Directory.GetDirectories(absoluteSourcePath, "*.*", SearchOption.AllDirectories))
                        IOUtils.TryCreateDirectory(dir.Replace(SourceDir, TargetDir));
                    var files = Directory.GetFiles(absoluteSourcePath, "*.*", SearchOption.AllDirectories);
                    var lastValue = 0;
                    ForegroundColor = ConsoleColor.DarkRed;
                    for (var i = 0; i < files.Length; i++) {
                        File.Copy(files[i], files[i].Replace(SourceDir, TargetDir));
                        var value = (int)(100 * ((double)i / files.Length));
                        var diff = value - lastValue;
                        Write(new string('█', diff));
                        lastValue = value;
                    }
                    ForegroundColor = ConsoleColor.Green;
                    CursorLeft = 1;
                    Write(new string('█', 100));
                }
                WriteLine();
                WriteLine();
            }
        }

        private static readonly object lockObject = new object();
        private static ConsoleColor defaultColor = ConsoleColor.Gray;

        private static void StartProcess(string fileName, string arguments, int? expectedCode = null) {
            lock (lockObject) {
                var process = new Process {
                                  StartInfo = new ProcessStartInfo(fileName, arguments) {
                                                  CreateNoWindow = true,
                                                  RedirectStandardError = true,
                                                  RedirectStandardOutput = true,
                                                  WorkingDirectory = TargetDir,
                                                  UseShellExecute = false,
                                              },
                              };

                process.OutputDataReceived += (sender, args) => {
                    if (string.IsNullOrWhiteSpace(args.Data))
                        return;
                    ForegroundColor = ConsoleColor.White;
                    WriteLine("> " + args.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                //process.BeginErrorReadLine();

                process.WaitForExit();

                var lines = process.StandardError.ReadToEnd().Split(new []{'\n'}, StringSplitOptions.RemoveEmptyEntries);
                ForegroundColor = ConsoleColor.Red;
                if (lines.Length > 0)
                    WriteLine("\n! " + string.Join("\n! ", lines));

                if (!expectedCode.HasValue)
                    return;

                WriteLine();
                if (process.ExitCode == expectedCode.Value) {
                    ForegroundColor = ConsoleColor.Green;
                    WriteLine("\\o/   PASS!   \\o/");
                } else {
                    ForegroundColor = ConsoleColor.Red;
                    WriteLine("☺☺☺   !!! FAIL !!!   ☺☺☺");
                }
            }
        }

        private static void PrintHeader(string header) {
            ForegroundColor = ConsoleColor.Yellow;
            WriteLine(new string('=', 102));
            if (header.Length % 2 == 1)
                header += " ";
            var padding = new string(' ', 48 - header.Length / 2);
            WriteLine($"<<<{padding}{header}{padding}>>>");
            WriteLine(new string('=', 102));
            ForegroundColor = defaultColor;
        }

        private static void PrintProcessHeader(string proc) {
            ForegroundColor = ConsoleColor.Yellow;
            WriteLine($"\n[{proc}]".PadRight(103, '-') + "\n");
            ForegroundColor = defaultColor;
        }

        private static void PrintProcessFooter() {
            PrintElapsed("\n> ");
            ForegroundColor = ConsoleColor.Yellow;
            WriteLine("\n".PadRight(103, '¨'));
            ForegroundColor = defaultColor;
        }

        private static bool PoseQuestion(string question) {
            sw.Stop();
            var res = false;
            var key = ConsoleKey.LeftWindows;
            ForegroundColor = ConsoleColor.Cyan;
            while (key != ConsoleKey.Escape && key != ConsoleKey.N) {
                WriteLine($"\n{question} (Y/N or RETURN/ESC)");
                key = ReadKey(true).Key;
                if (key != ConsoleKey.Y && key != ConsoleKey.Enter)
                    continue;

                res = true;
                break;
            }
            ForegroundColor = defaultColor;
            sw.Start();
            return res;
        }

        private static void WriteError(string message) {
            ForegroundColor = ConsoleColor.Red;
            WriteLine(message);
            ForegroundColor = defaultColor;
        }

        private static void PrintElapsed(string prefix = "") {
            ForegroundColor = ConsoleColor.Yellow;
            var time = sw.Elapsed.TotalMilliseconds;
            WriteLine($"{prefix}Elapsed time: {time - lastTime:0.0} ms ({time:0.0} ms)");
            WriteLine();
            lastTime = time;
            ForegroundColor = defaultColor;
        }
    }
}
