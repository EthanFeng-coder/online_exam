using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace mywebapp.Pages
{
    public class CompletionModel : PageModel
    {
        private readonly IHttpClientFactory _clientFactory;

        public CompletionModel(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [BindProperty(SupportsGet = true)]
        public string StudentId { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public int GroupId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool Done { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(StudentId))
            {
                return RedirectToPage("/Index");
            }

            // Mark student as completely done since there's only one group
            var client = _clientFactory.CreateClient("Questions");
            await client.PostAsJsonAsync($"api/Questions/markAsDone", new
            {
                StudentId = StudentId,
                GroupId = GroupId,
                CompletedAt = DateTime.UtcNow,
                Done = true
            });

            return Page();
        }
    }
}