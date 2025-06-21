namespace mywebapp.Models
{
    public class QuestionGroup
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<Question> Questions { get; set; } = new List<Question>();

        // Constructor to ensure Questions is initialized
        public QuestionGroup()
        {
            Questions = new List<Question>();
        }

        // Helper methods for question management
        public Question? GetQuestion(int index)
        {
            try
            {
                return index >= 0 && index < Questions.Count ? Questions[index] : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting question at index {index}: {ex.Message}");
                return null;
            }
        }

        public bool HasQuestion(int index)
        {
            return index >= 0 && index < Questions?.Count;
        }

        public int QuestionCount => Questions?.Count ?? 0;

        public void AddQuestion(Question question)
        {
            if (question == null)
            {
                Console.WriteLine("Cannot add null question");
                return;
            }
            Questions.Add(question);
            Console.WriteLine($"Added question {question.Id} to group {Id}");
        }

        public bool RemoveQuestion(int index)
        {
            try
            {
                if (HasQuestion(index))
                {
                    var question = Questions[index];
                    Questions.RemoveAt(index);
                    Console.WriteLine($"Removed question at index {index} from group {Id}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing question at index {index}: {ex.Message}");
                return false;
            }
        }

        // Debug helper
        public override string ToString()
        {
            return $"Group {Id}: {Title} ({QuestionCount} questions)";
        }
    }
}