using System.Text;
using System.Text.RegularExpressions;
using Theragraf.Core.Models;

namespace Theragraf.Functions.Services;

public sealed partial class PromptInputHardeningService : IPromptInputHardeningService
{
    public const int MaxTranscriptLength = 50_000;
    public const int MaxTherapistNameLength = 200;
    public const int MaxClientIdLength = 200;
    public const int MaxDemographicsFieldLength = 2_000;

    private static readonly Regex SuspiciousPromptInjectionPattern = MyRegex();

    public bool TrySanitize(TranscriptInput input, out TranscriptInput sanitized, out string? errorMessage)
    {
        var rawTranscript = Normalize(input.RawTranscript);
        var therapistName = Normalize(input.TherapistName);
        var clientId = Normalize(input.ClientId);

        if (string.IsNullOrWhiteSpace(rawTranscript) ||
            string.IsNullOrWhiteSpace(therapistName) ||
            string.IsNullOrWhiteSpace(clientId))
        {
            sanitized = input;
            errorMessage = "RawTranscript, TherapistName, and ClientId are required.";
            return false;
        }

        if (rawTranscript.Length > MaxTranscriptLength)
        {
            sanitized = input;
            errorMessage = $"RawTranscript exceeds the maximum allowed length of {MaxTranscriptLength} characters.";
            return false;
        }

        if (therapistName.Length > MaxTherapistNameLength)
        {
            sanitized = input;
            errorMessage = $"TherapistName exceeds the maximum allowed length of {MaxTherapistNameLength} characters.";
            return false;
        }

        if (clientId.Length > MaxClientIdLength)
        {
            sanitized = input;
            errorMessage = $"ClientId exceeds the maximum allowed length of {MaxClientIdLength} characters.";
            return false;
        }

        if (ContainsSuspiciousPromptInjection(rawTranscript))
        {
            sanitized = input;
            errorMessage = "RawTranscript contains suspicious instruction-like content and was rejected.";
            return false;
        }

        sanitized = input with
        {
            RawTranscript = rawTranscript,
            TherapistName = therapistName,
            ClientId = clientId,
        };
        errorMessage = null;
        return true;
    }

    public bool TrySanitize(ClientDemographicsSummary input, out ClientDemographicsSummary sanitized, out string? errorMessage)
    {
        if (!TrySanitizeDemographicsFields(input.PriorDiagnoses, input.FunctionalLimitations,
            out var priorDiagnoses, out var functionalLimitations, out errorMessage))
        {
            sanitized = input;
            return false;
        }

        sanitized = input with
        {
            PriorDiagnoses = priorDiagnoses,
            FunctionalLimitations = functionalLimitations,
        };
        errorMessage = null;
        return true;
    }

    public bool TrySanitize(UpsertClientDemographicsRequest input, out UpsertClientDemographicsRequest sanitized, out string? errorMessage)
    {
        if (!TrySanitizeDemographicsFields(input.PriorDiagnoses, input.FunctionalLimitations,
            out var priorDiagnoses, out var functionalLimitations, out errorMessage))
        {
            sanitized = input;
            return false;
        }

        sanitized = input with
        {
            PriorDiagnoses = priorDiagnoses,
            FunctionalLimitations = functionalLimitations,
        };
        errorMessage = null;
        return true;
    }

    private static bool TrySanitizeDemographicsFields(
        string? priorDiagnoses,
        string? functionalLimitations,
        out string? sanitizedPriorDiagnoses,
        out string? sanitizedFunctionalLimitations,
        out string? errorMessage)
    {
        sanitizedPriorDiagnoses = NormalizeOptional(priorDiagnoses);
        sanitizedFunctionalLimitations = NormalizeOptional(functionalLimitations);

        if (sanitizedPriorDiagnoses is { Length: > MaxDemographicsFieldLength })
        {
            errorMessage = $"PriorDiagnoses exceeds the maximum allowed length of {MaxDemographicsFieldLength} characters.";
            return false;
        }

        if (sanitizedFunctionalLimitations is { Length: > MaxDemographicsFieldLength })
        {
            errorMessage = $"FunctionalLimitations exceeds the maximum allowed length of {MaxDemographicsFieldLength} characters.";
            return false;
        }

        if (ContainsSuspiciousPromptInjection(sanitizedPriorDiagnoses) || ContainsSuspiciousPromptInjection(sanitizedFunctionalLimitations))
        {
            errorMessage = "Demographics contain suspicious instruction-like content and were rejected.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    internal static bool ContainsSuspiciousPromptInjection(string? text) =>
        !string.IsNullOrWhiteSpace(text) && SuspiciousPromptInjectionPattern.IsMatch(text);

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;

        foreach (var ch in value.Trim())
        {
            if (char.IsControl(ch) && ch is not '\r' and not '\n' and not '\t')
                continue;

            if (char.IsWhiteSpace(ch))
            {
                if (ch is '\r' or '\n')
                {
                    if (builder.Length > 0 && builder[^1] != '\n')
                        builder.Append('\n');
                    previousWasWhitespace = false;
                    continue;
                }

                if (previousWasWhitespace)
                    continue;

                builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            builder.Append(ch);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    internal static string? NormalizeOptional(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length == 0 ? null : normalized;
    }

    [GeneratedRegex(@"(?ix)(?:ignore\s+(?:all\s+)?(?:previous|prior|above|system|developer|earlier)\s+instructions|disregard\s+(?:all\s+)?(?:previous|prior|above|system|developer|earlier)\s+instructions|forget\s+(?:all\s+)?(?:previous|prior|above|system|developer|earlier)\s+instructions|you\s+are\s+now|act\s+as\s+(?:an?|the)|system\s*prompt|developer\s*message|reveal\s+(?:your\s+)?instructions|print\s+(?:the\s+)?(?:hidden|system|developer)\s+prompt|output\s+the\s+full\s+prompt|jailbreak|do\s+not\s+follow\s+(?:the\s+)?above\s+instructions|instead,?\s+(?:follow|do|return|output)|respond\s+with\s+exactly|return\s+only\s+the\s+prompt|<\s*system\s*>|```(?:system|prompt|instructions|json)?)", RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}
