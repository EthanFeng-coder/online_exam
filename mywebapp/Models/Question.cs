using System;
using System.Collections.Generic;

namespace mywebapp.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InitialCode { get; set; } = string.Empty;
        public List<string> TestCases { get; set; } = new List<string>();
        public int Difficulty { get; set; }

        public Question()
        {
            TestCases = new List<string>();
        }

        // Debug helper
        public override string ToString()
        {
            return $"Question {Id}: {Title} (Difficulty: {Difficulty})";
        }
    }
}