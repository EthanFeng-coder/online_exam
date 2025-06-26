using System.Net;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using mywebapp.Models;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace mywebapp.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public string? ErrorMessage { get; set; }
        public Question? CurrentQuestion { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Group { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Question { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StudentId { get; set; } = string.Empty;

        [BindProperty]
        public string SubmittedCode { get; set; } = string.Empty;

        public DashboardModel(IHttpClientFactory clientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _clientFactory = clientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Get parameters from query string
                if (string.IsNullOrEmpty(StudentId))
                {
                    return RedirectToPage("/Index");
                }

                var client = _clientFactory.CreateClient("Questions");
                var response = await client.GetAsync($"api/Questions/groups/{Group}/questions/{Question}?studentId={StudentId}");
                
                Console.WriteLine($"API Response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    CurrentQuestion = await response.Content.ReadFromJsonAsync<Question>();
                    return Page();
                }
                
                ErrorMessage = "Failed to load question";
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(StudentId))
                {
                    Console.WriteLine("No student ID found for submission");
                    return RedirectToPage("/Index");
                }

                Console.WriteLine($"Submitting solution for Student: {StudentId}, Group: {Group}, Question: {Question}");

                var client = _clientFactory.CreateClient("Questions");
                var submission = new CodeSubmission
                {
                    StudentId = StudentId,
                    GroupId = Group,
                    QuestionIndex = Question,
                    Code = SubmittedCode
                };

                // Submit directly - the backend will handle replacing existing submission
                var response = await client.PostAsJsonAsync("api/Questions/submit", submission);
                Console.WriteLine($"Submission response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SubmissionResult>();
                    if (result == null)
                    {
                        throw new InvalidOperationException("Failed to get submission result");
                    }

                    // Update student progress
                    var updateResponse = await client.PostAsJsonAsync("api/Questions/updateProgress", new
                    {
                        StudentId = StudentId,
                        GroupId = Group,
                        QuestionIndex = Question,
                        Completed = true
                    });

                    if (!updateResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Failed to update progress: {updateResponse.StatusCode}");
                        ErrorMessage = "Failed to update progress";
                        await OnGetAsync();
                        return Page();
                    }

                    return RedirectToPage("/Dashboard", new 
                    { 
                        studentId = StudentId,
                        group = result.NextGroup,
                        question = result.NextQuestion
                    });
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Submission failed: {errorContent}");
                ErrorMessage = "Failed to submit solution";
                await OnGetAsync();
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in submission: {ex.Message}");
                ErrorMessage = "Error submitting solution";
                await OnGetAsync();
                return Page();
            }
        }
    }

    public class SubmissionResult
    {
        public string Message { get; set; } = string.Empty;
        public int NextGroup { get; set; }
        public int NextQuestion { get; set; }
    }

    public class CodeSubmission
    {
        public required string StudentId { get; set; }
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public required string Code { get; set; }
        public bool ReplaceExisting { get; set; } = true;  // Default to replacing existing submissions
    }
}