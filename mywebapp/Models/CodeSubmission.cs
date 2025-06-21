namespace mywebapp.Models
{
    public class CodeSubmission
    {
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public string Code { get; set; } = string.Empty;
    }
}