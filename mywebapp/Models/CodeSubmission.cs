namespace mywebapp.Models
{
    public class CodeSubmission
    {
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    }
}