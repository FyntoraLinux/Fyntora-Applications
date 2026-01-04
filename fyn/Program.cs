/////////////////////////
// Fyntora - AUR Helper//
/////////////////////////
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace fyn
{
    public class PackageInfo
    {
        public string Repo { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public string PackageBase { get; set; } = ""; // For AUR git cloning
    }

    public class Fyn
    {
        public static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: fyn <command> <package-name>");
                Console.WriteLine("Commands:");
                Console.WriteLine("  s <package>  - Search for a package");
                Console.WriteLine("  i <package>  - Install a package");
                return;
            }

            if (args[0] == "s")
            {
                string packageName = args[1];
                await SearchPackages(packageName);
            }
            else if (args[0] == "i")
            {
                string packageName = args[1];
                await InstallPackage(packageName);
            }
            else
            {
                Console.WriteLine($"Unknown command: {args[0]}");
                Console.WriteLine("Available commands:");
                Console.WriteLine("  fyn s <package>  - Search for a package");
                Console.WriteLine("  fyn i <package>  - Install a package");
            }
        }

        static async Task<List<PackageInfo>> SearchPackages(string packageName)
        {
            List<PackageInfo> allPackages = new List<PackageInfo>();

            // Search official repos using pacman
            var officialPackages = SearchOfficialRepos(packageName);
            allPackages.AddRange(officialPackages);

            // Search AUR
            var aurPackages = await SearchAUR(packageName);
            allPackages.AddRange(aurPackages);

            // Display results
            int totalCount = allPackages.Count;
            Console.WriteLine($"Found {totalCount} package(s)\n");

            if (totalCount == 0)
            {
                Console.WriteLine("No packages found.");
                return allPackages;
            }

            foreach (var pkg in allPackages)
            {
                Console.WriteLine($"{pkg.Repo}/{pkg.Name} {pkg.Version}");
                Console.WriteLine($"    {pkg.Description}");
            }

            return allPackages;
        }

        static List<PackageInfo> SearchOfficialRepos(string packageName)
        {
            List<PackageInfo> packages = new List<PackageInfo>();

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "pacman",
                    Arguments = $"-Ss {packageName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process? process = Process.Start(startInfo))
                {
                    if (process == null) return packages;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Parse pacman output
                    // Format: repo/package version
                    //     description
                    var lines = output.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        // Match: repo/package version (and optional extra info)
                        var match = Regex.Match(line, @"^(\S+)/(\S+)\s+(\S+)");
                        if (match.Success)
                        {
                            string repo = match.Groups[1].Value;
                            string name = match.Groups[2].Value;
                            string version = match.Groups[3].Value;
                            string description = "";

                            // Next line should be description
                            if (i + 1 < lines.Length)
                            {
                                description = lines[i + 1].Trim();
                                i++; // Skip next line
                            }

                            packages.Add(new PackageInfo
                            {
                                Repo = repo,
                                Name = name,
                                Version = version,
                                Description = description
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error searching official repos: {e.Message}");
            }

            return packages;
        }

        static async Task<List<PackageInfo>> SearchAUR(string packageName)
        {
            List<PackageInfo> packages = new List<PackageInfo>();

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync($"https://aur.archlinux.org/rpc/v5/search/{packageName}");
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    
                    using (JsonDocument doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;
                        var results = root.GetProperty("results");
                        
                        foreach (var pkg in results.EnumerateArray())
                        {
                            string name = pkg.GetProperty("Name").GetString() ?? "N/A";
                            string version = pkg.GetProperty("Version").GetString() ?? "N/A";
                            string description = pkg.GetProperty("Description").GetString() ?? "N/A";
                            string packageBase = pkg.GetProperty("PackageBase").GetString() ?? name;
                            
                            packages.Add(new PackageInfo
                            {
                                Repo = "aur",
                                Name = name,
                                Version = version,
                                Description = description,
                                PackageBase = packageBase
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error searching AUR: {e.Message}");
                }
            }

            return packages;
        }

        static async Task InstallPackage(string packageName)
        {
            Console.WriteLine($"Searching for {packageName}...\n");

            // Search for packages
            List<PackageInfo> allPackages = new List<PackageInfo>();
            
            var officialPackages = SearchOfficialRepos(packageName);
            allPackages.AddRange(officialPackages);
            
            var aurPackages = await SearchAUR(packageName);
            allPackages.AddRange(aurPackages);

            if (allPackages.Count == 0)
            {
                Console.WriteLine($"Error: Package '{packageName}' not found.");
                return;
            }

            // Check for exact match
            var exactMatch = allPackages.FirstOrDefault(p => p.Name == packageName);
            
            if (exactMatch != null)
            {
                // Exact match found, install directly
                Console.WriteLine($"Found exact match: {exactMatch.Repo}/{exactMatch.Name} {exactMatch.Version}");
                await InstallSelectedPackage(exactMatch);
                return;
            }

            // Multiple matches, let user select
            Console.WriteLine($"Found {allPackages.Count} matching package(s):\n");
            
            int page = 0;
            int pageSize = 10;
            
            while (true)
            {
                int start = page * pageSize;
                int end = Math.Min(start + pageSize, allPackages.Count);
                
                for (int i = start; i < end; i++)
                {
                    var pkg = allPackages[i];
                    Console.WriteLine($"[{i + 1}] {pkg.Repo}/{pkg.Name} {pkg.Version}");
                    Console.WriteLine($"    {pkg.Description}");
                }

                Console.WriteLine();
                
                if (end >= allPackages.Count)
                {
                    Console.Write("Enter number or package name to install (or 'q' to quit): ");
                }
                else
                {
                    Console.Write($"Showing {start + 1}-{end} of {allPackages.Count}. Enter number/name, 'more' for next page, or 'q' to quit: ");
                }

                string? input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input) || input.ToLower() == "q")
                {
                    Console.WriteLine("Installation cancelled.");
                    return;
                }

                if (input.ToLower() == "more" || input.ToLower() == "m")
                {
                    if (end < allPackages.Count)
                    {
                        page++;
                        Console.WriteLine();
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("No more packages to show.\n");
                        continue;
                    }
                }

                // Check if input is a number
                if (int.TryParse(input, out int selection))
                {
                    if (selection > 0 && selection <= allPackages.Count)
                    {
                        var selectedPackage = allPackages[selection - 1];
                        await InstallSelectedPackage(selectedPackage);
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid number. Please enter 1-{allPackages.Count}.\n");
                        continue;
                    }
                }

                // Check if input matches a package name
                var selectedByName = allPackages.FirstOrDefault(p => p.Name == input);
                
                if (selectedByName != null)
                {
                    await InstallSelectedPackage(selectedByName);
                    return;
                }
                else
                {
                    Console.WriteLine($"Package '{input}' not found in results. Try again.\n");
                }
            }
        }

        static async Task InstallSelectedPackage(PackageInfo package)
        {
            Console.WriteLine($"\nInstalling {package.Repo}/{package.Name} {package.Version}...\n");

            if (package.Repo == "aur")
            {
                await InstallFromAUR(package.Name);
            }
            else
            {
                InstallFromOfficialRepo(package.Name);
            }
        }

        static void InstallFromOfficialRepo(string packageName)
        {
            Console.WriteLine($"Installing from official repository using pacman...");
            
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"pacman -S {packageName}",
                UseShellExecute = false
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Console.WriteLine("Error: Could not start pacman");
                    return;
                }

                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"\n{packageName} installed successfully!");
                }
                else
                {
                    Console.WriteLine($"\nError: Failed to install {packageName}");
                }
            }
        }

        static async Task InstallFromAUR(string packageName)
        {
            // First, get the PackageBase from the API
            string packageBase = packageName;
            
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync($"https://aur.archlinux.org/rpc/v5/info?arg[]={packageName}");
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;
                        var results = root.GetProperty("results");
                        
                        if (results.GetArrayLength() > 0)
                        {
                            var pkg = results[0];
                            packageBase = pkg.GetProperty("PackageBase").GetString() ?? packageName;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Warning: Could not fetch package info from AUR API: {e.Message}");
                    Console.WriteLine($"Attempting to use package name '{packageName}' for cloning...");
                }
            }

            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string cacheDir = Path.Combine(homeDir, ".cache", "fyn");
            string packageDir = Path.Combine(cacheDir, packageBase);
            
            try
            {
                Directory.CreateDirectory(cacheDir);
                
                if (Directory.Exists(packageDir))
                {
                    Console.WriteLine($"Package directory exists in cache. Updating...");
                    int gitPullResult = RunCommand("git", "pull", packageDir);
                    
                    if (gitPullResult != 0)
                    {
                        Console.WriteLine("Error updating repository. Trying fresh clone...");
                        Directory.Delete(packageDir, true);
                        int gitCloneResult = RunCommand("git", $"clone https://aur.archlinux.org/{packageBase}.git", cacheDir);
                        
                        if (gitCloneResult != 0)
                        {
                            Console.WriteLine($"Error: Failed to clone repository.");
                            return;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Cloning AUR repository for {packageBase}...");
                    int gitCloneResult = RunCommand("git", $"clone https://aur.archlinux.org/{packageBase}.git", cacheDir);
                    
                    if (gitCloneResult != 0)
                    {
                        Console.WriteLine($"Error: Failed to clone repository.");
                        return;
                    }
                }

                string pkgbuildPath = Path.Combine(packageDir, "PKGBUILD");
                if (File.Exists(pkgbuildPath))
                {
                    Console.WriteLine("\n==> PKGBUILD:");
                    Console.WriteLine(File.ReadAllText(pkgbuildPath));
                    Console.WriteLine();
                }

                Console.Write("Proceed with installation? [Y/n] ");
                string? response = Console.ReadLine();
                if (!string.IsNullOrEmpty(response) && response.ToLower() != "y")
                {
                    Console.WriteLine("Installation cancelled.");
                    return;
                }

                Console.WriteLine("\nBuilding package...");
                int makepkgResult = RunCommand("makepkg", "-si", packageDir);
                
                if (makepkgResult == 0)
                {
                    Console.WriteLine($"\n{packageName} installed successfully!");
                }
                else
                {
                    Console.WriteLine($"\nError: Failed to build/install {packageName}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during installation: {e.Message}");
            }
        }

        static int RunCommand(string command, string arguments, string workingDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Console.WriteLine($"Error: Could not start {command}");
                    return -1;
                }

                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}