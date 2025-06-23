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

        public DashboardModel(IHttpClientFactory clientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _clientFactory = clientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // First check TempData for newly logged in users
                var tempStudentId = TempData["StudentId"]?.ToString();
                if (!string.IsNullOrEmpty(tempStudentId))
                {
                    StudentId = tempStudentId;
                    HttpContext.Session.SetString("StudentId", tempStudentId);
                    Console.WriteLine($"Set student ID from TempData: {StudentId}");
                }
                // Then try query parameter
                else if (!string.IsNullOrEmpty(StudentId))
                {
                    HttpContext.Session.SetString("StudentId", StudentId);
                    Console.WriteLine($"Set student ID from query: {StudentId}");
                }
                // Finally try session
                else
                {
                    StudentId = HttpContext.Session.GetString("StudentId");
                    Console.WriteLine($"Retrieved student ID from session: {StudentId}");
                }

                if (string.IsNullOrEmpty(StudentId))
                {
                    Console.WriteLine("No student ID found - redirecting to login");
                    return RedirectToPage("/Index");
                }

                // Use named client from factory
                var client = _clientFactory.CreateClient("Questions");
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"api/Questions/groups/{Group}/questions/{Question}?studentId={StudentId}");

                var response = await client.SendAsync(request);
                Console.WriteLine($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API Response content length: {content.Length}");
                    
                    CurrentQuestion = JsonSerializer.Deserialize<Question>(content, 
                        new JsonSerializerOptions { 
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                    if (CurrentQuestion == null)
                    {
                        ErrorMessage = "Failed to deserialize question";
                        return Page();
                    }

                    return Page();
                }

                ErrorMessage = $"Failed to load question. Status: {response.StatusCode}";
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading question: {ex.Message}";
                Console.WriteLine($"Exception: {ex}");
                return Page();
            }
        }
    }
}