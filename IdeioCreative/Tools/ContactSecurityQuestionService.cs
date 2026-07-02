using System.Security.Cryptography;
using IdeioCreative.Models;
using Microsoft.AspNetCore.DataProtection;

namespace IdeioCreative.Tools
{
    public class ContactSecurityQuestionService
    {
        private static readonly (string Question, string Answer)[] Questions =
        [
            ("Güvenlik sorusu: 3 + 4 kaç eder?", "7"),
            ("Güvenlik sorusu: 5 + 2 kaç eder?", "7"),
            ("Güvenlik sorusu: 9 - 4 kaç eder?", "5"),
            ("Güvenlik sorusu: 2 + 6 kaç eder?", "8"),
            ("Güvenlik sorusu: 10 - 3 kaç eder?", "7"),
            ("Güvenlik sorusu: 4 + 4 kaç eder?", "8")
        ];

        private readonly IDataProtector _protector;

        public ContactSecurityQuestionService(IDataProtectionProvider dataProtectionProvider)
        {
            _protector = dataProtectionProvider.CreateProtector("IdeioCreative.ContactSecurityQuestion");
        }

        public SecurityQuestionViewModel CreateQuestion()
        {
            var selectedQuestion = Questions[RandomNumberGenerator.GetInt32(Questions.Length)];
            var payload = $"{selectedQuestion.Answer}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            return new SecurityQuestionViewModel
            {
                Question = selectedQuestion.Question,
                Token = _protector.Protect(payload)
            };
        }

        public bool Validate(string? token, string? answer)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(answer))
                return false;

            try
            {
                var payload = _protector.Unprotect(token);
                var parts = payload.Split('|');

                if (parts.Length != 2 || !long.TryParse(parts[1], out var createdAt))
                    return false;

                var questionAge = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(createdAt);

                if (questionAge > TimeSpan.FromMinutes(30))
                    return false;

                return string.Equals(parts[0], NormalizeAnswer(answer), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeAnswer(string answer)
        {
            return answer.Trim().ToLowerInvariant() switch
            {
                "beş" or "bes" => "5",
                "yedi" => "7",
                "sekiz" => "8",
                var normalizedAnswer => normalizedAnswer
            };
        }
    }
}
