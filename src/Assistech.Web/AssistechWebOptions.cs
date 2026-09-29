namespace Assistech.Web;

public sealed class AssistechWebOptions
{
    public const string SectionName = "Assistech";

    /// <summary>URL base da API Assistech. Padrao: http://localhost:5099</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5099";
}
