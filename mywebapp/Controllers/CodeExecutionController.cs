using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace mywebapp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CodeExecutionController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public CodeExecutionController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteCode([FromBody] CodeExecutionRequest request)
        {
            var tempPath = Path.Combine(_environment.ContentRootPath, "TempCode");
            var projectName = $"CodeRunner_{Guid.NewGuid()}";
            var projectPath = Path.Combine(tempPath, projectName);

            try
            {
                Directory.CreateDirectory(projectPath);

                // Create new console project
                var createProcess = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "new console --force",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectPath
                };

                using (var process = Process.Start(createProcess))
                {
                    await process.WaitForExitAsync();
                }

                // Replace Program.cs content with user code
                var programPath = Path.Combine(projectPath, "Program.cs");
                await System.IO.File.WriteAllTextAsync(programPath, request.Code);

                // Build the project first to get compilation errors
                var buildProcess = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectPath
                };

                using (var process = Process.Start(buildProcess))
                {
                    var buildOutput = await process.StandardOutput.ReadToEndAsync();
                    var buildError = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        var errorOutput = !string.IsNullOrEmpty(buildError) ? buildError : buildOutput;
                        var errors = ParseCompilationErrors(errorOutput);

                        if (errors.Count == 0)
                        {
                            errors.Add(new CompilationError
                            {
                                Line = 1,
                                Column = 1,
                                Severity = "error",
                                ErrorCode = "BUILD001",
                                Message = "Build failed. Check your code for syntax errors.",
                                File = "Program.cs"
                            });
                        }

                        return BadRequest(new { 
                            Success = false,
                            Errors = errors 
                        });
                    }
                }

                // Run the project if build succeeded
                var runProcess = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --no-build",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectPath
                };

                using (var process = Process.Start(runProcess))
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(error))
                    {
                        // Parse runtime errors
                        var runtimeError = ParseRuntimeError(error);
                        return BadRequest(new { Error = runtimeError });
                    }

                    return Ok(new { Output = output });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = $"Execution error: {ex.Message}" });
            }
            finally
            {
                // Cleanup
                try
                {
                    if (Directory.Exists(projectPath))
                    {
                        Directory.Delete(projectPath, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private List<CompilationError> ParseCompilationErrors(string errorOutput)
        {
            var errors = new List<CompilationError>();
            
            if (string.IsNullOrWhiteSpace(errorOutput))
            {
                return errors;
            }

            // Try multiple error patterns
            var patterns = new[]
            {
                @"(Program\.cs)\((\d+),(\d+)\):\s*(error|warning)\s+(CS\d+):\s*(.+?)(?=(?:\r|\n|$))",
                @"error (CS\d+):\s*(.+?)(?=(?:\r|\n|$))",
                @"(error|warning):\s*(.+?)(?=(?:\r|\n|$))"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(errorOutput, pattern, RegexOptions.Multiline);
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        if (pattern == patterns[0])
                        {
                            errors.Add(new CompilationError
                            {
                                File = match.Groups[1].Value,
                                Line = int.Parse(match.Groups[2].Value),
                                Column = int.Parse(match.Groups[3].Value),
                                Severity = match.Groups[4].Value,
                                ErrorCode = match.Groups[5].Value,
                                //Message = match.Groups[6].Value.Trim()
                            });
                        }
                        else if (pattern == patterns[1])
                        {
                            errors.Add(new CompilationError
                            {
                                File = "Program.cs",
                                Line = 1,
                                Column = 1,
                                Severity = "error",
                                ErrorCode = match.Groups[1].Value,
                                //Message = match.Groups[2].Value.Trim()
                            });
                        }
                        else
                        {
                            errors.Add(new CompilationError
                            {
                                File = "Program.cs",
                                Line = 1,
                                Column = 1,
                                Severity = match.Groups[1].Value,
                                ErrorCode = "CS0000",
                                //Message = match.Groups[2].Value.Trim()
                            });
                        }
                    }
                    break; // Stop after first successful pattern match
                }
            }

            return errors;
        }

        private RuntimeError ParseRuntimeError(string errorOutput)
        {
            var lines = errorOutput.Split('\n');
            var exceptionPattern = @"^Unhandled exception\. (.+?): (.+)$";
            var locationPattern = @"at .+ in .+:line (\d+)";

            var error = new RuntimeError();

            foreach (var line in lines)
            {
                var exceptionMatch = Regex.Match(line, exceptionPattern);
                if (exceptionMatch.Success)
                {
                    error.Type = exceptionMatch.Groups[1].Value;
                    error.Message = exceptionMatch.Groups[2].Value;
                    continue;
                }

                var locationMatch = Regex.Match(line, locationPattern);
                if (locationMatch.Success)
                {
                    error.Line = int.Parse(locationMatch.Groups[1].Value);
                    break;
                }
            }

            return error;
        }
    }

    public class CodeExecutionRequest
    {
        public string Code { get; set; }
    }

    public class CompilationError
    {
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Severity { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
    }

    public class RuntimeError
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public int Line { get; set; }
    }
}